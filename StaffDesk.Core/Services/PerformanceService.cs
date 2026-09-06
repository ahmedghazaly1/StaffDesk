using System.Text.Json;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class PerformanceService : IPerformanceService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IPerformanceRepository _repo;
    private readonly IAuditService _audit;
    private readonly ITaskRepository _tasks;

    public PerformanceService(IPerformanceRepository repo, IAuditService audit, ITaskRepository tasks)
    {
        _repo = repo;
        _audit = audit;
        _tasks = tasks;
    }

    public async Task<object> CreateCycleAsync(int actorEmployeeId, string actorRole, CreateReviewCycleRequest req)
    {
        DenyAdminAuditor(actorRole);
        RequireHrAdmin(actorRole);

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Name is required", 400);
        if (!DateOnly.TryParse(req.PeriodStart, out var start) || !DateOnly.TryParse(req.PeriodEnd, out var end) || end < start)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Invalid period", 400);
        if (req.DepartmentIds == null || req.DepartmentIds.Count == 0)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "At least one department is required", 400);

        var mode = string.IsNullOrWhiteSpace(req.PeerPresentationMode)
            ? PeerPresentationModes.Aggregated
            : req.PeerPresentationMode.ToUpperInvariant();
        if (mode is not (PeerPresentationModes.Attributed or PeerPresentationModes.Aggregated))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Invalid peer presentation mode", 400);

        var cutOff = DateOnly.TryParse(req.JoinCutOff, out var co) ? co : start;
        var deadlines = req.StageDeadlines ?? new Dictionary<string, string>();

        var cycle = new ReviewCycle
        {
            Name = req.Name.Trim(),
            PeriodStart = start,
            PeriodEnd = end,
            DepartmentIdsJson = JsonSerializer.Serialize(req.DepartmentIds.Distinct().ToList()),
            Stage = ReviewCycleStages.Draft,
            StageDeadlinesJson = JsonSerializer.Serialize(deadlines),
            PeerPresentationMode = mode,
            JoinCutOff = cutOff,
            ResponseWindowDays = req.ResponseWindowDays <= 0 ? 14 : req.ResponseWindowDays,
            CreatedByEmployeeId = actorEmployeeId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.AddCycleAsync(cycle);
        await _audit.LogAsync(PerformanceAuditEvents.ReviewCycleOpened, actorEmployeeId, $"employee:{actorEmployeeId}",
            "SUCCESS", "ReviewCycle", cycle.Id.ToString(), changes: new { cycle.Name, cycle.Stage });

        return MapCycle(cycle);
    }

    public async Task<object> AdvanceStageAsync(int actorEmployeeId, string actorRole, int cycleId, AdvanceStageRequest req)
    {
        DenyAdminAuditor(actorRole);
        RequireHrAdmin(actorRole);

        var cycle = await _repo.GetCycleAsync(cycleId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Cycle not found", 404);

        var next = ReviewCycleStages.Next(cycle.Stage)
            ?? throw new TaskDomainException(TaskErrorCodes.InvalidTransition, "Cycle is already closed", 409);

        if (cycle.Stage == ReviewCycleStages.Draft && next == ReviewCycleStages.SelfAssessment)
            await OpenCycleReviewsAsync(cycle);

        var from = cycle.Stage;
        cycle.Stage = next;
        cycle.StageChangedAt = DateTime.UtcNow;
        cycle.LastStageChangeReason = req.Reason;

        if (next == ReviewCycleStages.Published)
            await PublishReviewsAsync(cycle);

        await _repo.UpdateCycleAsync(cycle);
        await _audit.LogAsync(PerformanceAuditEvents.ReviewStageAdvanced, actorEmployeeId, $"employee:{actorEmployeeId}",
            "SUCCESS", "ReviewCycle", cycle.Id.ToString(),
            changes: new { from, to = next, reason = req.Reason });

        if (next == ReviewCycleStages.Published)
            await _audit.LogAsync(PerformanceAuditEvents.ReviewPublished, actorEmployeeId, $"employee:{actorEmployeeId}",
                "SUCCESS", "ReviewCycle", cycle.Id.ToString());

        return MapCycle(cycle);
    }

    public async Task<object> ReverseStageAsync(int actorEmployeeId, string actorRole, int cycleId, AdvanceStageRequest req)
    {
        DenyAdminAuditor(actorRole);
        RequireHrAdmin(actorRole);

        var cycle = await _repo.GetCycleAsync(cycleId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Cycle not found", 404);

        var previous = ReviewCycleStages.Previous(cycle.Stage)
            ?? throw new TaskDomainException(TaskErrorCodes.InvalidTransition, "Cycle is already at its first stage", 409);
        if (string.IsNullOrWhiteSpace(req.Reason))
            throw new TaskDomainException(TaskErrorCodes.ReasonRequired, "A reason is required to reverse a cycle stage", 422);

        var from = cycle.Stage;
        cycle.Stage = previous;
        cycle.StageChangedAt = DateTime.UtcNow;
        cycle.LastStageChangeReason = req.Reason;
        await _repo.UpdateCycleAsync(cycle);

        await _audit.LogAsync(PerformanceAuditEvents.ReviewStageReversed, actorEmployeeId, $"employee:{actorEmployeeId}",
            "SUCCESS", "ReviewCycle", cycle.Id.ToString(),
            changes: new { from, to = previous, reason = req.Reason });

        return MapCycle(cycle);
    }

    public async Task<object> GetProgressAsync(int actorEmployeeId, string actorRole, int cycleId)
    {
        DenyAdminAuditor(actorRole);
        if (!IsHrAdmin(actorRole) && !IsManagerRole(actorRole))
            throw Forbidden("Only HR_ADMIN or managers may view cycle progress");

        var cycle = await _repo.GetCycleAsync(cycleId, tracking: false)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Cycle not found", 404);

        var reviews = await _repo.GetReviewsForCycleAsync(cycleId);
        if (IsManagerRole(actorRole) && !IsHrAdmin(actorRole))
        {
            var reports = await _repo.GetReportIdsAsync(actorEmployeeId, includeIndirect: true);
            reviews = reviews.Where(r => reports.Contains(r.EmployeeId) || r.ReviewerEmployeeId == actorEmployeeId).ToList();
        }

        await AuditReadAsync(actorEmployeeId, "ReviewCycle", cycleId.ToString(), "progress");

        var byDept = reviews
            .GroupBy(r => r.Employee?.DepartmentId ?? 0)
            .Select(g => new
            {
                departmentId = g.Key,
                total = g.Count(),
                selfSubmitted = g.Count(x => x.SelfSubmitted),
                managerSubmitted = g.Count(x => x.ManagerSubmitted),
                calibrated = g.Count(x => x.CalibratedOverallRating != null)
            });

        var byReviewer = reviews
            .GroupBy(r => r.ReviewerEmployeeId)
            .Select(g => new
            {
                reviewerEmployeeId = g.Key,
                reviewerName = g.First().Reviewer?.FullName,
                total = g.Count(),
                managerSubmitted = g.Count(x => x.ManagerSubmitted)
            });

        return new { cycle = MapCycle(cycle), byDepartment = byDept, byReviewer };
    }

    public async Task<object> GetMyReviewsAsync(int actorEmployeeId, string actorRole)
    {
        DenyAdminAuditor(actorRole);
        var rows = await _repo.GetReviewsForEmployeeAsync(actorEmployeeId);
        await AuditReadAsync(actorEmployeeId, "PerformanceReview", "mine", "list");

        return rows.Select(r => new
        {
            r.Id,
            r.ReviewCycleId,
            cycleName = r.ReviewCycle?.Name,
            cycleStage = r.ReviewCycle?.Stage,
            r.EmployeeId,
            r.ReviewerEmployeeId,
            r.SelfSubmitted,
            r.ManagerSubmitted,
            published = IsPublishedOrLater(r.ReviewCycle?.Stage),
            overallRating = IsPublishedOrLater(r.ReviewCycle?.Stage)
                ? (r.CalibratedOverallRating ?? r.ManagerOverallRating)
                : (int?)null,
            r.CreatedAt
        }).ToList();
    }

    public async Task<object> GetReviewAsync(int actorEmployeeId, string actorRole, int reviewId)
    {
        DenyAdminAuditor(actorRole);
        var review = await _repo.GetReviewAsync(reviewId, tracking: false)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        var access = await ResolveAccessAsync(actorEmployeeId, actorRole, review);
        if (!access.CanRead)
        {
            await AuditReadAsync(actorEmployeeId, "PerformanceReview", reviewId.ToString(), "DENIED");
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Not found", 404);
        }

        await AuditReadAsync(actorEmployeeId, "PerformanceReview", reviewId.ToString(), "SUCCESS");
        var quality = access.CanSeeManager ? await BuildQualityIndicatorsAsync(review) : null;
        return ShapeReview(review, access, quality);
    }

    // PM-25/26: rework/reopen rate surfaced as evidence context, always explicitly labeled a
    // system-level indicator of how work flowed - never rendered as a performance score - and
    // suppressed below AN-12's sample-size floor so a thin sample doesn't masquerade as a signal.
    private const int QualityIndicatorMinSampleSize = 5;

    private async Task<object> BuildQualityIndicatorsAsync(PerformanceReview review)
    {
        var (n, rework, reopen) = await _repo.GetQualityIndicatorsAsync(
            review.EmployeeId, review.ReviewCycle.PeriodStart, review.ReviewCycle.PeriodEnd);

        if (n < QualityIndicatorMinSampleSize)
            return new
            {
                status = "insufficient_data",
                sampleSize = n,
                minimumSampleSize = QualityIndicatorMinSampleSize,
                label = "System-level indicator - not a performance score",
                caveat = "Too few completed tasks in this period to report reliably."
            };

        return new
        {
            status = "ok",
            sampleSize = n,
            period = new { start = review.ReviewCycle.PeriodStart, end = review.ReviewCycle.PeriodEnd },
            reworkRate = Math.Round((double)rework / n, 2),
            reopenRate = Math.Round((double)reopen / n, 2),
            label = "System-level indicator - not a performance score",
            caveat = "Reflects how work flowed through the system (e.g. unclear requirements), not the worth of the person. Requires interpretation, never presented as a rating."
        };
    }

    public async Task<object> SaveSelfAssessmentAsync(int actorEmployeeId, string actorRole, int reviewId, AssessmentRequest req)
    {
        DenyAdminAuditor(actorRole);
        var review = await _repo.GetReviewAsync(reviewId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        if (review.EmployeeId != actorEmployeeId)
            throw Forbidden("Only the employee may write the self-assessment");
        EnsureStageAllows(review.ReviewCycle.Stage, ReviewCycleStages.SelfAssessment, "Self-assessment");

        ValidateAssessment(req);
        review.SelfOverallRating = req.OverallRating;
        review.SelfJustification = req.Justification;
        review.SelfScoresJson = JsonSerializer.Serialize(req.Scores, JsonOpts);
        if (req.Submit)
        {
            RequireJustification(req.Justification);
            review.SelfSubmitted = true;
            review.SelfSubmittedAt = DateTime.UtcNow;
            await _audit.LogAsync(PerformanceAuditEvents.RatingSubmitted, actorEmployeeId, $"employee:{actorEmployeeId}",
                "SUCCESS", "PerformanceReview", review.Id.ToString(),
                changes: new { kind = "self", rating = req.OverallRating });
        }

        await _repo.UpdateReviewAsync(review);
        return ShapeReview(review, await ResolveAccessAsync(actorEmployeeId, actorRole, review));
    }

    public async Task<object> SaveManagerAssessmentAsync(int actorEmployeeId, string actorRole, int reviewId, AssessmentRequest req)
    {
        DenyAdminAuditor(actorRole);
        var review = await _repo.GetReviewAsync(reviewId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        if (review.ReviewerEmployeeId != actorEmployeeId && !IsHrAdmin(actorRole))
            throw Forbidden("Only the assigned reviewer (or HR_ADMIN) may write the manager assessment");
        if (review.EmployeeId == actorEmployeeId)
            throw Forbidden("A reviewer shall not review themselves");

        EnsureStageAllows(review.ReviewCycle.Stage, ReviewCycleStages.ManagerAssessment, "Manager assessment");

        var actor = await _repo.GetEmployeeAsync(actorEmployeeId);
        if (actor != null && actor.ManagerId == review.EmployeeId)
            throw Forbidden("Conflict of interest: cannot review your own manager");

        ValidateAssessment(req);
        review.ManagerOverallRating = req.OverallRating;
        review.ManagerJustification = req.Justification;
        review.ManagerScoresJson = JsonSerializer.Serialize(req.Scores, JsonOpts);
        if (req.Submit)
        {
            RequireJustification(req.Justification);
            review.ManagerSubmitted = true;
            review.ManagerSubmittedAt = DateTime.UtcNow;
            await _audit.LogAsync(PerformanceAuditEvents.RatingSubmitted, actorEmployeeId, $"employee:{actorEmployeeId}",
                "SUCCESS", "PerformanceReview", review.Id.ToString(),
                changes: new { kind = "manager", rating = req.OverallRating });
        }

        await _repo.UpdateReviewAsync(review);
        return ShapeReview(review, await ResolveAccessAsync(actorEmployeeId, actorRole, review));
    }

    public async Task<object> ReassignReviewerAsync(int actorEmployeeId, string actorRole, int reviewId, ReassignReviewerRequest req)
    {
        DenyAdminAuditor(actorRole);
        RequireHrAdmin(actorRole);

        var review = await _repo.GetReviewAsync(reviewId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        if (IsPublishedOrLater(review.ReviewCycle.Stage))
            throw Forbidden("Cannot reassign the reviewer of a published review");
        if (string.IsNullOrWhiteSpace(req.Reason))
            throw new TaskDomainException(TaskErrorCodes.ReasonRequired, "A reason is required to reassign a reviewer", 422);

        var newReviewer = await _repo.GetEmployeeAsync(req.NewReviewerEmployeeId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "New reviewer not found", 404);
        if (!newReviewer.IsActive)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "New reviewer must be an active employee", 422);
        if (req.NewReviewerEmployeeId == review.EmployeeId)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "The reviewer cannot be the employee being reviewed", 422);

        var previousReviewerId = review.ReviewerEmployeeId;
        review.ReviewerEmployeeId = req.NewReviewerEmployeeId;
        // The outgoing reviewer's not-yet-submitted assessment is discarded - it belongs to a reviewer
        // who is no longer assigned; a submitted one is left in place as a historical record.
        if (!review.ManagerSubmitted)
        {
            review.ManagerOverallRating = null;
            review.ManagerJustification = null;
            review.ManagerScoresJson = "[]";
        }
        await _repo.UpdateReviewAsync(review);

        await _audit.LogAsync(PerformanceAuditEvents.ReviewerReassigned, actorEmployeeId, $"employee:{actorEmployeeId}",
            "SUCCESS", "PerformanceReview", review.Id.ToString(),
            changes: new { from = previousReviewerId, to = req.NewReviewerEmployeeId, reason = req.Reason });

        return new { review.Id, previousReviewerId, review.ReviewerEmployeeId, reason = req.Reason.Trim() };
    }

    public async Task<object> AttachEvidenceAsync(int actorEmployeeId, string actorRole, int reviewId, AttachEvidenceRequest req)
    {
        DenyAdminAuditor(actorRole);
        var review = await _repo.GetReviewAsync(reviewId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        var access = await ResolveAccessAsync(actorEmployeeId, actorRole, review);
        if (!access.CanWriteEvidence)
            throw Forbidden("Not permitted to attach evidence");
        // PM-12: a published review is immutable except for the employee's response and an appended
        // appeal outcome - evidence is part of the record being frozen, so no role (including HR_ADMIN)
        // may add it after publication.
        if (IsPublishedOrLater(review.ReviewCycle.Stage))
            throw Forbidden("Cannot attach evidence after publication - the review is immutable");

        var type = (req.EvidenceType ?? ReviewEvidenceTypes.Note).ToUpperInvariant();
        if (type == ReviewEvidenceTypes.Task)
        {
            if (req.TaskId == null || !await _repo.TaskExistsAsync(req.TaskId.Value))
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "Valid taskId required", 400);
        }
        else if (type == ReviewEvidenceTypes.FeedbackNote)
        {
            if (req.FeedbackNoteId == null || await _repo.GetFeedbackNoteAsync(req.FeedbackNoteId.Value) == null)
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "Valid feedbackNoteId required", 400);
        }
        else if (string.IsNullOrWhiteSpace(req.Note))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Note is required", 400);

        var ev = await _repo.AddEvidenceAsync(new ReviewEvidence
        {
            PerformanceReviewId = reviewId,
            EvidenceType = type,
            TaskId = req.TaskId,
            FeedbackNoteId = req.FeedbackNoteId,
            Note = req.Note,
            AttachedByEmployeeId = actorEmployeeId
        });

        return new { ev.Id, ev.PerformanceReviewId, ev.EvidenceType, ev.TaskId, ev.FeedbackNoteId, ev.Note, ev.CreatedAt };
    }

    public async Task<object> AddResponseAsync(int actorEmployeeId, string actorRole, int reviewId, ReviewResponseRequest req)
    {
        DenyAdminAuditor(actorRole);
        var review = await _repo.GetReviewAsync(reviewId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        if (review.EmployeeId != actorEmployeeId)
            throw Forbidden("Only the employee may respond");
        if (!IsPublishedOrLater(review.ReviewCycle.Stage))
            throw Forbidden("Response is only allowed after publication");
        if (string.IsNullOrWhiteSpace(req.Response))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Response is required", 400);
        if (review.ResponseWindowEndsAt != null && DateTime.UtcNow > review.ResponseWindowEndsAt)
            throw Forbidden("Response window has closed");

        review.EmployeeResponse = req.Response.Trim();
        review.EmployeeResponseAt = DateTime.UtcNow;
        await _repo.UpdateReviewAsync(review);
        return new { review.Id, review.EmployeeResponse, review.EmployeeResponseAt };
    }

    public async Task<object> RaiseAppealAsync(int actorEmployeeId, string actorRole, int reviewId, AppealRequest req)
    {
        DenyAdminAuditor(actorRole);
        var review = await _repo.GetReviewAsync(reviewId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        if (review.EmployeeId != actorEmployeeId)
            throw Forbidden("Only the employee may appeal");
        if (review.ReviewCycle.Stage != ReviewCycleStages.Published && review.ReviewCycle.Stage != ReviewCycleStages.Closed)
            throw Forbidden("Appeals require a published review");
        if (string.IsNullOrWhiteSpace(req.Ground))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Ground is required", 400);

        var appeal = await _repo.AddAppealAsync(new ReviewAppeal
        {
            PerformanceReviewId = reviewId,
            Ground = req.Ground.Trim(),
            RaisedByEmployeeId = actorEmployeeId
        });

        await _audit.LogAsync(PerformanceAuditEvents.AppealRaised, actorEmployeeId, $"employee:{actorEmployeeId}",
            "SUCCESS", "ReviewAppeal", appeal.Id.ToString(), changes: new { reviewId });

        return new { appeal.Id, appeal.PerformanceReviewId, appeal.Ground, appeal.RaisedAt };
    }

    public async Task<object> DecideAppealAsync(int actorEmployeeId, string actorRole, int reviewId, int appealId, DecideAppealRequest req)
    {
        DenyAdminAuditor(actorRole);
        var review = await _repo.GetReviewAsync(reviewId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        var appeal = review.Appeals.FirstOrDefault(a => a.Id == appealId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Appeal not found", 404);

        // PM-34: the reviewing authority must be above the original reviewer - HR_ADMIN always qualifies;
        // otherwise the original reviewer's own manager may decide it. The original reviewer themselves
        // (and the employee being reviewed) may never decide their own appeal.
        var isHr = IsHrAdmin(actorRole);
        if (!isHr)
        {
            var originalReviewer = await _repo.GetEmployeeAsync(review.ReviewerEmployeeId);
            if (originalReviewer?.ManagerId != actorEmployeeId)
                throw Forbidden("Only HR_ADMIN or the original reviewer's manager may decide this appeal");
        }
        if (actorEmployeeId == appeal.RaisedByEmployeeId)
            throw Forbidden("Cannot decide your own appeal");

        if (appeal.Outcome != null)
            throw new TaskDomainException(TaskErrorCodes.InvalidTransition, "This appeal has already been decided", 409);
        if (!AppealOutcomes.IsValid(req.Outcome))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Outcome must be UPHELD, AMENDED, or DECLINED", 400);
        if (string.IsNullOrWhiteSpace(req.Reason))
            throw new TaskDomainException(TaskErrorCodes.ReasonRequired, "A reason is required to decide an appeal", 422);

        // Appended, never edits the original rating - only the appeal record itself is updated with
        // the decision; PreCalibrationOverallRating/CalibratedOverallRating are left untouched here.
        appeal.Outcome = req.Outcome.ToUpperInvariant();
        appeal.OutcomeReason = req.Reason.Trim();
        appeal.DecidedByEmployeeId = actorEmployeeId;
        appeal.DecidedAt = DateTime.UtcNow;
        await _repo.UpdateReviewAsync(review);

        await _audit.LogAsync(PerformanceAuditEvents.AppealDecided, actorEmployeeId, $"employee:{actorEmployeeId}",
            "SUCCESS", "ReviewAppeal", appeal.Id.ToString(),
            changes: new { reviewId, outcome = appeal.Outcome, reason = appeal.OutcomeReason });

        await _tasks.CreateNotificationAsync(
            review.EmployeeId,
            "APPEAL_DECIDED",
            $"Your appeal on review #{reviewId} was decided: {appeal.Outcome}.");

        return new { appeal.Id, appeal.PerformanceReviewId, appeal.Outcome, appeal.OutcomeReason, appeal.DecidedByEmployeeId, appeal.DecidedAt };
    }

    public async Task<object> NominatePeersAsync(int actorEmployeeId, string actorRole, int reviewId, PeerNominationRequest req)
    {
        DenyAdminAuditor(actorRole);
        var review = await _repo.GetReviewAsync(reviewId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        var isSubject = review.EmployeeId == actorEmployeeId;
        var isReviewer = review.ReviewerEmployeeId == actorEmployeeId;
        var isHr = IsHrAdmin(actorRole);
        if (!isSubject && !isReviewer && !isHr)
            throw Forbidden("Only the employee, their reviewer, or HR_ADMIN may nominate peers");
        if (IsPublishedOrLater(review.ReviewCycle.Stage))
            throw Forbidden("Cannot nominate peers after publication");

        var created = new List<object>();

        if ((isReviewer || isHr) && req.ApproveInvitationIds is { Count: > 0 })
        {
            foreach (var invId in req.ApproveInvitationIds)
            {
                var inv = review.PeerInvitations.FirstOrDefault(p => p.Id == invId);
                if (inv == null || inv.Status != PeerInvitationStatuses.Pending) continue;
                inv.Status = PeerInvitationStatuses.Issued;
                inv.Token = Guid.NewGuid().ToString("N");
                inv.IssuedAt = DateTime.UtcNow;
                await _repo.UpdatePeerInvitationAsync(inv);
                created.Add(new { inv.Id, inv.NomineeEmployeeId, inv.Status, inv.Token });
            }
        }

        foreach (var nomineeId in req.NomineeEmployeeIds.Distinct())
        {
            if (nomineeId == review.EmployeeId || nomineeId == review.ReviewerEmployeeId) continue;
            if (review.PeerInvitations.Any(p => p.NomineeEmployeeId == nomineeId)) continue;

            var issueNow = isReviewer || isHr;
            var inv = new PeerInvitation
            {
                PerformanceReviewId = reviewId,
                NomineeEmployeeId = nomineeId,
                NominatedByEmployeeId = actorEmployeeId,
                Status = issueNow ? PeerInvitationStatuses.Issued : PeerInvitationStatuses.Pending,
                Token = issueNow ? Guid.NewGuid().ToString("N") : null,
                IssuedAt = issueNow ? DateTime.UtcNow : null
            };
            await _repo.AddPeerInvitationAsync(inv);
            created.Add(new { inv.Id, inv.NomineeEmployeeId, inv.Status, token = inv.Token });
        }

        return new { invitations = created };
    }

    public async Task<object> SubmitPeerFeedbackAsync(string token, PeerFeedbackRequest req)
    {
        var inv = await _repo.GetPeerInvitationByTokenAsync(token)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Invitation not found", 404);

        if (inv.Status != PeerInvitationStatuses.Issued)
            throw new TaskDomainException(TaskErrorCodes.InvalidTransition, "Invitation is not open for feedback", 409);
        if (IsPublishedOrLater(inv.PerformanceReview.ReviewCycle.Stage))
            throw Forbidden("Cycle no longer accepts peer feedback");

        ValidateAssessment(new AssessmentRequest
        {
            OverallRating = req.OverallRating,
            Justification = req.Justification,
            Scores = req.Scores,
            Submit = true
        });
        RequireJustification(req.Justification);

        var fb = await _repo.AddPeerFeedbackAsync(new PeerFeedback
        {
            PeerInvitationId = inv.Id,
            OverallRating = req.OverallRating,
            Justification = req.Justification.Trim(),
            ScoresJson = JsonSerializer.Serialize(req.Scores, JsonOpts)
        });

        inv.Status = PeerInvitationStatuses.Submitted;
        await _repo.UpdatePeerInvitationAsync(inv);

        await _audit.LogAsync(PerformanceAuditEvents.PeerFeedbackSubmitted, inv.NomineeEmployeeId, $"employee:{inv.NomineeEmployeeId}",
            "SUCCESS", "PeerFeedback", fb.Id.ToString(),
            changes: new { reviewId = inv.PerformanceReviewId });

        return new { fb.Id, inv.PerformanceReviewId, fb.SubmittedAt };
    }

    public async Task<object> GetCalibrationAsync(int actorEmployeeId, string actorRole, int cycleId, int? departmentId)
    {
        DenyAdminAuditor(actorRole);
        if (!IsHrAdmin(actorRole) && !IsManagerRole(actorRole))
            throw Forbidden("Calibration view requires HR_ADMIN or Manager");

        var cycle = await _repo.GetCycleAsync(cycleId, tracking: false)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Cycle not found", 404);

        if (cycle.Stage != ReviewCycleStages.Calibration && !IsHrAdmin(actorRole)
            && cycle.Stage != ReviewCycleStages.Published && cycle.Stage != ReviewCycleStages.Closed)
            throw Forbidden("Calibration is only available during the calibration stage");

        var reviews = await _repo.GetReviewsForCycleAsync(cycleId);
        if (IsManagerRole(actorRole) && !IsHrAdmin(actorRole))
        {
            var reports = await _repo.GetReportIdsAsync(actorEmployeeId, includeIndirect: true);
            reviews = reviews.Where(r => reports.Contains(r.EmployeeId) || r.ReviewerEmployeeId == actorEmployeeId).ToList();
        }
        if (departmentId is int d)
            reviews = reviews.Where(r => r.Employee?.DepartmentId == d).ToList();

        await AuditReadAsync(actorEmployeeId, "ReviewCycle", cycleId.ToString(), "calibration");

        return new
        {
            cycleId,
            stage = cycle.Stage,
            rows = reviews.Select(r => new
            {
                r.Id,
                r.EmployeeId,
                employeeName = r.Employee?.FullName,
                departmentId = r.Employee?.DepartmentId,
                r.ReviewerEmployeeId,
                reviewerName = r.Reviewer?.FullName,
                managerOverallRating = r.ManagerOverallRating,
                calibratedOverallRating = r.CalibratedOverallRating,
                preCalibrationOverallRating = r.PreCalibrationOverallRating,
                r.ManagerSubmitted,
                r.CalibrationReason
            })
        };
    }

    public async Task<object> CalibrateRatingAsync(int actorEmployeeId, string actorRole, int reviewId, CalibrateRequest req)
    {
        DenyAdminAuditor(actorRole);
        if (!IsHrAdmin(actorRole) && !IsManagerRole(actorRole))
            throw Forbidden("Calibration requires HR_ADMIN or Manager");

        var review = await _repo.GetReviewAsync(reviewId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Review not found", 404);

        if (review.ReviewCycle.Stage != ReviewCycleStages.Calibration)
            throw Forbidden("Calibration changes only allowed during CALIBRATION stage");

        if (!IsHrAdmin(actorRole))
        {
            var reports = await _repo.GetReportIdsAsync(actorEmployeeId, includeIndirect: true);
            if (!reports.Contains(review.EmployeeId) && review.ReviewerEmployeeId != actorEmployeeId)
                throw Forbidden("Out of calibration scope");
        }

        if (req.OverallRating is < RatingScale.Min or > RatingScale.Max)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Invalid rating", 400);
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Trim().Length < 10)
            throw new TaskDomainException(TaskErrorCodes.ReasonRequired, "Calibration reason is required", 422);

        if (review.PreCalibrationOverallRating == null)
            review.PreCalibrationOverallRating = review.ManagerOverallRating;

        review.CalibratedOverallRating = req.OverallRating;
        review.CalibrationReason = req.Reason.Trim();
        review.CalibratedAt = DateTime.UtcNow;
        review.CalibratedByEmployeeId = actorEmployeeId;
        await _repo.UpdateReviewAsync(review);

        await _audit.LogAsync(PerformanceAuditEvents.RatingCalibrated, actorEmployeeId, $"employee:{actorEmployeeId}",
            "SUCCESS", "PerformanceReview", review.Id.ToString(),
            changes: new
            {
                pre = review.PreCalibrationOverallRating,
                post = review.CalibratedOverallRating,
                reason = review.CalibrationReason
            });

        return new
        {
            review.Id,
            review.PreCalibrationOverallRating,
            review.CalibratedOverallRating,
            review.CalibrationReason,
            review.CalibratedAt
        };
    }

    public async Task<object> CreateGoalAsync(int actorEmployeeId, string actorRole, CreateGoalRequest req)
    {
        DenyAdminAuditor(actorRole);
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Title is required", 400);
        if (!DateOnly.TryParse(req.PeriodStart, out var start) || !DateOnly.TryParse(req.PeriodEnd, out var end) || end < start)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Invalid period", 400);

        var owner = await _repo.GetEmployeeAsync(req.OwnerEmployeeId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Owner not found", 404);

        var managerId = req.ManagerEmployeeId ?? owner.ManagerId
            ?? throw new TaskDomainException(TaskErrorCodes.ValidationError, "Manager is required", 400);

        if (actorEmployeeId != req.OwnerEmployeeId && actorEmployeeId != managerId && !IsHrAdmin(actorRole))
            throw Forbidden("Only owner, manager, or HR_ADMIN may create a goal");

        var goal = new Goal
        {
            Title = req.Title.Trim(),
            Description = req.Description?.Trim() ?? "",
            OwnerEmployeeId = req.OwnerEmployeeId,
            ManagerEmployeeId = managerId,
            PeriodStart = start,
            PeriodEnd = end,
            MeasureOfSuccess = req.MeasureOfSuccess?.Trim() ?? "",
            TargetValue = req.TargetValue,
            CurrentValue = req.CurrentValue,
            Weight = req.Weight,
            State = GoalStates.Draft,
            CreatedByEmployeeId = actorEmployeeId
        };

        if (req.TaskIds != null)
        {
            foreach (var tid in req.TaskIds.Distinct())
            {
                if (await _repo.TaskExistsAsync(tid))
                    goal.TaskLinks.Add(new GoalTaskLink { TaskId = tid });
            }
        }

        await _repo.AddGoalAsync(goal);
        return await ShapeGoalAsync(goal);
    }

    public async Task<object> AcknowledgeGoalAsync(int actorEmployeeId, string actorRole, int goalId)
    {
        DenyAdminAuditor(actorRole);
        var goal = await _repo.GetGoalAsync(goalId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Goal not found", 404);

        if (actorEmployeeId == goal.OwnerEmployeeId)
        {
            goal.OwnerAcknowledged = true;
            goal.OwnerAcknowledgedAt = DateTime.UtcNow;
        }
        else if (actorEmployeeId == goal.ManagerEmployeeId || IsHrAdmin(actorRole))
        {
            goal.ManagerAcknowledged = true;
            goal.ManagerAcknowledgedAt = DateTime.UtcNow;
        }
        else
            throw Forbidden("Only owner or manager may acknowledge");

        if (goal.OwnerAcknowledged && goal.ManagerAcknowledged && goal.State == GoalStates.Draft)
            goal.State = GoalStates.Active;

        await _repo.UpdateGoalAsync(goal);
        return await ShapeGoalAsync(goal);
    }

    public async Task<object> UpdateGoalProgressAsync(int actorEmployeeId, string actorRole, int goalId, GoalProgressRequest req)
    {
        DenyAdminAuditor(actorRole);
        var goal = await _repo.GetGoalAsync(goalId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Goal not found", 404);

        if (actorEmployeeId != goal.OwnerEmployeeId && actorEmployeeId != goal.ManagerEmployeeId && !IsHrAdmin(actorRole))
            throw Forbidden("Not permitted to update this goal");

        var snapshot = JsonSerializer.Serialize(new
        {
            goal.Title, goal.Description, goal.MeasureOfSuccess,
            goal.TargetValue, goal.CurrentValue, goal.Weight, goal.State
        }, JsonOpts);
        await _repo.AddGoalVersionAsync(new GoalVersion
        {
            GoalId = goal.Id,
            SnapshotJson = snapshot,
            ChangedByEmployeeId = actorEmployeeId,
            ChangeReason = req.ChangeReason
        });

        goal.CurrentValue = req.CurrentValue;
        if (!string.IsNullOrWhiteSpace(req.State)) goal.State = req.State.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(req.Title)) goal.Title = req.Title.Trim();
        if (req.Description != null) goal.Description = req.Description;
        if (req.MeasureOfSuccess != null) goal.MeasureOfSuccess = req.MeasureOfSuccess;
        if (req.TargetValue != null) goal.TargetValue = req.TargetValue.Value;
        if (req.Weight != null) goal.Weight = req.Weight.Value;

        await _repo.UpdateGoalAsync(goal);
        return await ShapeGoalAsync(goal);
    }

    public async Task<object> CreateFeedbackNoteAsync(int actorEmployeeId, string actorRole, CreateFeedbackNoteRequest req)
    {
        DenyAdminAuditor(actorRole);
        if (string.IsNullOrWhiteSpace(req.Body))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Body is required", 400);
        if (req.ToEmployeeId == actorEmployeeId)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Cannot leave a note to yourself", 400);

        var to = await _repo.GetEmployeeAsync(req.ToEmployeeId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Recipient not found", 404);

        var reports = await _repo.GetReportIdsAsync(actorEmployeeId, includeIndirect: true);
        var actor = await _repo.GetEmployeeAsync(actorEmployeeId);
        var ok = IsHrAdmin(actorRole)
                 || reports.Contains(req.ToEmployeeId)
                 || actor?.ManagerId == req.ToEmployeeId;
        if (!ok)
            throw Forbidden("Feedback notes are only between manager and employee (or HR_ADMIN)");

        var note = await _repo.AddFeedbackNoteAsync(new FeedbackNote
        {
            FromEmployeeId = actorEmployeeId,
            ToEmployeeId = req.ToEmployeeId,
            Body = req.Body.Trim()
        });

        return new { note.Id, note.FromEmployeeId, note.ToEmployeeId, note.Body, note.CreatedAt, toName = to.FullName };
    }

    public async Task<object> GetCompetenciesAsync()
    {
        var list = await _repo.GetCompetenciesAsync();
        return new
        {
            ratingScale = RatingScale.Descriptors.Select(d => new { value = d.Value, label = d.Label, descriptor = d.Descriptor }),
            competencies = list.Select(c => new
            {
                c.Id, c.Name, c.Description, c.Category, c.MinSeniorityRank,
                levelDescriptors = c.LevelDescriptors.Select(d => new
                {
                    d.SeniorityLevelId,
                    levelName = d.SeniorityLevel?.Name,
                    rank = d.SeniorityLevel?.Rank,
                    d.Descriptor
                })
            })
        };
    }

    public async Task<DateTime?> GetReviewUpdatedAtAsync(int reviewId)
    {
        var review = await _repo.GetReviewAsync(reviewId, tracking: false);
        return review?.UpdatedAt;
    }

    public async Task<object?> GetGoalAsync(int actorEmployeeId, string actorRole, int goalId)
    {
        DenyAdminAuditor(actorRole);
        var goal = await _repo.GetGoalAsync(goalId, tracking: false);
        if (goal == null) return null;

        // Same visibility as create/update: owner, their manager, or HR_ADMIN.
        if (actorEmployeeId != goal.OwnerEmployeeId
            && actorEmployeeId != goal.ManagerEmployeeId
            && !IsHrAdmin(actorRole))
            throw Forbidden("Not permitted to read this goal");

        return await ShapeGoalAsync(goal);
    }

    public async Task<DateTime?> GetGoalUpdatedAtAsync(int goalId)
    {
        var goal = await _repo.GetGoalAsync(goalId, tracking: false);
        return goal?.UpdatedAt;
    }

    private async Task OpenCycleReviewsAsync(ReviewCycle cycle)
    {
        var deptIds = JsonSerializer.Deserialize<List<int>>(cycle.DepartmentIdsJson) ?? new List<int>();
        var employees = await _repo.GetActiveEmployeesInDepartmentsAsync(deptIds);
        var skips = new List<object>();
        var reviews = new List<PerformanceReview>();

        foreach (var emp in employees)
        {
            if (emp.JoinedAt >= cycle.JoinCutOff)
            {
                skips.Add(new { employeeId = emp.Id, reason = "Joined on or after cut-off" });
                continue;
            }

            var reviewerId = emp.ManagerId;
            if (reviewerId == null || reviewerId == emp.Id)
            {
                skips.Add(new { employeeId = emp.Id, reason = "No eligible reviewer in reporting line" });
                continue;
            }

            reviews.Add(new PerformanceReview
            {
                ReviewCycleId = cycle.Id,
                EmployeeId = emp.Id,
                ReviewerEmployeeId = reviewerId.Value
            });
        }

        cycle.SkipLogJson = JsonSerializer.Serialize(skips);
        if (reviews.Count > 0)
            await _repo.AddReviewsAsync(reviews);
    }

    private async Task PublishReviewsAsync(ReviewCycle cycle)
    {
        var reviews = await _repo.GetReviewsForCycleAsync(cycle.Id);
        var ends = DateTime.UtcNow.AddDays(cycle.ResponseWindowDays);
        foreach (var r in reviews)
        {
            var tracked = await _repo.GetReviewAsync(r.Id);
            if (tracked == null) continue;
            if (tracked.CalibratedOverallRating == null && tracked.ManagerOverallRating != null)
                tracked.CalibratedOverallRating = tracked.ManagerOverallRating;
            tracked.ResponseWindowEndsAt = ends;
            await _repo.UpdateReviewAsync(tracked);

            await _tasks.CreateNotificationAsync(
                tracked.EmployeeId,
                "REVIEW_PUBLISHED",
                $"Your performance review for '{cycle.Name}' has been published. You may add a written response.");
        }
    }

    private async Task<ReviewAccess> ResolveAccessAsync(int actorId, string role, PerformanceReview review)
    {
        var access = new ReviewAccess();
        var stage = review.ReviewCycle?.Stage ?? ReviewCycleStages.Draft;
        var published = IsPublishedOrLater(stage);

        if (IsHrAdmin(role))
        {
            access.CanRead = true;
            access.CanSeeSelf = true;
            access.CanSeeManager = true;
            access.CanSeePeers = true;
            access.PeersAttributed = true;
            access.CanSeeCalibration = true;
            access.CanWriteEvidence = true;
            return access;
        }

        if (review.EmployeeId == actorId)
        {
            access.CanRead = true;
            access.CanSeeSelf = true;
            access.CanSeeManager = published;
            access.CanSeePeers = published;
            access.PeersAttributed = false;
            access.CanSeeCalibration = published;
            access.CanWriteEvidence = !published;
            return access;
        }

        if (review.ReviewerEmployeeId == actorId)
        {
            access.CanRead = true;
            access.CanSeeSelf = review.SelfSubmitted;
            access.CanSeeManager = true;
            access.CanSeePeers = true;
            access.PeersAttributed = true;
            access.CanSeeCalibration = true;
            access.CanWriteEvidence = !published;
            return access;
        }

        if (IsManagerRole(role))
        {
            var reports = await _repo.GetReportIdsAsync(actorId, includeIndirect: true);
            // PM-37: a manager may read reports' reviews only for cycles they participated in as a
            // reviewer - not every published review in their reporting chain regardless of who ran it.
            var participated = await _repo.HasReviewedInCycleAsync(actorId, review.ReviewCycleId);
            if (reports.Contains(review.EmployeeId) && participated)
            {
                access.CanRead = published;
                access.CanSeeSelf = published && review.SelfSubmitted;
                access.CanSeeManager = published;
                access.CanSeePeers = published;
                access.PeersAttributed = true;
                access.CanSeeCalibration = published;
                access.CanWriteEvidence = false;
            }
        }

        return access;
    }

    private static object ShapeReview(PerformanceReview review, ReviewAccess access, object? qualityIndicators = null)
    {
        var stage = review.ReviewCycle?.Stage ?? "";
        var published = IsPublishedOrLater(stage);
        var mode = review.ReviewCycle?.PeerPresentationMode ?? PeerPresentationModes.Aggregated;

        object? peers = null;
        var submitted = review.PeerInvitations?
            .Where(p => p.Status == PeerInvitationStatuses.Submitted && p.Feedback != null)
            .ToList() ?? new List<PeerInvitation>();

        if (access.CanSeePeers)
        {
            if (access.PeersAttributed || mode == PeerPresentationModes.Attributed)
            {
                peers = submitted.Select(p => new
                {
                    p.NomineeEmployeeId,
                    nomineeName = p.Nominee?.FullName,
                    overallRating = p.Feedback!.OverallRating,
                    justification = p.Feedback.Justification,
                    attributed = true
                });
            }
            else if (submitted.Count < 3)
            {
                peers = new { withheld = true, reason = "Fewer than 3 peer respondents", n = submitted.Count };
            }
            else
            {
                peers = new
                {
                    attributed = false,
                    n = submitted.Count,
                    averageOverall = submitted.Average(p => p.Feedback!.OverallRating),
                    comments = submitted.Select(p => p.Feedback!.Justification)
                };
            }
        }

        return new
        {
            review.Id,
            review.ReviewCycleId,
            cycleName = review.ReviewCycle?.Name,
            cycleStage = stage,
            peerPresentationMode = mode,
            review.EmployeeId,
            employeeName = review.Employee?.FullName,
            review.ReviewerEmployeeId,
            reviewerName = review.Reviewer?.FullName,
            selfAssessment = access.CanSeeSelf ? new
            {
                overallRating = review.SelfOverallRating,
                justification = review.SelfJustification,
                submitted = review.SelfSubmitted,
                submittedAt = review.SelfSubmittedAt,
                scores = DeserializeScores(review.SelfScoresJson)
            } : null,
            managerAssessment = access.CanSeeManager ? new
            {
                overallRating = review.ManagerOverallRating,
                justification = review.ManagerJustification,
                submitted = review.ManagerSubmitted,
                submittedAt = review.ManagerSubmittedAt,
                scores = DeserializeScores(review.ManagerScoresJson)
            } : null,
            calibration = access.CanSeeCalibration ? new
            {
                preCalibrationOverallRating = review.PreCalibrationOverallRating,
                calibratedOverallRating = review.CalibratedOverallRating,
                reason = review.CalibrationReason,
                calibratedAt = review.CalibratedAt
            } : null,
            overallRating = published ? (review.CalibratedOverallRating ?? review.ManagerOverallRating) : null,
            evidence = review.Evidence?.Select(e => new { e.Id, e.EvidenceType, e.TaskId, e.FeedbackNoteId, e.Note, e.CreatedAt }),
            appeals = published ? review.Appeals?.Select(a => new { a.Id, a.Ground, a.RaisedAt, a.Outcome }) : null,
            employeeResponse = published ? review.EmployeeResponse : null,
            review.ResponseWindowEndsAt,
            peerFeedback = peers,
            qualityIndicators,
            metricsCaveat = "Any metrics shown as evidence measure the flow of work and not the worth of a person."
        };
    }

    private static object? DeserializeScores(string json)
    {
        try { return JsonSerializer.Deserialize<List<AssessmentScoreDto>>(json, JsonOpts); }
        catch { return Array.Empty<object>(); }
    }

    private static object MapCycle(ReviewCycle c) => new
    {
        c.Id, c.Name, c.PeriodStart, c.PeriodEnd, c.Stage,
        departmentIds = JsonSerializer.Deserialize<List<int>>(c.DepartmentIdsJson),
        stageDeadlines = JsonSerializer.Deserialize<Dictionary<string, string>>(c.StageDeadlinesJson),
        c.PeerPresentationMode, c.JoinCutOff, c.ResponseWindowDays,
        skipLog = JsonSerializer.Deserialize<object>(c.SkipLogJson),
        c.CreatedAt, c.StageChangedAt, c.LastStageChangeReason
    };

    // PM-29: aggregate progress of linked tasks is shown as context alongside a goal, never used to
    // automatically determine the goal's own outcome (State stays a deliberate, separate decision).
    private async Task<object> ShapeGoalAsync(Goal g)
    {
        var taskIds = g.TaskLinks?.Select(t => t.TaskId).ToList() ?? new List<int>();
        object? taskProgress = null;
        if (taskIds.Count > 0)
        {
            var statuses = await _repo.GetTaskStatusesAsync(taskIds);
            var done = statuses.Values.Count(s => s == "DONE");
            taskProgress = new
            {
                totalLinkedTasks = taskIds.Count,
                completedLinkedTasks = done,
                percentComplete = Math.Round(100.0 * done / taskIds.Count, 1),
                note = "Context only - does not automatically determine this goal's outcome."
            };
        }

        return new
        {
            g.Id, g.Title, g.Description, g.OwnerEmployeeId, g.ManagerEmployeeId,
            g.PeriodStart, g.PeriodEnd, g.MeasureOfSuccess, g.TargetValue, g.CurrentValue,
            g.Weight, g.State, g.OwnerAcknowledged, g.ManagerAcknowledged,
            taskIds,
            taskProgress,
            g.CreatedAt, g.UpdatedAt
        };
    }

    private static void ValidateAssessment(AssessmentRequest req)
    {
        if (req.OverallRating is < RatingScale.Min or > RatingScale.Max)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Overall rating must be 1-5", 400);
        foreach (var s in req.Scores)
        {
            if (s.Score is < RatingScale.Min or > RatingScale.Max)
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "Competency score must be 1-5", 400);
        }
    }

    private static void RequireJustification(string? text)
    {
        if ((text?.Trim().Length ?? 0) < RatingScale.MinJustificationLength)
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                $"Justification must be at least {RatingScale.MinJustificationLength} characters", 422);
    }

    private static void EnsureStageAllows(string current, string required, string action)
    {
        if (IsPublishedOrLater(current))
            throw Forbidden($"{action} is closed after publication");
        if (current != required)
            throw Forbidden($"{action} is only allowed during {required}");
    }

    private static bool IsPublishedOrLater(string? stage) =>
        stage is ReviewCycleStages.Published or ReviewCycleStages.Closed;

    private async Task AuditReadAsync(int actorId, string targetType, string targetId, string outcome) =>
        await _audit.LogAsync(PerformanceAuditEvents.ReviewRead, actorId, $"employee:{actorId}",
            outcome, targetType, targetId);

    private static void DenyAdminAuditor(string role)
    {
        if (role is User.Roles.Admin or User.Roles.Auditor)
            throw Forbidden("Technical Admin/Auditor roles cannot access performance data (PM-38)");
    }

    private static void RequireHrAdmin(string role)
    {
        if (!IsHrAdmin(role))
            throw Forbidden("HR_ADMIN only");
    }

    private static bool IsHrAdmin(string role) => role == User.Roles.HrAdmin;
    private static bool IsManagerRole(string role) => role == User.Roles.Manager;

    private static TaskDomainException Forbidden(string message) =>
        new(TaskErrorCodes.Forbidden, message, 403);

    private sealed class ReviewAccess
    {
        public bool CanRead { get; set; }
        public bool CanSeeSelf { get; set; }
        public bool CanSeeManager { get; set; }
        public bool CanSeePeers { get; set; }
        public bool PeersAttributed { get; set; }
        public bool CanSeeCalibration { get; set; }
        public bool CanWriteEvidence { get; set; }
    }
}

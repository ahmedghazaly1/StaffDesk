using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Constants;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Services;

public class GovernanceService : IGovernanceService
{
    private readonly IGovernanceRepository _governance;
    private readonly IAuditRepository _auditRepository;
    private readonly IAuditService _auditService;
    private readonly IJobRepository _jobRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly AppDbContext _db;
    private readonly string _exportRoot;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public GovernanceService(
        IGovernanceRepository governance,
        IAuditRepository auditRepository,
        IAuditService auditService,
        IJobRepository jobRepository,
        IEmployeeRepository employeeRepository,
        AppDbContext db)
    {
        _governance = governance;
        _auditRepository = auditRepository;
        _auditService = auditService;
        _jobRepository = jobRepository;
        _employeeRepository = employeeRepository;
        _db = db;
        _exportRoot = Path.Combine(AppContext.BaseDirectory, "exports");
        Directory.CreateDirectory(_exportRoot);
    }

    public Task<IReadOnlyList<RetentionPolicy>> GetRetentionPoliciesAsync() =>
        _governance.GetRetentionPoliciesAsync();

    public async Task<LegalHold> PlaceLegalHoldAsync(string targetType, string targetId, string reason, int actorEmployeeId)
    {
        if (targetType is not (LegalHoldTargetTypes.Employee or LegalHoldTargetTypes.Task))
            throw new ArgumentException("targetType must be Employee or Task");
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("targetId is required");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new ArgumentException("reason must be at least 5 characters");

        if (await _governance.HasActiveHoldAsync(targetType, targetId))
            throw new InvalidOperationException("An active legal hold already exists for this target");

        var hold = await _governance.PlaceHoldAsync(new LegalHold
        {
            TargetType = targetType,
            TargetId = targetId.Trim(),
            Reason = reason.Trim(),
            PlacedById = actorEmployeeId,
            PlacedAt = DateTime.UtcNow
        });

        await _auditService.LogAsync(
            "LEGAL_HOLD_PLACED",
            actorEmployeeId,
            $"employee:{actorEmployeeId}",
            "SUCCESS",
            targetType,
            targetId,
            changes: new { hold.Id, hold.Reason });

        return hold;
    }

    public async Task<LegalHold> LiftLegalHoldAsync(long holdId, int actorEmployeeId)
    {
        var existing = await _governance.GetHoldByIdAsync(holdId)
            ?? throw new KeyNotFoundException("Legal hold not found");
        if (existing.LiftedAt != null)
            throw new InvalidOperationException("Legal hold is already lifted");

        var hold = await _governance.LiftHoldAsync(holdId, actorEmployeeId)
            ?? throw new KeyNotFoundException("Legal hold not found");

        await _auditService.LogAsync(
            "LEGAL_HOLD_LIFTED",
            actorEmployeeId,
            $"employee:{actorEmployeeId}",
            "SUCCESS",
            hold.TargetType,
            hold.TargetId,
            changes: new { hold.Id });

        return hold;
    }

    public Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync() =>
        _governance.GetActiveHoldsAsync();

    public async Task<DataExport> QueueAuditExportAsync(
        int actorEmployeeId,
        string? eventType,
        string? outcome,
        int? actorId,
        string? targetType,
        string? targetId,
        DateTime? fromDate,
        DateTime? toDate,
        string? sourceIp)
    {
        var filter = new { eventType, outcome, actorId, targetType, targetId, fromDate, toDate, sourceIp };

        var export = await _governance.CreateExportAsync(new DataExport
        {
            Type = DataExportTypes.Audit,
            State = DataExportStates.Queued,
            RequestedByEmployeeId = actorEmployeeId,
            FilterJson = JsonSerializer.Serialize(filter, JsonOpts),
            CreatedAt = DateTime.UtcNow
        });

        var job = await _jobRepository.EnqueueAsync(
            JobTypes.AuditExport,
            JsonSerializer.Serialize(new { ExportId = export.Id }));

        export.JobId = job.Id;
        await _governance.UpdateExportAsync(export);

        await _auditService.LogAsync(
            "EXPORT_REQUESTED",
            actorEmployeeId,
            $"employee:{actorEmployeeId}",
            "SUCCESS",
            "DataExport",
            export.Id.ToString(),
            changes: new { export.Type, filter });

        return (await _governance.GetExportByIdAsync(export.Id))!;
    }

    public Task<DataExport?> GetAuditExportAsync(long id) =>
        _governance.GetExportByIdAsync(id);

    public async Task<DataExport> QueueSubjectAccessExportAsync(int subjectEmployeeId, int actorEmployeeId)
    {
        _ = await _employeeRepository.GetByIdAsync(subjectEmployeeId)
            ?? throw new KeyNotFoundException("Employee not found");

        var export = await _governance.CreateExportAsync(new DataExport
        {
            Type = DataExportTypes.SubjectAccess,
            State = DataExportStates.Queued,
            RequestedByEmployeeId = actorEmployeeId,
            SubjectEmployeeId = subjectEmployeeId,
            FilterJson = JsonSerializer.Serialize(new { subjectEmployeeId }, JsonOpts),
            CreatedAt = DateTime.UtcNow
        });

        var job = await _jobRepository.EnqueueAsync(
            JobTypes.SubjectAccessExport,
            JsonSerializer.Serialize(new { ExportId = export.Id }));

        export.JobId = job.Id;
        await _governance.UpdateExportAsync(export);

        await _auditService.LogAsync(
            "SUBJECT_ACCESS_EXPORTED",
            actorEmployeeId,
            $"employee:{actorEmployeeId}",
            "SUCCESS",
            "Employee",
            subjectEmployeeId.ToString(),
            changes: new { exportId = export.Id, queued = true });

        return (await _governance.GetExportByIdAsync(export.Id))!;
    }

    public async Task<(long JobId, long PurgeRunId)> QueuePurgeAsync(int actorEmployeeId, bool dryRun)
    {
        var run = await _governance.CreatePurgeRunAsync(new PurgeRun
        {
            DryRun = dryRun,
            State = "QUEUED",
            RequestedByEmployeeId = actorEmployeeId,
            CreatedAt = DateTime.UtcNow
        });

        var job = await _jobRepository.EnqueueAsync(
            JobTypes.Purge,
            JsonSerializer.Serialize(new { PurgeRunId = run.Id }));

        run.JobId = job.Id;
        await _governance.UpdatePurgeRunAsync(run);
        return (job.Id, run.Id);
    }

    public Task<PurgeRun?> GetPurgeRunAsync(long id) =>
        _governance.GetPurgeRunByIdAsync(id);

    public async Task<(long JobId, string Message)> QueueErasureAsync(int subjectEmployeeId, int actorEmployeeId)
    {
        if (await _governance.HasActiveHoldAsync(LegalHoldTargetTypes.Employee, subjectEmployeeId.ToString()))
            throw new InvalidOperationException("Erasure refused: an active legal hold is in force for this employee (DG-9)");

        var subject = await _employeeRepository.GetByIdAsync(subjectEmployeeId)
            ?? throw new KeyNotFoundException("Employee not found");

        if (subject.IsErased)
            return (0, "Employee already erased");

        var job = await _jobRepository.EnqueueAsync(
            JobTypes.Erasure,
            JsonSerializer.Serialize(new { SubjectEmployeeId = subjectEmployeeId, ActorEmployeeId = actorEmployeeId }));

        return (job.Id, "Erasure queued");
    }

    public async Task ProcessAuditExportAsync(long exportId)
    {
        var export = await _db.DataExports.FirstOrDefaultAsync(e => e.Id == exportId)
            ?? throw new InvalidOperationException($"Export {exportId} not found");

        export.State = DataExportStates.Running;
        await _db.SaveChangesAsync();

        try
        {
            using var filterDoc = JsonDocument.Parse(export.FilterJson);
            var root = filterDoc.RootElement;
            string? GetStr(string name) =>
                root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            int? GetInt(string name) =>
                root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
            DateTime? GetDt(string name) =>
                root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                    && DateTime.TryParse(p.GetString(), out var dt) ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : null;

            var events = await _auditRepository.GetAllMatchingAsync(
                GetStr("eventType"), GetStr("outcome"), GetInt("actorId"),
                GetStr("targetType"), GetStr("targetId"), GetDt("fromDate"), GetDt("toDate"), GetStr("sourceIp"));

            var fileName = $"audit-export-{exportId}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            var path = Path.Combine(_exportRoot, fileName);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(events.Select(MapAudit), JsonOpts), Encoding.UTF8);

            export.FilePath = path;
            export.RowCount = events.Count;
            export.State = DataExportStates.Succeeded;
            export.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _auditService.LogAsync(
                "EXPORT_DOWNLOADED",
                export.RequestedByEmployeeId,
                $"employee:{export.RequestedByEmployeeId}",
                "SUCCESS",
                "DataExport",
                export.Id.ToString(),
                changes: new { export.Type, export.RowCount, filter = export.FilterJson });
        }
        catch (Exception ex)
        {
            export.State = DataExportStates.Failed;
            export.Error = ex.Message;
            export.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            throw;
        }
    }

    public async Task ProcessSubjectAccessExportAsync(long exportId)
    {
        var export = await _db.DataExports.FirstOrDefaultAsync(e => e.Id == exportId)
            ?? throw new InvalidOperationException($"Export {exportId} not found");

        if (export.SubjectEmployeeId == null)
            throw new InvalidOperationException("Subject employee id missing");

        export.State = DataExportStates.Running;
        await _db.SaveChangesAsync();

        try
        {
            var employeeId = export.SubjectEmployeeId.Value;
            var employee = await _db.Employees.AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.Level)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.EmployeeId == employeeId);
            var tasks = await _db.Tasks.AsNoTracking()
                .Where(t => t.AssigneeId == employeeId || t.CreatedById == employeeId)
                .Select(t => new { t.Id, t.Key, t.Title, t.Status, t.AssigneeId, t.CreatedById, t.DeletedAt })
                .ToListAsync();
            var comments = await _db.TaskComments.AsNoTracking()
                .Where(c => c.AuthorId == employeeId)
                .Select(c => new { c.Id, c.TaskId, c.Body, c.CreatedAt, c.DeletedAt })
                .ToListAsync();
            var timeEntries = await _db.TaskTimeEntries.AsNoTracking()
                .Where(t => t.EmployeeId == employeeId)
                .Select(t => new { t.Id, t.TaskId, t.Minutes, t.Note, t.WorkedOn, t.CreatedAt })
                .ToListAsync();
            var notifications = await _db.Notifications.AsNoTracking()
                .Where(n => n.RecipientId == employeeId)
                .Select(n => new { n.Id, n.Type, n.Message, n.IsRead, n.CreatedAt, n.TaskId })
                .ToListAsync();
            var audit = (await _auditRepository.GetEventsForEmployeeAsync(employeeId)).ToList();

            var pack = new
            {
                exportedAt = DateTime.UtcNow,
                subjectEmployeeId = employeeId,
                profile = employee == null ? null : new
                {
                    employee.Id,
                    employee.FullName,
                    employee.JobTitle,
                    employee.DepartmentId,
                    DepartmentName = employee.Department?.Name,
                    employee.LevelId,
                    LevelName = employee.Level?.Name,
                    employee.ManagerId,
                    employee.IsActive,
                    employee.IsErased
                },
                account = user == null ? null : new { user.Id, user.Username, user.Email, user.Role },
                tasks,
                comments,
                timeEntries,
                notifications,
                auditEvents = audit.Select(MapAudit)
            };

            var path = Path.Combine(_exportRoot, $"subject-access-{employeeId}-{exportId}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(pack, JsonOpts), Encoding.UTF8);

            export.FilePath = path;
            export.RowCount = 1;
            export.State = DataExportStates.Succeeded;
            export.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            export.State = DataExportStates.Failed;
            export.Error = ex.Message;
            export.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            throw;
        }
    }

    public async Task ProcessPurgeAsync(long purgeRunId)
    {
        var run = await _governance.GetPurgeRunByIdAsync(purgeRunId)
            ?? throw new InvalidOperationException($"Purge run {purgeRunId} not found");

        run.State = "RUNNING";
        await _governance.UpdatePurgeRunAsync(run);

        var policies = await _governance.GetRetentionPoliciesAsync();
        var activeHolds = await _governance.GetActiveHoldsAsync();
        var heldEmployeeIds = activeHolds
            .Where(h => h.TargetType == LegalHoldTargetTypes.Employee)
            .Select(h => h.TargetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var heldTaskIds = activeHolds
            .Where(h => h.TargetType == LegalHoldTargetTypes.Task)
            .Select(h => h.TargetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new Dictionary<string, object>();
        const int batchSize = 200;

        try
        {
            foreach (var policy in policies.Where(p => p.IsActive))
            {
                var cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays);
                var removed = 0;
                var wouldRemove = 0;

                switch (policy.DataClass)
                {
                    case RetentionDataClasses.AuditEvents:
                    {
                        var exempt = await _db.AuditEvents.AsNoTracking()
                            .Where(e =>
                                (e.TargetType == "Employee" && e.TargetId != null && heldEmployeeIds.Contains(e.TargetId))
                                || (e.TargetType == "Task" && e.TargetId != null && heldTaskIds.Contains(e.TargetId)))
                            .Select(e => e.Id)
                            .ToListAsync();

                        wouldRemove = await _db.AuditEvents.CountAsync(e =>
                            e.OccurredAt < cutoff
                            && e.EventType != "PURGE_EXECUTED"
                            && !exempt.Contains(e.Id));

                        if (!run.DryRun)
                        {
                            long afterId = 0;
                            while (true)
                            {
                                var batchIds = await _db.AuditEvents
                                    .Where(e => e.Id > afterId
                                                && e.OccurredAt < cutoff
                                                && e.EventType != "PURGE_EXECUTED"
                                                && !exempt.Contains(e.Id))
                                    .OrderBy(e => e.Id)
                                    .Select(e => e.Id)
                                    .Take(batchSize)
                                    .ToListAsync();
                                if (batchIds.Count == 0) break;
                                removed += await _db.AuditEvents.Where(e => batchIds.Contains(e.Id)).ExecuteDeleteAsync();
                                afterId = batchIds[^1];
                            }
                        }

                        results[policy.DataClass] = new { cutoff, removed = run.DryRun ? 0 : removed, wouldRemove };
                        break;
                    }
                    case RetentionDataClasses.Notifications:
                    {
                        wouldRemove = await _db.Notifications.CountAsync(n =>
                            n.CreatedAt < cutoff
                            && !heldEmployeeIds.Contains(n.RecipientId.ToString())
                            && (n.TaskId == null || !heldTaskIds.Contains(n.TaskId.Value.ToString())));
                        if (!run.DryRun)
                        {
                            while (true)
                            {
                                var ids = await _db.Notifications.Where(n =>
                                        n.CreatedAt < cutoff
                                        && !heldEmployeeIds.Contains(n.RecipientId.ToString())
                                        && (n.TaskId == null || !heldTaskIds.Contains(n.TaskId.Value.ToString())))
                                    .OrderBy(n => n.Id).Select(n => n.Id).Take(batchSize).ToListAsync();
                                if (ids.Count == 0) break;
                                removed += await _db.Notifications.Where(n => ids.Contains(n.Id)).ExecuteDeleteAsync();
                            }
                        }
                        results[policy.DataClass] = new { cutoff, removed = run.DryRun ? 0 : removed, wouldRemove };
                        break;
                    }
                    case RetentionDataClasses.TaskActivity:
                    {
                        wouldRemove = await _db.TaskActivities.CountAsync(a =>
                            a.CreatedAt < cutoff && !heldTaskIds.Contains(a.TaskId.ToString()));
                        if (!run.DryRun)
                        {
                            while (true)
                            {
                                var ids = await _db.TaskActivities
                                    .Where(a => a.CreatedAt < cutoff && !heldTaskIds.Contains(a.TaskId.ToString()))
                                    .OrderBy(a => a.Id).Select(a => a.Id).Take(batchSize).ToListAsync();
                                if (ids.Count == 0) break;
                                removed += await _db.TaskActivities.Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync();
                            }
                        }
                        results[policy.DataClass] = new { cutoff, removed = run.DryRun ? 0 : removed, wouldRemove };
                        break;
                    }
                    case RetentionDataClasses.SoftDeletedTasks:
                    {
                        wouldRemove = await _db.Tasks.CountAsync(t =>
                            t.DeletedAt != null && t.DeletedAt < cutoff && !heldTaskIds.Contains(t.Id.ToString()));
                        if (!run.DryRun)
                        {
                            while (true)
                            {
                                var ids = await _db.Tasks
                                    .Where(t => t.DeletedAt != null && t.DeletedAt < cutoff && !heldTaskIds.Contains(t.Id.ToString()))
                                    .OrderBy(t => t.Id).Select(t => t.Id).Take(batchSize).ToListAsync();
                                if (ids.Count == 0) break;
                                removed += await _db.Tasks.Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync();
                            }
                        }
                        results[policy.DataClass] = new { cutoff, removed = run.DryRun ? 0 : removed, wouldRemove };
                        break;
                    }
                    case RetentionDataClasses.SessionRecords:
                    {
                        var heldUserIds = await _db.Users.AsNoTracking()
                            .Where(u => u.EmployeeId != null && heldEmployeeIds.Contains(u.EmployeeId.Value.ToString()))
                            .Select(u => u.Id)
                            .ToListAsync();
                        wouldRemove = await _db.AuthSessions.CountAsync(s =>
                            (s.RefreshExpiresAt < cutoff || (s.RevokedAt != null && s.RevokedAt < cutoff))
                            && !heldUserIds.Contains(s.UserId));
                        if (!run.DryRun)
                        {
                            while (true)
                            {
                                var ids = await _db.AuthSessions
                                    .Where(s =>
                                        (s.RefreshExpiresAt < cutoff || (s.RevokedAt != null && s.RevokedAt < cutoff))
                                        && !heldUserIds.Contains(s.UserId))
                                    .OrderBy(s => s.CreatedAt)
                                    .Select(s => s.Id)
                                    .Take(batchSize)
                                    .ToListAsync();
                                if (ids.Count == 0) break;
                                removed += await _db.AuthSessions.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync();
                            }
                        }
                        results[policy.DataClass] = new { cutoff, removed = run.DryRun ? 0 : removed, wouldRemove };
                        break;
                    }
                    case RetentionDataClasses.IdempotencyKeys:
                    {
                        // PL-3: each record carries its own ExpiresAt (set by IdempotencyMiddleware,
                        // typically 24h) - purged against that field directly rather than the
                        // policy's generic RetentionDays cutoff, since it is the more precise signal.
                        var now = DateTime.UtcNow;
                        wouldRemove = await _db.IdempotencyRecords.CountAsync(r => r.ExpiresAt < now);
                        if (!run.DryRun)
                        {
                            while (true)
                            {
                                var ids = await _db.IdempotencyRecords
                                    .Where(r => r.ExpiresAt < now)
                                    .OrderBy(r => r.Id).Select(r => r.Id).Take(batchSize).ToListAsync();
                                if (ids.Count == 0) break;
                                removed += await _db.IdempotencyRecords.Where(r => ids.Contains(r.Id)).ExecuteDeleteAsync();
                            }
                        }
                        results[policy.DataClass] = new { cutoff = now, removed = run.DryRun ? 0 : removed, wouldRemove };
                        break;
                    }
                    default:
                        results[policy.DataClass] = new { skipped = true };
                        break;
                }
            }

            run.State = "SUCCEEDED";
            run.CompletedAt = DateTime.UtcNow;
            run.ResultsJson = JsonSerializer.Serialize(results, JsonOpts);
            await _governance.UpdatePurgeRunAsync(run);

            await _auditService.LogAsync(
                "PURGE_EXECUTED",
                run.RequestedByEmployeeId,
                $"employee:{run.RequestedByEmployeeId}",
                "SUCCESS",
                "PurgeRun",
                run.Id.ToString(),
                changes: new { run.DryRun, results });
        }
        catch (Exception ex)
        {
            run.State = "FAILED";
            run.Error = ex.Message;
            run.CompletedAt = DateTime.UtcNow;
            await _governance.UpdatePurgeRunAsync(run);
            throw;
        }
    }

    public async Task ProcessErasureAsync(int subjectEmployeeId, int actorEmployeeId)
    {
        if (await _governance.HasActiveHoldAsync(LegalHoldTargetTypes.Employee, subjectEmployeeId.ToString()))
            throw new InvalidOperationException("Erasure refused: legal hold in force (DG-9)");

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == subjectEmployeeId)
            ?? throw new InvalidOperationException("Employee not found");

        if (employee.IsErased) return;

        var opaque = $"former-employee-{subjectEmployeeId}";
        employee.FullName = $"Former Employee #{subjectEmployeeId}";
        employee.JobTitle = "Redacted";
        employee.IsActive = false;
        employee.IsErased = true;
        employee.ErasedAt = DateTime.UtcNow;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmployeeId == subjectEmployeeId);
        if (user != null)
        {
            user.Username = opaque;
            user.Email = $"{opaque}@redacted.local";
            user.PasswordHash = "ERASED";
        }

        await _db.TaskComments
            .Where(c => c.AuthorId == subjectEmployeeId && c.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Body, "[redacted]")
                .SetProperty(c => c.DeletedAt, DateTime.UtcNow));

        await _db.TaskTimeEntries
            .Where(t => t.EmployeeId == subjectEmployeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Note, (string?)null));

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "ERASURE_EXECUTED",
            actorEmployeeId,
            $"employee:{actorEmployeeId}",
            "SUCCESS",
            "Employee",
            subjectEmployeeId.ToString(),
            changes: new { subjectEmployeeId, opaqueRef = opaque });
    }

    private static object MapAudit(AuditEvent e) => new
    {
        e.Id,
        e.OccurredAt,
        e.EventType,
        e.ActorId,
        e.ActorLabel,
        e.OnBehalfOfId,
        e.TargetType,
        e.TargetId,
        e.Outcome,
        e.ChangesJson,
        e.RequestId,
        e.SourceIp
    };
}

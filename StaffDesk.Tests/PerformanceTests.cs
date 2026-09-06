using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Services;
using StaffDesk.Infrastructure.Data;
using StaffDesk.Infrastructure.Repositories;

namespace StaffDesk.Tests;

public class PerformanceTests
{
    private static readonly string LongJustification = new string('x', 200);

    private static async Task<(AppDbContext db, PerformanceService svc, Employee mgr, Employee emp, Employee peer, Employee other)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);

        var junior = new SeniorityLevel { Name = "Junior", Rank = 20, IsActive = true };
        var senior = new SeniorityLevel { Name = "Senior", Rank = 40, IsActive = true };
        db.SeniorityLevels.AddRange(junior, senior);
        await db.SaveChangesAsync();

        var dept = new Department { Name = "Engineering", Location = "Test" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var mgr = new Employee
        {
            FullName = "Manager One", JobTitle = "Mgr", DepartmentId = dept.Id,
            LevelId = senior.Id, JoinedAt = new DateOnly(2019, 1, 1), IsActive = true
        };
        db.Employees.Add(mgr);
        await db.SaveChangesAsync();

        var emp = new Employee
        {
            FullName = "Employee One", JobTitle = "Dev", DepartmentId = dept.Id,
            LevelId = junior.Id, ManagerId = mgr.Id, JoinedAt = new DateOnly(2020, 6, 1), IsActive = true
        };
        var peer = new Employee
        {
            FullName = "Peer One", JobTitle = "Dev", DepartmentId = dept.Id,
            LevelId = junior.Id, ManagerId = mgr.Id, JoinedAt = new DateOnly(2020, 6, 1), IsActive = true
        };
        var other = new Employee
        {
            FullName = "Other Dept", JobTitle = "Dev", DepartmentId = dept.Id,
            LevelId = junior.Id, ManagerId = mgr.Id, JoinedAt = new DateOnly(2020, 6, 1), IsActive = true
        };
        db.Employees.AddRange(emp, peer, other);
        await db.SaveChangesAsync();

        db.Competencies.Add(new Competency
        {
            Name = "Quality of work", Description = "Test", Category = "Delivery", IsActive = true
        });
        await db.SaveChangesAsync();

        var audit = new AuditService(new AuditRepository(db));
        var calendar = new WorkingCalendarService(new PerfStubCalendarRepo());
        var svc = new PerformanceService(new PerformanceRepository(db), audit, new TaskRepository(db, calendar));
        return (db, svc, mgr, emp, peer, other);
    }

    private static string Just => LongJustification;

    private sealed class PerfStubCalendarRepo : IWorkCalendarRepository
    {
        public Task<CalendarHoliday> AddHolidayAsync(CalendarHoliday holiday) => throw new NotImplementedException();
        public Task<WorkCalendar> CreateAsync(WorkCalendar calendar) => throw new NotImplementedException();
        public Task<WorkCalendar?> GetByIdAsync(int id) => Task.FromResult<WorkCalendar?>(null);
        public Task<WorkCalendar?> GetForDepartmentAsync(int departmentId, DateTime? asOfUtc = null) => Task.FromResult<WorkCalendar?>(null);
        public Task<WorkCalendar?> GetOrganizationDefaultAsync(DateTime? asOfUtc = null) => Task.FromResult<WorkCalendar?>(null);
        public Task<IReadOnlyList<WorkCalendar>> ListAsync() => Task.FromResult<IReadOnlyList<WorkCalendar>>(Array.Empty<WorkCalendar>());
        public Task<bool> RemoveHolidayAsync(int holidayId) => Task.FromResult(false);
        public Task SeedDefaultIfEmptyAsync() => Task.CompletedTask;
        public Task SetDepartmentCalendarAsync(int departmentId, int? calendarId) => Task.CompletedTask;
        public Task UpdateAsync(WorkCalendar calendar) => Task.CompletedTask;
    }

    [Fact]
    public async Task Admin_Denied_On_CreateCycle()
    {
        var (db, svc, mgr, _, _, _) = await SeedAsync();
        await using (db)
        {
            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.CreateCycleAsync(mgr.Id, User.Roles.Admin, new CreateReviewCycleRequest
                {
                    Name = "H1", PeriodStart = "2026-01-01", PeriodEnd = "2026-06-30",
                    DepartmentIds = new List<int> { 1 }
                }));
            Assert.Equal(TaskErrorCodes.Forbidden, ex.Code);
        }
    }

    [Fact]
    public async Task Stage_Machine_Opens_Reviews_And_Gates_Self_Assessment()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            var cycleObj = await svc.CreateCycleAsync(mgr.Id, User.Roles.HrAdmin, new CreateReviewCycleRequest
            {
                Name = "H1 2026",
                PeriodStart = "2026-01-01",
                PeriodEnd = "2026-06-30",
                DepartmentIds = new List<int> { emp.DepartmentId },
                JoinCutOff = "2026-01-01",
                PeerPresentationMode = PeerPresentationModes.Aggregated
            });
            var cycleId = (int)cycleObj.GetType().GetProperty("Id")!.GetValue(cycleObj)!;

            Assert.Equal(0, await db.PerformanceReviews.CountAsync());

            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            Assert.Equal(1, await db.PerformanceReviews.CountAsync(r => r.EmployeeId == emp.Id));

            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);

            await svc.SaveSelfAssessmentAsync(emp.Id, User.Roles.Member, review.Id, new AssessmentRequest
            {
                OverallRating = 3, Justification = Just, Submit = true
            });

            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.SaveManagerAssessmentAsync(mgr.Id, User.Roles.Manager, review.Id, new AssessmentRequest
                {
                    OverallRating = 3, Justification = Just, Submit = true
                }));
            Assert.Equal(TaskErrorCodes.Forbidden, ex.Code);

            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            await svc.SaveManagerAssessmentAsync(mgr.Id, User.Roles.Manager, review.Id, new AssessmentRequest
            {
                OverallRating = 4, Justification = Just, Submit = true
            });
        }
    }

    [Fact]
    public async Task Justification_Required_On_Submit()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            var cycleObj = await svc.CreateCycleAsync(mgr.Id, User.Roles.HrAdmin, new CreateReviewCycleRequest
            {
                Name = "H1", PeriodStart = "2026-01-01", PeriodEnd = "2026-06-30",
                DepartmentIds = new List<int> { emp.DepartmentId }, JoinCutOff = "2026-01-01"
            });
            var cycleId = (int)cycleObj.GetType().GetProperty("Id")!.GetValue(cycleObj)!;
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);

            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.SaveSelfAssessmentAsync(emp.Id, User.Roles.Member, review.Id, new AssessmentRequest
                {
                    OverallRating = 3, Justification = "too short", Submit = true
                }));
            Assert.Equal(422, ex.HttpStatus);
        }
    }

    [Fact]
    public async Task Manager_Cannot_See_Self_Assessment_Before_Submit()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            var cycleObj = await svc.CreateCycleAsync(mgr.Id, User.Roles.HrAdmin, new CreateReviewCycleRequest
            {
                Name = "H1", PeriodStart = "2026-01-01", PeriodEnd = "2026-06-30",
                DepartmentIds = new List<int> { emp.DepartmentId }, JoinCutOff = "2026-01-01"
            });
            var cycleId = (int)cycleObj.GetType().GetProperty("Id")!.GetValue(cycleObj)!;
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);

            await svc.SaveSelfAssessmentAsync(emp.Id, User.Roles.Member, review.Id, new AssessmentRequest
            {
                OverallRating = 3, Justification = Just, Submit = false
            });

            var shaped = await svc.GetReviewAsync(mgr.Id, User.Roles.Manager, review.Id);
            var json = JsonSerializer.Serialize(shaped, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("selfAssessment").ValueKind);
        }
    }

    [Fact]
    public async Task Calibration_Retains_Pre_Change_Rating()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            var cycleId = await OpenThroughManagerAsync(svc, db, mgr, emp);
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());

            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);
            Assert.Equal(4, review.ManagerOverallRating);

            await svc.CalibrateRatingAsync(mgr.Id, User.Roles.HrAdmin, review.Id, new CalibrateRequest
            {
                OverallRating = 3,
                Reason = "Aligned with peer cohort evidence"
            });

            await db.Entry(review).ReloadAsync();
            Assert.Equal(4, review.PreCalibrationOverallRating);
            Assert.Equal(3, review.CalibratedOverallRating);
        }
    }

    [Fact]
    public async Task Peer_Aggregation_Withheld_Below_Three()
    {
        var (db, svc, mgr, emp, peer, other) = await SeedAsync();
        await using (db)
        {
            var cycleId = await OpenThroughManagerAsync(svc, db, mgr, emp);
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);

            await svc.NominatePeersAsync(mgr.Id, User.Roles.Manager, review.Id, new PeerNominationRequest
            {
                NomineeEmployeeIds = new List<int> { peer.Id, other.Id }
            });

            var invitations = await db.PeerInvitations.Where(p => p.PerformanceReviewId == review.Id).ToListAsync();
            Assert.Equal(2, invitations.Count);
            foreach (var inv in invitations)
            {
                await svc.SubmitPeerFeedbackAsync(inv.Token!, new PeerFeedbackRequest
                {
                    OverallRating = 4, Justification = Just
                });
            }

            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());

            var shaped = await svc.GetReviewAsync(emp.Id, User.Roles.Member, review.Id);
            var json = JsonSerializer.Serialize(shaped);
            Assert.Contains("withheld", json);
        }
    }

    [Fact]
    public async Task Review_Read_Is_Audited()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            await OpenThroughManagerAsync(svc, db, mgr, emp);
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);
            await svc.GetReviewAsync(emp.Id, User.Roles.Member, review.Id);

            Assert.Contains(db.AuditEvents, e => e.EventType == PerformanceAuditEvents.ReviewRead
                                                 && e.TargetId == review.Id.ToString());
        }
    }

    [Fact]
    public async Task Member_Denied_Peer_Review_Read()
    {
        var (db, svc, mgr, emp, peer, _) = await SeedAsync();
        await using (db)
        {
            await OpenThroughManagerAsync(svc, db, mgr, emp);
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);

            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.GetReviewAsync(peer.Id, User.Roles.Member, review.Id));
            Assert.Equal(TaskErrorCodes.NotFound, ex.Code);
        }
    }

    [Fact]
    public async Task HrAdmin_Cannot_Attach_Evidence_After_Publication()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            var cycleId = await OpenThroughManagerAsync(svc, db, mgr, emp);
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);

            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.AttachEvidenceAsync(mgr.Id, User.Roles.HrAdmin, review.Id, new AttachEvidenceRequest
                {
                    EvidenceType = ReviewEvidenceTypes.Note, Note = "sneaking in post-publish"
                }));
            Assert.Equal(TaskErrorCodes.Forbidden, ex.Code);
        }
    }

    [Fact]
    public async Task HrAdmin_Reassigns_Reviewer_With_Reason()
    {
        var (db, svc, mgr, emp, _, other) = await SeedAsync();
        await using (db)
        {
            var cycleObj = await svc.CreateCycleAsync(mgr.Id, User.Roles.HrAdmin, new CreateReviewCycleRequest
            {
                Name = "H1", PeriodStart = "2026-01-01", PeriodEnd = "2026-06-30",
                DepartmentIds = new List<int> { emp.DepartmentId }, JoinCutOff = "2026-01-01"
            });
            var cycleId = (int)cycleObj.GetType().GetProperty("Id")!.GetValue(cycleObj)!;
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);
            Assert.Equal(mgr.Id, review.ReviewerEmployeeId);

            var noReason = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.ReassignReviewerAsync(mgr.Id, User.Roles.HrAdmin, review.Id, new ReassignReviewerRequest
                {
                    NewReviewerEmployeeId = other.Id, Reason = ""
                }));
            Assert.Equal(422, noReason.HttpStatus);

            var nonHr = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.ReassignReviewerAsync(mgr.Id, User.Roles.Manager, review.Id, new ReassignReviewerRequest
                {
                    NewReviewerEmployeeId = other.Id, Reason = "conflict"
                }));
            Assert.Equal(TaskErrorCodes.Forbidden, nonHr.Code);

            await svc.ReassignReviewerAsync(mgr.Id, User.Roles.HrAdmin, review.Id, new ReassignReviewerRequest
            {
                NewReviewerEmployeeId = other.Id, Reason = "Original reviewer had a conflict of interest"
            });
            await db.Entry(review).ReloadAsync();
            Assert.Equal(other.Id, review.ReviewerEmployeeId);
        }
    }

    [Fact]
    public async Task Appeal_Cannot_Be_Decided_By_Original_Reviewer_Or_Employee()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            var cycleId = await OpenThroughManagerAsync(svc, db, mgr, emp);
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);

            var appealResult = await svc.RaiseAppealAsync(emp.Id, User.Roles.Member, review.Id, new AppealRequest
            {
                Ground = "The rating does not reflect my contributions this period"
            });
            var appealId = (int)appealResult.GetType().GetProperty("Id")!.GetValue(appealResult)!;

            var byReviewer = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.DecideAppealAsync(mgr.Id, User.Roles.Manager, review.Id, appealId, new DecideAppealRequest
                {
                    Outcome = AppealOutcomes.Upheld, Reason = "self-approving"
                }));
            Assert.Equal(TaskErrorCodes.Forbidden, byReviewer.Code);

            var byEmployee = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.DecideAppealAsync(emp.Id, User.Roles.Member, review.Id, appealId, new DecideAppealRequest
                {
                    Outcome = AppealOutcomes.Upheld, Reason = "self-approving"
                }));
            Assert.Equal(TaskErrorCodes.Forbidden, byEmployee.Code);

            var decided = await svc.DecideAppealAsync(mgr.Id, User.Roles.HrAdmin, review.Id, appealId, new DecideAppealRequest
            {
                Outcome = AppealOutcomes.Amended, Reason = "Reviewed additional context and adjusted the note"
            });
            var outcome = (string?)decided.GetType().GetProperty("Outcome")!.GetValue(decided);
            Assert.Equal(AppealOutcomes.Amended, outcome);

            var again = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.DecideAppealAsync(mgr.Id, User.Roles.HrAdmin, review.Id, appealId, new DecideAppealRequest
                {
                    Outcome = AppealOutcomes.Upheld, Reason = "trying again"
                }));
            Assert.Equal(409, again.HttpStatus);
        }
    }

    [Fact]
    public async Task Manager_Who_Did_Not_Review_In_Cycle_Cannot_Read_Report_Review()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            // A second manager, upstream of nobody in this cycle, has the MANAGER role but never
            // reviewed anyone in it - PM-37 says that alone must not grant read access.
            var outsideManager = new Employee
            {
                FullName = "Outside Manager", JobTitle = "Mgr", DepartmentId = emp.DepartmentId,
                LevelId = mgr.LevelId, JoinedAt = new DateOnly(2019, 1, 1), IsActive = true
            };
            db.Employees.Add(outsideManager);
            await db.SaveChangesAsync();

            var cycleId = await OpenThroughManagerAsync(svc, db, mgr, emp);
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);

            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.GetReviewAsync(outsideManager.Id, User.Roles.Manager, review.Id));
            Assert.Equal(TaskErrorCodes.NotFound, ex.Code);

            // The actual reviewer can still read it.
            var shaped = await svc.GetReviewAsync(mgr.Id, User.Roles.Manager, review.Id);
            Assert.NotNull(shaped);
        }
    }

    [Fact]
    public async Task HrAdmin_Reverses_Stage_With_Reason_Only()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            var cycleObj = await svc.CreateCycleAsync(mgr.Id, User.Roles.HrAdmin, new CreateReviewCycleRequest
            {
                Name = "H1", PeriodStart = "2026-01-01", PeriodEnd = "2026-06-30",
                DepartmentIds = new List<int> { emp.DepartmentId }, JoinCutOff = "2026-01-01"
            });
            var cycleId = (int)cycleObj.GetType().GetProperty("Id")!.GetValue(cycleObj)!;
            await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());

            var noReason = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.ReverseStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest()));
            Assert.Equal(422, noReason.HttpStatus);

            var nonHr = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.ReverseStageAsync(mgr.Id, User.Roles.Manager, cycleId, new AdvanceStageRequest { Reason = "x" }));
            Assert.Equal(TaskErrorCodes.Forbidden, nonHr.Code);

            var result = await svc.ReverseStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId,
                new AdvanceStageRequest { Reason = "opened with the wrong department scope" });
            var stage = (string?)result.GetType().GetProperty("Stage")!.GetValue(result);
            Assert.Equal(ReviewCycleStages.Draft, stage);
        }
    }

    [Fact]
    public async Task Admin_Cannot_Read_Review()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            await OpenThroughManagerAsync(svc, db, mgr, emp);
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);
            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.GetReviewAsync(mgr.Id, User.Roles.Admin, review.Id));
            Assert.Equal(TaskErrorCodes.Forbidden, ex.Code);
        }
    }

    [Fact]
    public async Task Auditor_Cannot_Read_Review()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            await OpenThroughManagerAsync(svc, db, mgr, emp);
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);
            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.GetReviewAsync(emp.Id, User.Roles.Auditor, review.Id));
            Assert.Equal(TaskErrorCodes.Forbidden, ex.Code);
        }
    }

    [Fact]
    public async Task Employee_Does_Not_See_Manager_Assessment_Before_Publish()
    {
        var (db, svc, mgr, emp, _, _) = await SeedAsync();
        await using (db)
        {
            await OpenThroughManagerAsync(svc, db, mgr, emp);
            var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);
            await svc.SaveManagerAssessmentAsync(mgr.Id, User.Roles.Manager, review.Id, new AssessmentRequest
            {
                OverallRating = 4, Justification = Just, Submit = true
            });
            var shaped = await svc.GetReviewAsync(emp.Id, User.Roles.Member, review.Id);
            var json = JsonSerializer.Serialize(shaped);
            Assert.DoesNotContain("\"overallRating\":4", json);
        }
    }

    private static async Task<int> OpenThroughManagerAsync(
        PerformanceService svc, AppDbContext db, Employee mgr, Employee emp)
    {
        var cycleObj = await svc.CreateCycleAsync(mgr.Id, User.Roles.HrAdmin, new CreateReviewCycleRequest
        {
            Name = "H1", PeriodStart = "2026-01-01", PeriodEnd = "2026-06-30",
            DepartmentIds = new List<int> { emp.DepartmentId }, JoinCutOff = "2026-01-01",
            PeerPresentationMode = PeerPresentationModes.Aggregated
        });
        var cycleId = (int)cycleObj.GetType().GetProperty("Id")!.GetValue(cycleObj)!;
        await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
        var review = await db.PerformanceReviews.FirstAsync(r => r.EmployeeId == emp.Id);
        await svc.SaveSelfAssessmentAsync(emp.Id, User.Roles.Member, review.Id, new AssessmentRequest
        {
            OverallRating = 3, Justification = Just, Submit = true
        });
        await svc.AdvanceStageAsync(mgr.Id, User.Roles.HrAdmin, cycleId, new AdvanceStageRequest());
        await svc.SaveManagerAssessmentAsync(mgr.Id, User.Roles.Manager, review.Id, new AssessmentRequest
        {
            OverallRating = 4, Justification = Just, Submit = true
        });
        return cycleId;
    }
}

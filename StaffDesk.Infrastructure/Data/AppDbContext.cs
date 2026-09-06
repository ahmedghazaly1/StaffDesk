using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;

namespace StaffDesk.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options) { }

    // ============================================
    // Phase 1 & 2 Entities
    // ============================================
    public DbSet<Department> Departments { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<SeniorityLevel> SeniorityLevels { get; set; }
    public DbSet<EmployeeActivity> EmployeeActivities { get; set; }

    // ============================================
    // Phase 2 - Task Management Entities
    // ============================================
    public DbSet<WorkTask> Tasks { get; set; }
    public DbSet<TaskComment> TaskComments { get; set; }
    public DbSet<TaskWatcher> TaskWatchers { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<TaskTag> TaskTags { get; set; }
    public DbSet<TaskChecklistItem> TaskChecklistItems { get; set; }
    public DbSet<TaskDependency> TaskDependencies { get; set; }
    public DbSet<TaskTimeEntry> TaskTimeEntries { get; set; }
    public DbSet<TaskActivity> TaskActivities { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    // ============================================
    // Phase 3 - Audit
    // ============================================
    public DbSet<AuditEvent> AuditEvents { get; set; }

    // ============================================
    // Part B - Work Cycle Completion
    // ============================================
    public DbSet<TaskStatusInterval> TaskStatusIntervals { get; set; }
    public DbSet<TaskRequest> TaskRequests { get; set; }
    public DbSet<TaskAcceptanceCriterion> TaskAcceptanceCriteria { get; set; }
    public DbSet<DepartmentDefaultCriterion> DepartmentDefaultCriteria { get; set; }

    // ============================================
    // Phase 3 - SLA & Escalation
    // ============================================
    public DbSet<SlaPolicy> SlaPolicies { get; set; }
    public DbSet<SlaEscalation> SlaEscalations { get; set; }

    // ============================================
    // Part B - Approvals
    // ============================================
    public DbSet<TaskApproval> TaskApprovals { get; set; }
    public DbSet<ApprovalStep> ApprovalSteps { get; set; }

    // ============================================
    // Part B - Rework & Quality (§5.5)
    // ============================================
    public DbSet<ReworkEvent> ReworkEvents { get; set; }

    // ============================================
    // Part B - Closure & Outcome (§5.6)
    // ============================================
    public DbSet<TaskOutcome> TaskOutcomes { get; set; }
    public DbSet<TaskClosure> TaskClosures { get; set; }

    // ============================================
    // Part B - Recurrence & Templates (§5.7)
    // ============================================
    public DbSet<TaskTemplate> TaskTemplates { get; set; }
    public DbSet<RecurrenceRule> RecurrenceRules { get; set; }
    public DbSet<RecurrenceOccurrence> RecurrenceOccurrences { get; set; }

    // ============================================
    // Part B - Delegation (§5.10)
    // ============================================
    public DbSet<Delegation> Delegations { get; set; }

    // ============================================
    // Part B - Jobs, Saved Views, Triagers, SLA history
    // ============================================
    public DbSet<Job> Jobs { get; set; }
    public DbSet<SavedView> SavedViews { get; set; }
    public DbSet<DepartmentTriager> DepartmentTriagers { get; set; }
    public DbSet<TaskSlaRecord> TaskSlaRecords { get; set; }

    // ============================================
    // Part A — Governance (retention, holds, exports)
    // ============================================
    public DbSet<RetentionPolicy> RetentionPolicies { get; set; }
    public DbSet<LegalHold> LegalHolds { get; set; }
    public DbSet<DataExport> DataExports { get; set; }
    public DbSet<PurgeRun> PurgeRuns { get; set; }

    // ============================================
    // Part C — Capacity, Leave, Timesheets
    // ============================================
    public DbSet<WorkCalendar> WorkCalendars { get; set; }
    public DbSet<CalendarHoliday> CalendarHolidays { get; set; }
    public DbSet<LeaveRequest> LeaveRequests { get; set; }
    public DbSet<Timesheet> Timesheets { get; set; }

    // ============================================
    // Part D — Analytics snapshots
    // ============================================
    public DbSet<DailyMetricSnapshot> DailyMetricSnapshots { get; set; }

    // ============================================
    // Part E — Performance Management
    // ============================================
    public DbSet<Competency> Competencies { get; set; }
    public DbSet<CompetencyLevelDescriptor> CompetencyLevelDescriptors { get; set; }
    public DbSet<ReviewCycle> ReviewCycles { get; set; }
    public DbSet<PerformanceReview> PerformanceReviews { get; set; }
    public DbSet<ReviewEvidence> ReviewEvidences { get; set; }
    public DbSet<ReviewAppeal> ReviewAppeals { get; set; }
    public DbSet<PeerInvitation> PeerInvitations { get; set; }
    public DbSet<PeerFeedback> PeerFeedbacks { get; set; }
    public DbSet<Goal> Goals { get; set; }
    public DbSet<GoalVersion> GoalVersions { get; set; }
    public DbSet<GoalTaskLink> GoalTaskLinks { get; set; }
    public DbSet<FeedbackNote> FeedbackNotes { get; set; }

    // ============================================
    // Phase 3 Part F - Platform & API Maturity
    // ============================================
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<WebhookSubscription> WebhookSubscriptions { get; set; }
    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; }

    // Part G — Operations (OB-1)
    public DbSet<WorkerHeartbeat> WorkerHeartbeats { get; set; }

    // Part H — Sessions (SP-1..SP-5)
    public DbSet<AuthSession> AuthSessions { get; set; }
    public DbSet<LoginThrottle> LoginThrottles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ============================================
        // Phase 1 & 2 Relationships
        // ============================================
        
        // Department - Employee (one-to-many)
        modelBuilder.Entity<Department>()
            .HasMany(d => d.Employees)
            .WithOne(e => e.Department)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Department - Manager (optional one-to-one)
        modelBuilder.Entity<Department>()
            .HasOne(d => d.Manager)
            .WithMany()
            .HasForeignKey(d => d.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Employee - SeniorityLevel (many-to-one)
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Level)
            .WithMany(l => l.Employees)
            .HasForeignKey(e => e.LevelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Employee - Manager (self-referencing)
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Manager)
            .WithMany(e => e.DirectReports)
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // SeniorityLevel - unique name and rank (OR-2)
        modelBuilder.Entity<SeniorityLevel>()
            .HasIndex(l => l.Name)
            .IsUnique();

        modelBuilder.Entity<SeniorityLevel>()
            .HasIndex(l => l.Rank)
            .IsUnique();

        // User - Employee (optional one-to-one link, A-2)
        modelBuilder.Entity<User>()
            .HasOne(u => u.Employee)
            .WithMany()
            .HasForeignKey(u => u.EmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        // EmployeeActivity (OR-13)
        modelBuilder.Entity<EmployeeActivity>()
            .HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // ============================================
        // Phase 2 - Task Management Relationships
        // ============================================

        // WorkTask - Department
        modelBuilder.Entity<WorkTask>()
            .HasOne(t => t.Department)
            .WithMany()
            .HasForeignKey(t => t.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkTask - Assignee
        modelBuilder.Entity<WorkTask>()
            .HasOne(t => t.Assignee)
            .WithMany()
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        // WorkTask - CreatedBy
        modelBuilder.Entity<WorkTask>()
            .HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkTask - Parent/Subtask (self-referencing)
        modelBuilder.Entity<WorkTask>()
            .HasOne(t => t.ParentTask)
            .WithMany(t => t.Subtasks)
            .HasForeignKey(t => t.ParentTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkTask - Key unique constraint
        modelBuilder.Entity<WorkTask>()
            .HasIndex(t => t.Key)
            .IsUnique();

        // LS-5: Status and DeletedAt are filtered on almost every task list query.
        modelBuilder.Entity<WorkTask>()
            .HasIndex(t => t.Status);

        modelBuilder.Entity<WorkTask>()
            .HasIndex(t => t.DeletedAt);

        // ============================================
        // TaskComment
        // ============================================
        modelBuilder.Entity<TaskComment>()
            .HasOne(c => c.Task)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskComment>()
            .HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================
        // TaskWatcher - Composite primary key
        // ============================================
        modelBuilder.Entity<TaskWatcher>()
            .HasKey(w => new { w.TaskId, w.EmployeeId });

        modelBuilder.Entity<TaskWatcher>()
            .HasOne(w => w.Task)
            .WithMany(t => t.Watchers)
            .HasForeignKey(w => w.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskWatcher>()
            .HasOne(w => w.Employee)
            .WithMany()
            .HasForeignKey(w => w.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================
        // Tag - Unique name
        // ============================================
        modelBuilder.Entity<Tag>()
            .HasIndex(t => t.Name)
            .IsUnique();

        // ============================================
        // TaskTag - Composite primary key
        // ============================================
        modelBuilder.Entity<TaskTag>()
            .HasKey(tt => new { tt.TaskId, tt.TagId });

        modelBuilder.Entity<TaskTag>()
            .HasOne(tt => tt.Task)
            .WithMany(t => t.Tags)
            .HasForeignKey(tt => tt.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskTag>()
            .HasOne(tt => tt.Tag)
            .WithMany(t => t.Tasks)
            .HasForeignKey(tt => tt.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================
        // TaskChecklistItem
        // ============================================
        modelBuilder.Entity<TaskChecklistItem>()
            .HasOne(ci => ci.Task)
            .WithMany(t => t.ChecklistItems)
            .HasForeignKey(ci => ci.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskChecklistItem>()
            .HasOne(ci => ci.CompletedBy)
            .WithMany()
            .HasForeignKey(ci => ci.CompletedById)
            .OnDelete(DeleteBehavior.SetNull);

        // ============================================
        // TaskDependency - Unique pair constraint
        // ============================================
        modelBuilder.Entity<TaskDependency>()
            .HasIndex(d => new { d.BlockedTaskId, d.BlockingTaskId })
            .IsUnique();

        modelBuilder.Entity<TaskDependency>()
            .HasOne(d => d.BlockedTask)
            .WithMany(t => t.BlockedBy)
            .HasForeignKey(d => d.BlockedTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskDependency>()
            .HasOne(d => d.BlockingTask)
            .WithMany(t => t.Blocking)
            .HasForeignKey(d => d.BlockingTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================
        // TaskTimeEntry
        // ============================================
        modelBuilder.Entity<TaskTimeEntry>()
            .HasOne(te => te.Task)
            .WithMany(t => t.TimeEntries)
            .HasForeignKey(te => te.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskTimeEntry>()
            .HasOne(te => te.Employee)
            .WithMany()
            .HasForeignKey(te => te.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================
        // TaskActivity
        // ============================================
        modelBuilder.Entity<TaskActivity>()
            .HasOne(a => a.Task)
            .WithMany(t => t.Activities)
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskActivity>()
            .HasOne(a => a.Actor)
            .WithMany()
            .HasForeignKey(a => a.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================
        // Notification
        // ============================================
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Task)
            .WithMany()
            .HasForeignKey(n => n.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        // ============================================
        // Phase 3 - Audit
        // ============================================
        modelBuilder.Entity<AuditEvent>()
            .HasIndex(e => e.OccurredAt);

        modelBuilder.Entity<AuditEvent>()
            .HasIndex(e => new { e.EventType, e.Outcome });

        modelBuilder.Entity<AuditEvent>()
            .HasIndex(e => e.ActorId);

        modelBuilder.Entity<AuditEvent>()
            .HasIndex(e => new { e.TargetType, e.TargetId });

        modelBuilder.Entity<AuditEvent>()
            .HasOne(e => e.Actor)
            .WithMany()
            .HasForeignKey(e => e.ActorId)
            .OnDelete(DeleteBehavior.SetNull);

        // ============================================
        // Part B - TaskStatusInterval (WC-23..WC-27)
        // ============================================
        modelBuilder.Entity<TaskStatusInterval>()
            .HasOne(i => i.Task)
            .WithMany(t => t.StatusIntervals)
            .HasForeignKey(i => i.TaskId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskStatusInterval>()
            .HasOne(i => i.Actor)
            .WithMany()
            .HasForeignKey(i => i.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        // WC-25: at most one OPEN interval (ExitedAt IS NULL) per task
        modelBuilder.Entity<TaskStatusInterval>()
            .HasIndex(i => i.TaskId)
            .IsUnique()
            .HasFilter("\"ExitedAt\" IS NULL");

        modelBuilder.Entity<TaskStatusInterval>()
            .HasIndex(i => new { i.TaskId, i.EnteredAt });

        // ============================================
        // Part B - TaskRequest (WC-1..WC-9)
        // ============================================
        modelBuilder.Entity<TaskRequest>()
            .HasOne(r => r.RequestedBy)
            .WithMany()
            .HasForeignKey(r => r.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskRequest>()
            .HasOne(r => r.Department)
            .WithMany()
            .HasForeignKey(r => r.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskRequest>()
            .HasOne(r => r.MergedIntoRequest)
            .WithMany()
            .HasForeignKey(r => r.MergedIntoRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        // WC-6: TaskRequest.CreatedTaskId -> WorkTask
        modelBuilder.Entity<TaskRequest>()
            .HasOne(r => r.CreatedTask)
            .WithMany()
            .HasForeignKey(r => r.CreatedTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskRequest>()
            .HasIndex(r => r.Status);

        modelBuilder.Entity<TaskRequest>()
            .HasIndex(r => new { r.DepartmentId, r.Status });

        // WorkTask.SourceRequestId -> TaskRequest (the reverse link back, WC-6).
        modelBuilder.Entity<WorkTask>()
            .HasOne(t => t.SourceRequest)
            .WithMany()
            .HasForeignKey(t => t.SourceRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        // ============================================
        // Part B - TaskAcceptanceCriterion (WC-10..WC-14)
        // ============================================
        modelBuilder.Entity<TaskAcceptanceCriterion>()
            .HasOne(ac => ac.Task)
            .WithMany(t => t.AcceptanceCriteria)
            .HasForeignKey(ac => ac.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskAcceptanceCriterion>()
            .HasOne(ac => ac.MetBy)
            .WithMany()
            .HasForeignKey(ac => ac.MetById)
            .OnDelete(DeleteBehavior.SetNull);

        // ============================================
        // Part B - DepartmentDefaultCriterion (WC-12)
        // ============================================
        modelBuilder.Entity<DepartmentDefaultCriterion>()
            .HasOne(d => d.Department)
            .WithMany(d => d.DefaultCriteria)
            .HasForeignKey(d => d.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // ============================================
        // Phase 3 - SLA & Escalation
        // ============================================

        // SlaPolicy - unique per (DepartmentId, Priority)
        modelBuilder.Entity<SlaPolicy>()
            .HasIndex(p => new { p.DepartmentId, p.Priority })
            .IsUnique();

        modelBuilder.Entity<SlaPolicy>()
            .HasOne(p => p.Department)
            .WithMany()
            .HasForeignKey(p => p.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // SlaEscalation - one per (TaskId, Level)
        modelBuilder.Entity<SlaEscalation>()
            .HasIndex(e => new { e.TaskId, e.Level })
            .IsUnique();

        modelBuilder.Entity<SlaEscalation>()
            .HasOne(e => e.Task)
            .WithMany()
            .HasForeignKey(e => e.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // ============================================
        // Part B - Approvals (AP-1..AP-6)
        // ============================================

        // TaskApproval - Task relationship
        modelBuilder.Entity<TaskApproval>()
            .HasOne(a => a.Task)
            .WithMany()
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // ApprovalStep - TaskApproval relationship (FIXED)
        modelBuilder.Entity<ApprovalStep>()
            .HasOne(s => s.TaskApproval)
            .WithMany(a => a.Steps)
            .HasForeignKey(s => s.TaskApprovalId)
            .OnDelete(DeleteBehavior.Cascade);

        // ApprovalStep - Task relationship
        modelBuilder.Entity<ApprovalStep>()
            .HasOne(s => s.Task)
            .WithMany()
            .HasForeignKey(s => s.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // ApprovalStep - Approver (Employee)
        modelBuilder.Entity<ApprovalStep>()
            .HasOne(s => s.Approver)
            .WithMany()
            .HasForeignKey(s => s.ApproverId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for faster queries
        modelBuilder.Entity<ApprovalStep>()
            .HasIndex(s => new { s.TaskId, s.State });

        modelBuilder.Entity<ApprovalStep>()
            .HasIndex(s => s.ApproverId);

        // ============================================
        // Part B - ReworkEvent (RW-1..RW-4)
        // ============================================
        modelBuilder.Entity<ReworkEvent>()
            .HasOne(r => r.Task)
            .WithMany()
            .HasForeignKey(r => r.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReworkEvent>()
            .HasOne(r => r.TriggeredByEmployee)
            .WithMany()
            .HasForeignKey(r => r.TriggeredBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ReworkEvent>()
            .HasIndex(r => r.TaskId);

        modelBuilder.Entity<ReworkEvent>()
            .HasIndex(r => new { r.Category, r.OccurredAt });

        // ============================================
        // Part B - Closure & Outcome (WC-15..WC-18)
        // ============================================

        // TaskOutcome - Task relationship
        modelBuilder.Entity<TaskOutcome>()
            .HasOne(o => o.Task)
            .WithMany()
            .HasForeignKey(o => o.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskOutcome>()
            .HasIndex(o => o.TaskId);

        modelBuilder.Entity<TaskOutcome>()
            .HasIndex(o => o.Outcome);

        // TaskClosure - Task relationship
        modelBuilder.Entity<TaskClosure>()
            .HasOne(c => c.Task)
            .WithMany()
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskClosure>()
            .HasIndex(c => c.TaskId);

        // ============================================
        // Part B - Recurrence & Templates (RC-1..RC-6)
        // ============================================

        // TaskTemplate - Department relationship
        modelBuilder.Entity<TaskTemplate>()
            .HasOne(t => t.Department)
            .WithMany()
            .HasForeignKey(t => t.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // TaskTemplate - CreatedBy (Employee)
        modelBuilder.Entity<TaskTemplate>()
            .HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // TaskTemplate - DefaultAssignee (Employee)
        modelBuilder.Entity<TaskTemplate>()
            .HasOne(t => t.DefaultAssignee)
            .WithMany()
            .HasForeignKey(t => t.DefaultAssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TaskTemplate>()
            .HasIndex(t => t.IsActive);

        // RecurrenceRule - Template relationship
        modelBuilder.Entity<RecurrenceRule>()
            .HasOne(r => r.Template)
            .WithMany()
            .HasForeignKey(r => r.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecurrenceRule>()
            .HasIndex(r => r.IsPaused);

        modelBuilder.Entity<RecurrenceRule>()
            .HasIndex(r => r.NextGenerationAt);

        // RecurrenceOccurrence - Rule relationship
        modelBuilder.Entity<RecurrenceOccurrence>()
            .HasOne(o => o.Rule)
            .WithMany()
            .HasForeignKey(o => o.RuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // RecurrenceOccurrence - Task relationship
        modelBuilder.Entity<RecurrenceOccurrence>()
            .HasOne(o => o.Task)
            .WithMany()
            .HasForeignKey(o => o.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RecurrenceOccurrence>()
            .HasIndex(o => o.OccurrenceDate);

        modelBuilder.Entity<RecurrenceOccurrence>()
            .HasIndex(o => o.State);

        // RC-3: one occurrence per rule+date
        modelBuilder.Entity<RecurrenceOccurrence>()
            .HasIndex(o => new { o.RuleId, o.OccurrenceDate })
            .IsUnique();

        // ============================================
        // Part B - Delegation (§5.10)
        // ============================================

        // Delegation - Delegator (Employee)
        modelBuilder.Entity<Delegation>()
            .HasOne(d => d.Delegator)
            .WithMany()
            .HasForeignKey(d => d.DelegatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Delegation - Delegate (Employee)
        modelBuilder.Entity<Delegation>()
            .HasOne(d => d.Delegate)
            .WithMany()
            .HasForeignKey(d => d.DelegateId)
            .OnDelete(DeleteBehavior.Restrict);

        // Delegation - CreatedBy (Employee)
        modelBuilder.Entity<Delegation>()
            .HasOne(d => d.CreatedByEmployee)
            .WithMany()
            .HasForeignKey(d => d.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Delegation>()
            .HasIndex(d => new { d.DelegatorId, d.IsActive });

        modelBuilder.Entity<Delegation>()
            .HasIndex(d => new { d.DelegateId, d.IsActive });

        modelBuilder.Entity<Delegation>()
            .HasIndex(d => d.EndDate);

        // ============================================
        // Part B - Jobs (AR-1..AR-5)
        // ============================================
        modelBuilder.Entity<Job>()
            .HasIndex(j => new { j.State, j.NextAttemptAt });

        modelBuilder.Entity<Job>()
            .HasIndex(j => j.Type);

        // ============================================
        // Part B - Saved Views (WC-21/22)
        // ============================================
        modelBuilder.Entity<SavedView>()
            .HasOne(v => v.Owner)
            .WithMany()
            .HasForeignKey(v => v.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SavedView>()
            .HasOne(v => v.Department)
            .WithMany()
            .HasForeignKey(v => v.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SavedView>()
            .HasIndex(v => new { v.OwnerId, v.Name });

        // ============================================
        // Part B - Department Triagers (WC-7)
        // ============================================
        modelBuilder.Entity<DepartmentTriager>()
            .HasOne(t => t.Department)
            .WithMany()
            .HasForeignKey(t => t.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DepartmentTriager>()
            .HasOne(t => t.Employee)
            .WithMany()
            .HasForeignKey(t => t.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DepartmentTriager>()
            .HasIndex(t => new { t.DepartmentId, t.EmployeeId })
            .IsUnique();

        // ============================================
        // Part B - Task SLA history (SL-7)
        // ============================================
        modelBuilder.Entity<TaskSlaRecord>()
            .HasOne(r => r.Task)
            .WithMany()
            .HasForeignKey(r => r.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskSlaRecord>()
            .HasIndex(r => new { r.TaskId, r.EffectiveFrom });

        // ============================================
        // Part A — Governance
        // ============================================
        modelBuilder.Entity<RetentionPolicy>()
            .HasIndex(p => p.DataClass)
            .IsUnique();

        modelBuilder.Entity<LegalHold>()
            .HasOne(h => h.PlacedBy)
            .WithMany()
            .HasForeignKey(h => h.PlacedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LegalHold>()
            .HasOne(h => h.LiftedBy)
            .WithMany()
            .HasForeignKey(h => h.LiftedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LegalHold>()
            .HasIndex(h => new { h.TargetType, h.TargetId, h.LiftedAt });

        modelBuilder.Entity<DataExport>()
            .HasOne(e => e.RequestedBy)
            .WithMany()
            .HasForeignKey(e => e.RequestedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DataExport>()
            .HasIndex(e => e.CreatedAt);

        // ============================================
        // Part C — Work calendars, leave, timesheets
        // ============================================
        modelBuilder.Entity<WorkCalendar>()
            .HasIndex(c => c.IsOrganizationDefault);

        modelBuilder.Entity<CalendarHoliday>()
            .HasOne(h => h.Calendar)
            .WithMany(c => c.Holidays)
            .HasForeignKey(h => h.CalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CalendarHoliday>()
            .HasIndex(h => new { h.CalendarId, h.Date });

        modelBuilder.Entity<Department>()
            .HasOne(d => d.WorkCalendar)
            .WithMany()
            .HasForeignKey(d => d.WorkCalendarId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<LeaveRequest>()
            .HasOne(l => l.Employee)
            .WithMany()
            .HasForeignKey(l => l.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LeaveRequest>()
            .HasOne(l => l.DecidedBy)
            .WithMany()
            .HasForeignKey(l => l.DecidedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LeaveRequest>()
            .HasIndex(l => new { l.EmployeeId, l.State, l.StartDate, l.EndDate });

        modelBuilder.Entity<Timesheet>()
            .HasOne(t => t.Employee)
            .WithMany()
            .HasForeignKey(t => t.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Timesheet>()
            .HasOne(t => t.ReviewedBy)
            .WithMany()
            .HasForeignKey(t => t.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Timesheet>()
            .HasIndex(t => new { t.EmployeeId, t.WeekStart })
            .IsUnique();

        modelBuilder.Entity<Timesheet>()
            .HasIndex(t => t.State);

        modelBuilder.Entity<TaskTimeEntry>()
            .HasOne(e => e.Timesheet)
            .WithMany(t => t.Entries)
            .HasForeignKey(e => e.TimesheetId)
            .OnDelete(DeleteBehavior.SetNull);

        // ============================================
        // Part D — DailyMetricSnapshot (AN-3/AN-4)
        // ============================================
        modelBuilder.Entity<DailyMetricSnapshot>()
            .HasOne(s => s.Department)
            .WithMany()
            .HasForeignKey(s => s.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailyMetricSnapshot>()
            .HasIndex(s => new { s.DepartmentId, s.Date, s.Version })
            .IsUnique();

        modelBuilder.Entity<DailyMetricSnapshot>()
            .HasIndex(s => new { s.DepartmentId, s.Date });

        // ============================================
        // Part E — Performance Management
        // ============================================
        modelBuilder.Entity<Competency>()
            .HasIndex(c => c.Name)
            .IsUnique();

        modelBuilder.Entity<CompetencyLevelDescriptor>()
            .HasOne(d => d.Competency)
            .WithMany(c => c.LevelDescriptors)
            .HasForeignKey(d => d.CompetencyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CompetencyLevelDescriptor>()
            .HasOne(d => d.SeniorityLevel)
            .WithMany()
            .HasForeignKey(d => d.SeniorityLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CompetencyLevelDescriptor>()
            .HasIndex(d => new { d.CompetencyId, d.SeniorityLevelId })
            .IsUnique();

        modelBuilder.Entity<ReviewCycle>()
            .HasIndex(c => c.Stage);

        modelBuilder.Entity<PerformanceReview>()
            .HasOne(r => r.ReviewCycle)
            .WithMany(c => c.Reviews)
            .HasForeignKey(r => r.ReviewCycleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PerformanceReview>()
            .HasOne(r => r.Employee)
            .WithMany()
            .HasForeignKey(r => r.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PerformanceReview>()
            .HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PerformanceReview>()
            .HasIndex(r => new { r.ReviewCycleId, r.EmployeeId })
            .IsUnique();

        modelBuilder.Entity<ReviewEvidence>()
            .HasOne(e => e.PerformanceReview)
            .WithMany(r => r.Evidence)
            .HasForeignKey(e => e.PerformanceReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewAppeal>()
            .HasOne(a => a.PerformanceReview)
            .WithMany(r => r.Appeals)
            .HasForeignKey(a => a.PerformanceReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PeerInvitation>()
            .HasOne(p => p.PerformanceReview)
            .WithMany(r => r.PeerInvitations)
            .HasForeignKey(p => p.PerformanceReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PeerInvitation>()
            .HasOne(p => p.Nominee)
            .WithMany()
            .HasForeignKey(p => p.NomineeEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PeerInvitation>()
            .HasIndex(p => p.Token)
            .IsUnique();

        modelBuilder.Entity<PeerFeedback>()
            .HasOne(f => f.PeerInvitation)
            .WithOne(p => p.Feedback)
            .HasForeignKey<PeerFeedback>(f => f.PeerInvitationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Goal>()
            .HasOne(g => g.Owner)
            .WithMany()
            .HasForeignKey(g => g.OwnerEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Goal>()
            .HasOne(g => g.Manager)
            .WithMany()
            .HasForeignKey(g => g.ManagerEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoalVersion>()
            .HasOne(v => v.Goal)
            .WithMany(g => g.Versions)
            .HasForeignKey(v => v.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GoalTaskLink>()
            .HasOne(l => l.Goal)
            .WithMany(g => g.TaskLinks)
            .HasForeignKey(l => l.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GoalTaskLink>()
            .HasIndex(l => new { l.GoalId, l.TaskId })
            .IsUnique();

        modelBuilder.Entity<FeedbackNote>()
            .HasOne(n => n.FromEmployee)
            .WithMany()
            .HasForeignKey(n => n.FromEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FeedbackNote>()
            .HasOne(n => n.ToEmployee)
            .WithMany()
            .HasForeignKey(n => n.ToEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employee>()
            .Property(e => e.JoinedAt)
            .HasDefaultValue(new DateOnly(2020, 1, 1));

        // PL-1/PL-3: one record per (caller, key); expired records are purged by the retention job.
        modelBuilder.Entity<IdempotencyRecord>()
            .HasIndex(r => new { r.CallerId, r.Key })
            .IsUnique();
        modelBuilder.Entity<IdempotencyRecord>()
            .HasIndex(r => r.ExpiresAt);

        // PL-11/PL-12: API keys — unique hash index for O(1) lookup on every authenticated request.
        modelBuilder.Entity<ApiKey>()
            .HasOne(k => k.Owner)
            .WithMany()
            .HasForeignKey(k => k.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ApiKey>()
            .HasIndex(k => k.KeyHash)
            .IsUnique();
        modelBuilder.Entity<ApiKey>()
            .HasIndex(k => k.OwnerId);

        // PL-13/PL-14: webhook subscriptions and delivery records.
        modelBuilder.Entity<WebhookSubscription>()
            .HasOne(s => s.Owner)
            .WithMany()
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WebhookDelivery>()
            .HasOne(d => d.Subscription)
            .WithMany()
            .HasForeignKey(d => d.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<WebhookDelivery>()
            .HasIndex(d => d.SubscriptionId);
        modelBuilder.Entity<WebhookDelivery>()
            .HasIndex(d => d.State);

        // OB-1: singleton worker heartbeat row (Id = 1).
        modelBuilder.Entity<WorkerHeartbeat>()
            .HasKey(h => h.Id);

        modelBuilder.Entity<AuthSession>()
            .HasIndex(s => s.RefreshTokenHash);
        modelBuilder.Entity<AuthSession>()
            .HasIndex(s => s.UserId);
        modelBuilder.Entity<AuthSession>()
            .HasIndex(s => s.FamilyId);
        modelBuilder.Entity<LoginThrottle>()
            .HasIndex(t => t.Key)
            .IsUnique();
    }

    // WC-27: closed status intervals are immutable.
    public override int SaveChanges()
    {
        EnforceStatusIntervalImmutability();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceStatusIntervalImmutability();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceStatusIntervalImmutability()
    {
        foreach (var entry in ChangeTracker.Entries<TaskStatusInterval>())
        {
            if (entry.State == EntityState.Modified)
            {
                var originalExited = entry.Property(i => i.ExitedAt).OriginalValue;
                if (originalExited != null)
                    throw new InvalidOperationException("Closed status intervals cannot be modified (WC-27).");
            }

            if (entry.State == EntityState.Deleted && entry.Entity.ExitedAt != null)
                throw new InvalidOperationException("Closed status intervals cannot be deleted (WC-27).");
        }
    }
}
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Interfaces;

public interface ILeaveService
{
    Task<LeaveRequest> RequestLeaveAsync(int employeeId, string type, DateOnly start, DateOnly end, bool partialDay, string? note);
    Task<LeaveRequest> DecideAsync(int leaveId, int actorEmployeeId, bool approve, string? decisionNote);
    Task<LeaveRequest> CancelAsync(int leaveId, int actorEmployeeId);
    Task<IReadOnlyList<object>> ListVisibleAsync(int actorEmployeeId, string actorRole, int? employeeIdFilter = null);
    Task<IReadOnlyList<LeaveRequest>> GetPendingApprovalsAsync(int managerEmployeeId);
    Task<bool> IsOnApprovedLeaveAsync(int employeeId, DateOnly date);
}

public interface ICapacityService
{
    Task<object> GetTeamAvailabilityAsync(int departmentId, DateOnly from, DateOnly to, int actorEmployeeId, string actorRole);
    Task<object> GetWorkloadAsync(int departmentId, DateOnly from, DateOnly to, int actorEmployeeId, string actorRole, double overheadPercent);
}

public interface ITimesheetService
{
    Task<object> GetWeekAsync(int employeeId, DateOnly weekStart, int actorEmployeeId, string actorRole);
    Task EnsureTimeEntryEditableAsync(int employeeId, DateOnly workedOn);
    Task LinkEntryToWeekAsync(int employeeId, int timeEntryId, DateOnly workedOn);
    Task<Timesheet> SubmitWeekAsync(int employeeId, DateOnly weekStart);
    Task<Timesheet> ReviewAsync(int timesheetId, int reviewerEmployeeId, bool approve, string? note);
    Task<Timesheet> ReopenAsync(int timesheetId, int reviewerEmployeeId, string reason);
    Task<IReadOnlyList<object>> MineAsync(int employeeId);
    Task<IReadOnlyList<object>> PendingReviewAsync(int managerEmployeeId);

    Task<object> UploadAttachmentAsync(int employeeId, DateOnly monthStart, string fileName, string? contentType, Stream content, long sizeBytes);
    Task<TimesheetAttachmentDownload> DownloadAttachmentAsync(int attachmentId, int actorEmployeeId, string actorRole);
    Task DeleteAttachmentAsync(int attachmentId, int actorEmployeeId);
}

public record TimesheetAttachmentDownload(string FileName, string ContentType, Stream Content);

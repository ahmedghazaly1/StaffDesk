using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ITimesheetRepository
{
    Task<Timesheet> GetOrCreateOpenWeekAsync(int employeeId, DateOnly weekStart);
    Task<Timesheet?> GetByIdAsync(int id);
    Task UpdateAsync(Timesheet timesheet);
    Task AttachTimeEntryAsync(int timeEntryId, int timesheetId);
    Task<IReadOnlyList<Timesheet>> GetForEmployeeAsync(int employeeId);
    Task<IReadOnlyList<Timesheet>> GetSubmittedForReviewerAsync(IEnumerable<int> reportEmployeeIds);

    Task<TimesheetAttachment> AddAttachmentAsync(TimesheetAttachment attachment, byte[] bytes);
    Task<TimesheetAttachment?> GetAttachmentAsync(int attachmentId);
    Task<byte[]?> GetAttachmentBytesAsync(int attachmentId);
    Task RemoveAttachmentAsync(TimesheetAttachment attachment);
}

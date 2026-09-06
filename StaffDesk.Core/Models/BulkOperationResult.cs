namespace StaffDesk.Core.Models;

public class BulkOperationResult
{
    public List<int> Successful { get; set; } = new List<int>();
    public List<BulkOperationFailure> Failed { get; set; } = new List<BulkOperationFailure>();
    /// <summary>CP-7: non-blocking advisory per task id, e.g. "Assignee is on approved leave today".</summary>
    public Dictionary<int, string> Warnings { get; set; } = new Dictionary<int, string>();
    public int TotalProcessed => Successful.Count + Failed.Count;
    public int TotalSuccessful => Successful.Count;
    public int TotalFailed => Failed.Count;
}

public class BulkOperationFailure
{
    public int TaskId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
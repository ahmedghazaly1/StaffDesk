namespace StaffDesk.Core.Entities;

/// <summary>OB-1: worker liveness signal — updated each poll cycle by StaffDesk.Worker.</summary>
public class WorkerHeartbeat
{
    public int Id { get; set; } = 1;
    public string InstanceId { get; set; } = string.Empty;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}

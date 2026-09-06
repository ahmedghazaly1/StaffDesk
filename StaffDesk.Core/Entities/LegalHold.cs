namespace StaffDesk.Core.Entities;

/// <summary>DG-4: exempts an employee or task from purge and erasure until lifted.</summary>
public class LegalHold
{
    public long Id { get; set; }
    public string TargetType { get; set; } = string.Empty; // Employee | Task
    public string TargetId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int PlacedById { get; set; }
    public Employee? PlacedBy { get; set; }
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    public int? LiftedById { get; set; }
    public Employee? LiftedBy { get; set; }
    public DateTime? LiftedAt { get; set; }

    public bool IsActive => LiftedAt == null;
}

public static class LegalHoldTargetTypes
{
    public const string Employee = "Employee";
    public const string Task = "Task";
}

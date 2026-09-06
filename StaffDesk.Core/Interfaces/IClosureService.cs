using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IClosureService
{
    // Record task outcome (WC-15)
    Task<TaskOutcome> SetOutcomeAsync(int taskId, string outcome, string? note, string? externalReference, string? externalReferenceLabel, int userId);
    
    // Get outcome for a task
    Task<TaskOutcome?> GetOutcomeByTaskIdAsync(int taskId);
    
    // Get all outcomes for a task
    Task<IEnumerable<TaskOutcome>> GetOutcomesByTaskIdAsync(int taskId);
    
    // Process task closure when moving to DONE (WC-17)
    Task<TaskClosure> ProcessClosureAsync(int taskId, int userId, string? closureNote = null);
    
    // Get closure for a task
    Task<TaskClosure?> GetClosureByTaskIdAsync(int taskId);
    
    // Check if task has closure
    Task<bool> HasClosureAsync(int taskId);
}
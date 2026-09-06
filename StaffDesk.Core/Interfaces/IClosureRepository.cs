using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IClosureRepository
{
    // Outcome
    Task<TaskOutcome> CreateOutcomeAsync(TaskOutcome outcome);
    Task<TaskOutcome?> GetOutcomeByTaskIdAsync(int taskId);
    Task<IEnumerable<TaskOutcome>> GetOutcomesByTaskIdAsync(int taskId);
    
    // Closure
    Task<TaskClosure> CreateClosureAsync(TaskClosure closure);
    Task<TaskClosure?> GetClosureByTaskIdAsync(int taskId);
    Task<bool> HasClosureAsync(int taskId);
}
using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;

namespace StaffDesk.Infrastructure.Data;

// WC-26: one-time backfill of TaskStatusInterval rows for any WorkTask that predates this feature
// (i.e. has no interval rows yet). Called once at startup alongside DbSeeder's other seed calls -
// idempotent, since it only ever looks at tasks with zero existing intervals.
public static class TaskStatusIntervalBackfill
{
    private static readonly HashSet<string> TerminalStatuses = new() { "DONE", "CANCELLED" };

    public static void Backfill(AppDbContext context)
    {
        var taskIdsWithIntervals = context.TaskStatusIntervals
            .Select(i => i.TaskId)
            .Distinct()
            .ToList();

        var tasksNeedingBackfill = context.Tasks
            .Where(t => !taskIdsWithIntervals.Contains(t.Id))
            .ToList();

        if (tasksNeedingBackfill.Count == 0) return;

        foreach (var task in tasksNeedingBackfill)
        {
            var isTerminal = TerminalStatuses.Contains(task.Status);

            var interval = new TaskStatusInterval
            {
                TaskId = task.Id,
                Status = task.Status,
                EnteredAt = task.CreatedAt,
                ExitedAt = isTerminal ? task.UpdatedAt : null,
                ActorId = task.CreatedById,
                IsEstimated = true,
                DurationSeconds = isTerminal
                    ? (int)Math.Max(0, (task.UpdatedAt - task.CreatedAt).TotalSeconds)
                    : null
            };

            context.TaskStatusIntervals.Add(interval);
        }

        context.SaveChanges();
    }
}

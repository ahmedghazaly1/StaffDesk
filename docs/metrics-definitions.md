# Canonical metric definitions (Appendix A as implemented)

Source of truth for managers: how StaffDesk computes each figure. Duration metrics are p50/p85/p95 with `n`. Ratios include numerator and denominator. Snapshots that include backfilled status intervals (WC-26) set `includesBackfilled`.

| Metric | Implementation |
|--------|----------------|
| **Triage latency** | Working (or elapsed) minutes from request `SubmittedAt` to `TriageDecidedAt`. p50/p85/p95 on GET `/analytics/flow` (`triageLatency`). |
| **Lead time** | `completedAt − createdAt` for tasks reaching DONE. Working-calendar minutes via `IWorkingCalendarService`. |
| **Cycle time** | `completedAt − first IN_PROGRESS entry` (status intervals). |
| **Reaction time** | First assignment instant − `createdAt`. |
| **Time in status** | Sum of `TaskStatusInterval` durations per status. |
| **Blocked time** | Sum of intervals in `BLOCKED`. |
| **Flow efficiency** | Time in `IN_PROGRESS` ÷ lead time, as a percentage. |
| **Throughput** | Count of transitions into DONE in the period (snapshot `Throughput`). |
| **Arrival rate** | Count of tasks created in the period. |
| **WIP** | Count of tasks in an active status at snapshot time. |
| **Aging WIP** | Now − first `IN_PROGRESS` for still-active tasks. |
| **Backlog size** | OPEN tasks plus untriaged requests. |
| **On-time completion** | DONE with `completedAt ≤ dueAt` ÷ DONE with a due date set. Undated tasks excluded. |
| **SLA attainment** | Tasks meeting resolution target ÷ tasks with an SLA policy. |
| **Rework rate** | `IN_REVIEW → IN_PROGRESS` count ÷ completed. |
| **Reopen rate** | Tasks with ≥1 `DONE → OPEN` ÷ completed. |
| **First-pass yield** | Completed with zero rework and zero reopens ÷ completed. |
| **Estimate accuracy** | Distribution of `loggedMinutes ÷ estimateMinutes` (not a mean). |
| **Utilisation** | Committed load ÷ capacity; always returned with unestimated-task count (CP-13). Overhead % is a request parameter (default 20% in snapshots). |
| **Carry-over rate** | Still-open at period end that were open at start ÷ open at start. |

GET analytics endpoints read **precomputed** `DailyMetricSnapshot` rows only (AN-8). Rebuild: `dotnet run --project StaffDesk.Worker -- rebuild-metrics yyyy-MM-dd yyyy-MM-dd [departmentId]`.

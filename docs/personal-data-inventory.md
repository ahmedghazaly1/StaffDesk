# Personal Data Inventory (DG-6)

Classification of fields that hold personal data in StaffDesk. Lawful purpose is internal workforce operations under employment/contract. Retention class maps to `RetentionPolicy.DataClass` rows (DG-1).

| Field / record | Entity | Classification | Lawful purpose | Retention class |
|----------------|--------|----------------|----------------|-----------------|
| FullName | Employee | Identifying | Directory / assignment | (lives with employee; erasure pseudonymises) |
| JobTitle | Employee | Employment | Org structure | (with employee) |
| Email, Username | User | Contact / Identifying | Authentication | session_records / account lifetime |
| PasswordHash | User | Authentication secret | Auth | Never exported; erased on DG-8 |
| ManagerId / LevelId / DepartmentId | Employee | Employment | Hierarchy | (with employee) |
| Comment Body | TaskComment | Behavioural / free text | Collaboration | task_activity |
| Time entry Note | TaskTimeEntry | Behavioural | Time tracking | task_activity |
| Notification Message | Notification | Behavioural | Alerts | notifications |
| Audit ActorLabel, SourceIp, UserAgent | AuditEvent | Identifying / behavioural | Security audit | audit_events |
| Leave type, note | LeaveRequest | Health-adjacent | Absence | (with leave request; redacted for non-HR) |
| Performance ratings, justifications | PerformanceReview | Performance | HR cycles | closed_performance_reviews |

## Rules

- **DG-10:** Application logs must use numeric ids (`employeeId`, `userId`, `requestId`), never names, emails, or free-text bodies.
- **DG-7:** Subject access export assembles profile, tasks, comments, time entries, notifications, and audit events naming the employee.
- **DG-8:** Erasure pseudonymises identifying fields and redacts authored free text; it does not destroy aggregate history.
- **DG-9:** Erasure and purge skip targets under an active legal hold.

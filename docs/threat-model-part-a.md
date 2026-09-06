# Threat model — Part A (Audit & governance)

One page. Assets, actors, trust boundary, attacks, controls.

| Asset | Why it matters |
|-------|----------------|
| Audit event chain | Legal / security evidence; tampering must be detectable |
| Personal data in comments, leave, sessions | Subject access and erasure obligations |
| Purge / erasure jobs | Irreversible deletion |

| Actor | Trust |
|-------|--------|
| Authenticated employee | Inside the app; scoped by role |
| AUDITOR | Read audit log only |
| ADMIN | Operations, not a substitute for HR |
| Operator with DB access | Outside the app; hash chain is the detection control |
| Anonymous network | Login / rate limits only |

Trust boundary: HTTP API. The database is trusted for availability, not for integrity of the audit chain.

| Attack | Control |
|--------|---------|
| Forge or delete audit rows | Hash chain + `verify-audit`; verification fails on a corrupted row |
| Read records the role cannot see | Field redaction; 404 on hidden performance/leave; PERMISSION_DENIED audited |
| Purge evidence under investigation | Legal hold skips employee/task-linked purge classes and erasure |
| Flood login | Progressive delay, ACCOUNT_LOCKED, rate-limit 429 + RATE_LIMIT_TRIGGERED |
| Steal signing secret from config | `STAFFDESK_JWT_SECRET` required; refuse empty default |

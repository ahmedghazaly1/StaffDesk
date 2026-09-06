# Configuration catalogue (NFR-23)

All tunables live in `StaffDesk.API/appsettings.json` (and environment overrides). Do not add new magic numbers in code.

| Section | Keys | Used by |
|---------|------|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL | API, Worker |
| `JwtSettings` | `Issuer`, `Audience`, `ExpiryMinutes` | Access tokens. **Signing secret is `STAFFDESK_JWT_SECRET` only (SP-11).** |
| `Auth` | `AccessTokenMinutes`, `RefreshTokenDays`, `LockoutAfterFailures`, `LockoutMinutes`, `DelayBaseMs`, `DelayMaxMs` | Sessions, lockout (SP-1, SP-5) |
| `RateLimits` | `AuthPerMinute`, `ExportPerMinute`, `WritePerMinute`, `ReadPerMinute` | PL-9/PL-10 |
| `Retention` | `AuditEventsDays`, `TaskActivityDays`, `NotificationsDays`, `SoftDeletedTasksDays`, `SessionRecordsDays` | DG-1 purge policies |
| `Operations` | worker heartbeat age, queue age, rollup staleness, HTTP error %, dead-job count | OB-4 alerts |

SLA policy defaults remain data in `SlaPolicies` (admin UI), not compiled constants. Analytics suppression minimum is `InsufficientData.DefaultThreshold` in Core (documented exception: a named constant, not a scattered literal).

# Threat model — Part E (Performance)

One page. Assets, actors, trust boundary, attacks, controls.

| Asset | Why it matters |
|-------|----------------|
| Self / manager ratings and justifications | Career impact; must not leak before the stage allows |
| Peer feedback | Attribution rules; aggregated vs named |
| Calibration pre-change values | Audit of rating changes |
| Quality indicators (rework/reopen) | Must never be presented as a person-score |

| Actor | Trust |
|-------|--------|
| Employee (subject) | Own review after stage gates |
| Assigned manager | Stage-appropriate writes |
| HR_ADMIN | Cycle machine, calibration, appeals |
| ADMIN / AUDITOR | **No** performance record access (PM-38) |
| Unrelated manager | No read of reports they did not review |

Trust boundary: performance controllers and `PerformanceService`. Analytics dashboards are operational metrics, not ratings.

| Attack | Control |
|--------|---------|
| ADMIN browsing reviews | `DenyAdminAuditor` on every performance API; tests |
| Employee reading manager draft | `CanSeeManager` only after publication |
| Manager reading self-assessment before submit | `CanSeeSelf` only after `SelfSubmitted` |
| Rating without a written reason | Justification required on submit (≥200 chars) |
| Silent calibration overwrite | Store `PreCalibrationOverallRating` |
| Peer reading another person’s review | GetReview returns 404 + audited DENIED |

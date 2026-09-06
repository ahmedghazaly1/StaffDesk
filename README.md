# StaffDesk

StaffDesk is a role-based workforce management platform for running the day-to-day operations of an organization: task and project tracking, employee and department administration, leave and timesheet management, performance reviews, SLA and capacity planning, and a tamper-evident audit trail for compliance.

**Live demo:** https://staffdesk-production.up.railway.app/login.html

## Features

- **Tasks & Task Requests** — full task board with filters, bulk actions, templates, recurring tasks, and an informal request/triage queue.
- **People & Departments** — employee directory, departments, seniority levels, and manager delegation.
- **Time** — leave & absence tracking, weekly timesheets, capacity & availability planning, and configurable working calendars.
- **Performance Management** — self/manager assessments, review cycles, calibration, goals, peer feedback, and a shared competency framework.
- **Analytics & Reporting** — personal, department, flow, and organization-wide reporting with CSV export.
- **Platform & Operations** — API keys, webhooks, SLA policies, and a live system health dashboard (job queue, HTTP errors, audit chain integrity).
- **Security & Audit** — session management, role-based access control, and a hash-chained, tamper-evident audit log.
- **Localization** — full English/Arabic support with right-to-left layout.

## Roles

| Role | Access |
|---|---|
| Admin | Full organization-wide access — people, departments, planning, analytics, platform, and system health |
| Manager | Team-level task, leave, and performance oversight |
| HR Admin | Performance review cycles, calibration, and the competency library |
| Auditor | Read-only access to the full audit trail |
| Member | Personal task list, timesheets, leave requests, and performance reviews |

## Tech Stack

- **Backend:** ASP.NET Core (.NET 8), Entity Framework Core
- **Database:** PostgreSQL
- **Frontend:** Vanilla JavaScript, served as static files from the API
- **Background jobs:** A separate worker process (`StaffDesk.Worker`) for recurring tasks, analytics rollups, and audit chain checks

## Project Structure

```
StaffDesk.API/             ASP.NET Core Web API + static frontend (wwwroot)
StaffDesk.Core/             Domain entities, interfaces, and services
StaffDesk.Infrastructure/   EF Core, repositories, and PostgreSQL data access
StaffDesk.Worker/           Background job processor
StaffDesk.Tests/            Automated tests
Dockerfile                  Builds and runs StaffDesk.API
Dockerfile.worker           Builds and runs StaffDesk.Worker
```

## Running Locally

**Prerequisites:** .NET 8 SDK, PostgreSQL

```bash
# Restore dependencies
dotnet restore

# Apply database migrations (from the API project, or let it auto-migrate on startup)
dotnet ef database update --project StaffDesk.Infrastructure --startup-project StaffDesk.API

# Run the API (also serves the frontend)
dotnet run --project StaffDesk.API
```

The frontend will be available at `http://localhost:<port>/login.html`.

### Required environment variables

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string (`Host=...;Port=...;Database=...;Username=...;Password=...`) |
| `STAFFDESK_JWT_SECRET` | Signing secret for auth tokens — must be a random value of at least 32 characters, never a default/known value |

## Deployment

This project is deployed on [Railway](https://railway.com) as two services sharing one PostgreSQL database:

- **API service** — built from the root `Dockerfile`, serves both the backend and frontend.
- **Worker service** — built from `Dockerfile.worker` (set via the `RAILWAY_DOCKERFILE_PATH` environment variable), runs background jobs.

Both services require the `ConnectionStrings__DefaultConnection` and `STAFFDESK_JWT_SECRET` environment variables described above.

## License

Internal / private project — no license currently specified.

# ADR-0007: Use BackgroundService with PostgreSQL claims for scheduled work

> **Status:** Accepted — retrospective documentation of the implemented architecture  
> **Recorded:** 2026-11-04 · **Scope:** Bookify.Services.Booking backend · **Module:** 24.7  
> **Decision date:** Not independently established. The recorded date documents the decision; it is not claimed to be the original adoption date.

## Context

Bookify must expire overdue booking approvals and payments, complete eligible stays, dispatch Outbox notifications and issue two types of reminders. These operations are currently hosted in the API process. Introducing a scheduler platform before the product needs recurring dashboards or complicated cron rules would add deployment and operational weight.

## Decision

Use an ASP.NET Core `BackgroundService` named `BookingBackgroundWorker`, with a configurable polling interval and batch size. Its current ordered job keys are: **ApprovalExpiration, PaymentExpiration, BookingCompletion, OutboxNotification, PaymentDeadlineReminder, UpcomingCheckInReminder**. Resolve each job in its own DI scope and use PostgreSQL-backed claims to reduce concurrent duplicate processing between workers. Jobs handle failures individually rather than aborting every later job in the cycle.

## Alternatives considered (retrospective)

The following are trade-off analyses, **not** a claim that a historical design meeting formally voted on every option.

| Alternative | Trade-off for Bookify |
| --- | --- |
| HTTP-only/manual background execution | No timely expiration or automated notifications. |
| Hangfire or Quartz from day one | Job dashboards and richer schedules, but adds maintenance and storage conventions not required by present jobs. |
| Dedicated scheduler service now | Stronger deployment isolation, but adds service lifecycle and deployment complexity. |

## Consequences and trade-offs

**Benefits:** small operational footprint and reuse of the database already required by the API. **Costs:** work competes for API process resources; polling may introduce latency; no built-in historical job dashboard. **Scope:** not a separate deployed worker in the current solution. The existing defaults are a 10-second polling interval and a batch size of 50; background jobs can be disabled via `BackgroundJobs:Enabled`.

## Verification and review triggers

- Validate disabled/enabled behavior and expired-booking safety with existing tests.
- Monitor processing failures, polling delays and claim contention.
- Reevaluate a scheduler if persistent scheduling, a dedicated operations UI, complex cron expressions or process isolation become real requirements.
- Document current runtime prerequisites in the root README (24.6).

## Source of truth

- [`BookingBackgroundWorker.cs`](../../../src/Bookify.Services.Booking.Api/BackgroundJobs/BookingBackgroundWorker.cs) — six jobs, scopes, polling and telemetry.
- [`BackgroundJobsOptions.cs`](../../../src/Bookify.Services.Booking.Api/BackgroundJobs/BackgroundJobsOptions.cs) — worker defaults.
- [`BackgroundJobsDependencyInjection.cs`](../../../src/Bookify.Services.Booking.Api/BackgroundJobs/BackgroundJobsDependencyInjection.cs) — hosted service registration.
- [`PaymentExpirationJob.cs`](../../../src/Bookify.Services.Booking.Application/BackgroundJobs/Expiration/PaymentExpirationJob.cs) — deadline processing.
- [`PostgreSqlBackgroundJobClaimManager.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Persistence/Concurrency/PostgreSqlBackgroundJobClaimManager.cs) — database-backed claims.

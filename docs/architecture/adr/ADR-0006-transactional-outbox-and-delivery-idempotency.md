# ADR-0006: Persist domain-event Outbox records and protect notification delivery

> **Status:** Accepted — retrospective documentation of the implemented architecture  
> **Recorded:** 2026-11-04 · **Scope:** Bookify.Services.Booking backend · **Module:** 24.7  
> **Decision date:** Not independently established. The recorded date documents the decision; it is not claimed to be the original adoption date.

## Context

Booking lifecycle transitions emit domain events. Sending SMTP inside a booking transaction would bind database correctness to a network operation. Conversely, an in-memory-only event handler could lose a notification when the API crashes between commit and delivery. Bookify has Outbox tables, a retryable notification job, delivery records and a domain-event dispatcher.

## Decision

Preserve the **Outbox pattern** for durable notification work: stage event messages through `BookingDbContext`, persist them with the associated aggregate transaction when that saving path is used, then let `OutboxNotificationJob` select and process pending messages asynchronously. Keep an explicit delivery ledger (`notification_deliveries`) for idempotent notification handling, plus retry, deferral and terminal failure behavior.

**Do not conflate two mechanisms:** `EfCoreTransactionManager.CommitAsync` dispatches in-memory domain events after commit, while the Outbox job operates on persisted records. They serve different purposes and are not an at-most-once SMTP guarantee.

## Alternatives considered (retrospective)

The following are trade-off analyses, **not** a claim that a historical design meeting formally voted on every option.

| Alternative | Trade-off for Bookify |
| --- | --- |
| Send SMTP inside the booking transaction | Tight coupling, long locks and external-provider failure can block core writes. |
| In-memory event dispatch only | Simple but does not survive process termination before delivery. |
| External broker as the only persistence | Adds infrastructure and dual-write consistency issues without a transactional integration plan. |

## Consequences and trade-offs

**Benefits:** decoupled notification processing, retry capability and documented dead-letter behavior. **Costs:** eventual delivery, duplicate-send risk across crashes, monitoring of pending/dead-letter records, and careful transaction boundaries. **Important:** `BookingDbContext` includes both overridden EF `SaveChangesAsync` paths and an explicit `IUnitOfWork.SaveChangesAsync`; never infer exact behavior merely from one overload without following dispatch and integration tests. The existing `OutboxPersistenceIntegrationTests` explicitly assert one persisted event after repeated saves and no event after rollback.

## Verification and review triggers

- Retain the integration assertions for commit-once, rollback and concurrent workers.
- Review the call path and the Outbox row in tests if EF or Unit-of-Work signatures change.
- Confirm disabled SMTP is handled as a deferred delivery, not a successful email.
- Reevaluate a separate broker only if scale or additional consumers justify the operational burden.
- Include a documentary consistency check in 24.8; full durability regression remains the responsibility of the existing integration suite.

## Source of truth

- [`BookingDbContext.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Persistence/BookingDbContext.cs) — event staging and EF save overloads.
- [`EfCoreTransactionManager.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Persistence/Transactions/EfCoreTransactionManager.cs) — post-commit dispatch.
- [`OutboxNotificationJob.cs`](../../../src/Bookify.Services.Booking.Application/BackgroundJobs/Notifications/OutboxNotificationJob.cs) — retry and event-to-notification mapping.
- [`BookingNotificationService.cs`](../../../src/Bookify.Services.Booking.Application/Notifications/BookingNotificationService.cs) — notification delivery ledger.
- [`OutboxPersistenceIntegrationTests.cs`](../../../tests/Bookify.Services.Booking.Integration.Tests/Outbox/OutboxPersistenceIntegrationTests.cs) — commit and rollback assertions.
- [`OutboxNotificationJobIntegrationTests.cs`](../../../tests/Bookify.Services.Booking.Integration.Tests/BackgroundJobs/OutboxNotificationJobIntegrationTests.cs) — retry and concurrency coverage.

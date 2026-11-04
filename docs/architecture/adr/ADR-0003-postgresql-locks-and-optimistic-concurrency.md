# ADR-0003: Combine PostgreSQL row locks with optimistic concurrency

> **Status:** Accepted — retrospective documentation of the implemented architecture  
> **Recorded:** 2026-11-04 · **Scope:** Bookify.Services.Booking backend · **Module:** 24.7  
> **Decision date:** Not independently established. The recorded date documents the decision; it is not claimed to be the original adoption date.

## Context

Availability and payments are concurrency-sensitive: concurrent clients may request overlapping inventory, initiate the same payment or race payment expiration with webhook reconciliation. A read-then-write check without isolation cannot guarantee correctness. Bookify already uses PostgreSQL transactions, property-scoped inventory locks and booking-scoped payment-initiation locks.

## Decision

Use a **transactional PostgreSQL `SELECT ... FOR UPDATE`** lock on the property row while creating bookings, then evaluate inventory conflicts and persist the result within that transaction. Use a corresponding booking-row lock for payment initiation, cancellation and related reconciliation paths. Preserve booking optimistic concurrency checks and map detected conflicts into the existing `Result`/HTTP contract.

This is **database-coordinated concurrency**, not a distributed lock service. The transaction manager currently begins transactions at `ReadCommitted` isolation.

## Alternatives considered (retrospective)

The following are trade-off analyses, **not** a claim that a historical design meeting formally voted on every option.

| Alternative | Trade-off for Bookify |
| --- | --- |
| Only optimistic concurrency | Can detect some lost updates but cannot alone serialize all competing availability decisions. |
| Serializable transactions everywhere | Stronger isolation at the cost of retries and more transaction aborts. |
| Redis or distributed mutex | Another failure domain and a correctness problem if lock and database commit are not coordinated. |

## Consequences and trade-offs

**Benefits:** one authoritative locking system co-located with durable state; robust protection for inventory and payment races. **Costs:** lock waits, potential contention for a popular property and care around lock ordering. **Invariant:** booking availability must use the same transaction that holds the relevant property lock. **Caution:** a lock is not a substitute for validation of date ranges, unit type or aggregate state.

## Verification and review triggers

- Preserve concurrency integration tests for overlapping room/entire-property bookings and competing payment operations.
- Review lock ordering if new transactions acquire multiple row locks.
- Reevaluate if measured contention or horizontal scaling makes property-level serialization a bottleneck.
- Keep the relevant HTTP `409` conflict descriptions correct in 24.8.

## Source of truth

- [`PostgreSqlBookingInventoryLock.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Persistence/Concurrency/PostgreSqlBookingInventoryLock.cs) — property row FOR UPDATE.
- [`PostgreSqlPaymentInitiationLock.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Persistence/Concurrency/PostgreSqlPaymentInitiationLock.cs) — booking row FOR UPDATE.
- [`EfCoreTransactionManager.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Persistence/Transactions/EfCoreTransactionManager.cs) — transaction isolation and boundaries.
- [`CreateBookingCommandHandler.cs`](../../../src/Bookify.Services.Booking.Application/Bookings/Create/CreateBookingCommandHandler.cs) — lock-before-availability flow.
- [`ExpireBookingPaymentCommandHandler.cs`](../../../src/Bookify.Services.Booking.Application/Bookings/ExpirePayment/ExpireBookingPaymentCommandHandler.cs) — payment-expiration coordination.

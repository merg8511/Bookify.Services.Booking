# ADR-0001: Use EF Core for transactional writes and Dapper for specialized reads

> **Status:** Accepted — retrospective documentation of the implemented architecture  
> **Recorded:** 2026-11-04 · **Scope:** Bookify.Services.Booking backend · **Module:** 24.7  
> **Decision date:** Not independently established. The recorded date documents the decision; it is not claimed to be the original adoption date.

## Context

Bookify stores properties, units, bookings, payments, idempotency keys, webhook events, Outbox messages and notification delivery records in PostgreSQL. Its command handlers mutate aggregates using repositories and a Unit of Work. Its query handlers depend on read-service interfaces with Dapper implementations. Availability and administrative listings need carefully shaped SQL and predictable projection costs. Both approaches use the same physical PostgreSQL database.

## Decision

Keep **EF Core** as the write-side object/transaction mapper and **Dapper** for specialized, read-optimized SQL. Maintain persistence abstractions in Application; resolve the implementations in Infrastructure. Do not introduce another read database, a message bus or asynchronous read-model replication just to call this CQRS.

Migrations and aggregate persistence live with the EF Core model; Dapper queries must track schema changes and return Application read models rather than tracked domain entities.

## Alternatives considered (retrospective)

The following are trade-off analyses, **not** a claim that a historical design meeting formally voted on every option.

| Alternative | Trade-off for Bookify |
| --- | --- |
| EF Core for both writes and reads | One tool and simpler maintenance, but less explicit control for complex, high-volume projections. |
| Dapper for both writes and reads | Greater SQL control, but substantial manual aggregate mapping and unit-of-work management. |
| Separate write/read databases | Potential independent scaling with eventual consistency, but unjustified synchronization and operational complexity at the current scale. |

## Consequences and trade-offs

**Benefits:** EF Core handles aggregate changes and migrations while Dapper provides explicit joins, filtering and pagination. **Costs:** two persistence styles, SQL/schema drift risks and duplicated mapping concepts. **Consistency:** a shared database does not imply every read sees uncommitted writes; transaction and isolation boundaries still apply. **Boundary:** a Dapper read service does not bypass resource authorization enforced by the HTTP/Application flow.

## Verification and review triggers

- Preserve build and repository tests when changing aggregate mappings.
- Review Dapper SQL after every migration that affects its projections.
- Reevaluate only when query profiling, maintenance costs or scale justify a different storage topology.
- Audit API write/read contracts in **24.8 — Tests de OpenAPI y documentación**, without treating documentation tests as a substitute for persistence integration tests.

## Source of truth

- [`DependencyInjection.cs`](../../../src/Bookify.Services.Booking.Infrastructure/DependencyInjection.cs) — registration of EF repositories and Dapper read services.
- [`BookingDbContext.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Persistence/BookingDbContext.cs) — DbContext and write-side persistence.
- [`DapperAvailabilityReadService.cs`](../../../src/Bookify.Services.Booking.Infrastructure/Persistence/ReadServices/DapperAvailabilityReadService.cs) — specialized availability SQL.
- [`CreateBookingCommandHandler.cs`](../../../src/Bookify.Services.Booking.Application/Bookings/Create/CreateBookingCommandHandler.cs) — command-side Unit of Work.

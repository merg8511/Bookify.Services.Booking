# ADR-0002: Use explicit CQRS executors without a mediator library

> **Status:** Accepted — retrospective documentation of the implemented architecture  
> **Recorded:** 2026-11-04 · **Scope:** Bookify.Services.Booking backend · **Module:** 24.7  
> **Decision date:** Not independently established. The recorded date documents the decision; it is not claimed to be the original adoption date.

## Context

Bookify has a growing set of use cases: property management, availability, booking lifecycle, pricing, payments and administrative queries. Mixing direct HTTP handlers with business rules makes validation, error handling and testing harder. The project already uses `ICommandExecutor`, `IQueryExecutor`, handler interfaces and `Result` to separate transport and orchestration.

## Decision

Retain explicit, generic command/query executors and typed handlers in Application. Commands may change state and invoke Domain behavior; queries return read models and do not mutate the domain. Minimal API endpoints translate HTTP into commands/queries and map `Result` to typed success or ProblemDetails.

**CQRS means separation of responsibilities**, not necessarily separate databases, an event broker or a mediator NuGet package.

## Alternatives considered (retrospective)

The following are trade-off analyses, **not** a claim that a historical design meeting formally voted on every option.

| Alternative | Trade-off for Bookify |
| --- | --- |
| Commands and queries directly in endpoints | Less indirection initially, but would couple transport, validation and use cases. |
| MediatR or other external mediator | Standard pipeline tooling but a new library and indirection for capabilities already present. |
| Microservice/event-driven CQRS | Scalable in some systems, but introduces network contracts, distributed failure and eventual consistency. |

## Consequences and trade-offs

**Benefits:** highly explicit dependencies, isolated use-case tests, no mediator dependency and straightforward DI. **Costs:** more interfaces and registrations; cross-cutting behaviors must be maintained by Bookify. **Boundary:** handlers receive application abstractions; only the API owns HTTP response types.

## Verification and review triggers

- Keep Application free of ASP.NET Core HTTP result types.
- Ensure every new use case has a clear command/query handler and validator where applicable.
- Review this decision if cross-cutting pipeline complexity grows beyond the custom executor model.
- Confirm request/response documentation in 24.8 remains aligned with the corresponding HTTP endpoints.

## Source of truth

- [`ICommandExecutor.cs`](../../../src/Bookify.Services.Booking.Application/Abstractions/Messaging/ICommandExecutor.cs) — command execution contract.
- [`IQueryExecutor.cs`](../../../src/Bookify.Services.Booking.Application/Abstractions/Messaging/IQueryExecutor.cs) — query execution contract.
- [`DependencyInjection.cs`](../../../src/Bookify.Services.Booking.Application/DependencyInjection.cs) — explicit handler/validator registrations.
- [`CreateBookingEndpoint.cs`](../../../src/Bookify.Services.Booking.Api/Endpoints/Bookings/Create/CreateBookingEndpoint.cs) — HTTP-to-Application adapter.

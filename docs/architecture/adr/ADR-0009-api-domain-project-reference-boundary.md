# ADR-0009: Record the Api-to-Domain reference as an explicit architectural exception

> **Status:** Accepted — retrospective documentation of the implemented architecture  
> **Recorded:** 2026-11-04 · **Scope:** Bookify.Services.Booking backend · **Module:** 24.7  
> **Decision date:** Not independently established. The recorded date documents the decision; it is not claimed to be the original adoption date.

## Context

Bookify follows Clean Architecture but its API `.csproj` directly references Domain as well as Application and Infrastructure. Some API endpoint and error mapping code uses domain-level `Result`/`Error` types to translate failures into HTTP responses. A simplified architecture diagram that claims the API has no Domain reference would be inaccurate. This decision was explicitly deferred from earlier modules to the ADR documentation in **24.7**.

## Decision

**Record the as-built dependency rather than hiding it or silently refactoring it in a documentation module.** The API currently has a direct project reference to Domain. The intended long-term boundary remains: Domain contains business invariants and must not depend on Api/ASP.NET Core; Application orchestrates use cases; Infrastructure implements its ports. Transport-specific DTOs remain in Api, not Domain.

This ADR acknowledges the reference as a **documented architectural exception**. It is **not** an assertion that an API-to-Domain reference is inherently forbidden by all Clean Architecture variants, nor does it establish a new coding policy that must be applied without discussion.

## Alternatives considered (retrospective)

The following are trade-off analyses, **not** a claim that a historical design meeting formally voted on every option.

| Alternative | Trade-off for Bookify |
| --- | --- |
| Immediately remove the direct reference | Potentially tightens boundaries but requires real code, compile and test changes beyond documentation scope. |
| Pretend the reference is absent | Creates a false architecture diagram and confuses future maintainers. |
| Move domain types into Api | Reverses dependencies and risks contaminating business logic with HTTP concerns. |

## Consequences and trade-offs

**Benefits:** transparency about actual assembly dependencies and their limited purpose. **Costs:** API developers can accidentally couple HTTP contracts to additional domain internals; clear architectural review is needed. **No functionality changes:** ADR-0009 consumes the deferred documentation item, not an unapproved refactor. If dependency reduction becomes a requirement, it needs a separate, explicitly agreed roadmap change and regression plan rather than being smuggled into 24.7.

## Verification and review triggers

- Check project references and actual source usages before asserting dependency boundaries.
- Keep business rules in Domain and HTTP representations in Api.
- Review this exception whenever a new endpoint begins accepting/exposing domain entities directly.
- The 24.8 documentation review should ensure that diagrams and README do not contradict the real project references.

## Source of truth

- [`Bookify.Services.Booking.Api.csproj`](../../../src/Bookify.Services.Booking.Api/Bookify.Services.Booking.Api.csproj) — direct references to Domain, Application and Infrastructure.
- [`ErrorHttpExtensions.cs`](../../../src/Bookify.Services.Booking.Api/Extensions/ErrorHttpExtensions.cs) — translation of domain errors into ProblemDetails.
- [`CreateBookingEndpoint.cs`](../../../src/Bookify.Services.Booking.Api/Endpoints/Bookings/Create/CreateBookingEndpoint.cs) — direct use of Domain.Shared Result.
- [`ICommandExecutor.cs`](../../../src/Bookify.Services.Booking.Application/Abstractions/Messaging/ICommandExecutor.cs) — Application contract using Result.

## Notes and boundaries

**Deferred consumed:** `DEFERRED → 24.7 — ADRs` (API→Domain documentation). This is an architectural record only: it neither removes nor approves expanding the dependency across new features.

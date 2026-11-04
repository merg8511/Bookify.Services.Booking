# Architectural Decision Records (ADRs)

> **Bookify.Services.Booking** · Module **24.7** · Recorded **2026-11-04**  
> **Scope:** Decisions already reflected in the backend through Module 24.6. This directory documents the existing architecture; it does not introduce new behavior.

## Why ADRs?

An ADR records **context → decision → alternatives → consequences → review conditions**. Code and tests establish what is currently implemented; an ADR makes trade-offs understandable to the next contributor.

All decisions below are **retrospective records** of current implementation. `Accepted` describes the documented as-built approach and **does not assert** that the decisions were approved on the recording date.

## Decision index

| ID | Decision | Status | Primary boundary |
| --- | --- | --- | --- |
| [ADR-0001](ADR-0001-ef-core-and-dapper.md) | EF Core + Dapper | Accepted (retrospective) | Persistence |
| [ADR-0002](ADR-0002-explicit-cqrs-without-mediator.md) | Explicit CQRS without mediator | Accepted (retrospective) | Application |
| [ADR-0003](ADR-0003-postgresql-locks-and-optimistic-concurrency.md) | PostgreSQL locks + optimistic concurrency | Accepted (retrospective) | Consistency |
| [ADR-0004](ADR-0004-scoped-http-and-provider-idempotency.md) | HTTP/provider idempotency | Accepted (retrospective) | API + Payments |
| [ADR-0005](ADR-0005-separate-payment-attempt-and-booking-state.md) | Payment model and reconciliation | Accepted (retrospective) | Domain + Payments |
| [ADR-0006](ADR-0006-transactional-outbox-and-delivery-idempotency.md) | Outbox and notification delivery | Accepted (retrospective) | Persistence + Jobs |
| [ADR-0007](ADR-0007-backgroundservice-for-scheduled-work.md) | BackgroundService scheduler | Accepted (retrospective) | API + Jobs |
| [ADR-0008](ADR-0008-oidc-authorization-and-guest-credentials.md) | OIDC/JWT, resource authorization, Guest | Accepted (retrospective) | Security |
| [ADR-0009](ADR-0009-api-domain-project-reference-boundary.md) | API→Domain project reference | Documented architectural exception | Project dependencies |

**Deferred consumed:** The prior `DEFERRED → 24.7 — ADRs` item about the existing API→Domain dependency is addressed in ADR-0009. Documentation does not silently perform an architectural refactor.

## How to read the records

1. Start with [the architecture diagrams](../bookify-system-diagrams.md) for a view of the runtime and assembly boundaries.
2. Read ADR-0001/0002 for persistence and application structure.
3. Read ADR-0003/0004/0005 for the Booking/Payment correctness model.
4. Read ADR-0006/0007 for asynchronous work.
5. Read ADR-0008/0009 for identity and architectural boundaries.

## Adding or revising an ADR

- Assign the next sequential ID; do not renumber historical records.
- Use **Proposed**, **Accepted**, **Superseded** or **Deprecated** as appropriate. If superseded, link both directions.
- Record a new decision when the underlying behavior or trade-off actually changes; do not rewrite history to make a present decision look inevitable.
- Ground the context and decision in actual code and tests; identify inferred alternatives as retrospective analysis.
- Explain consequences, operational impact and explicit conditions for reconsideration.
- Avoid publishing secrets or client credentials in documentation.

## Related documentation

- [Project README](../../../README.md)
- [System and workflow diagrams](../bookify-system-diagrams.md)
- [REST conventions](../../api/rest-conventions.md)
- [Endpoint access matrix](../../security/endpoint-access-matrix.md)
- [Stripe CLI local webhook guide](../../payments/stripe-cli-local-webhooks.md)
- [Test coverage inventory](../../testing/test-coverage-inventory.md)

## Verification boundary

The source reference was the initial reviewed code snapshot for Module 24, plus the locally supplied Module 24.1–24.6 documentation and test results. **The user’s local branch is authoritative for any differences not yet merged**. Rendering, Markdown links, build and the Module 24.8 document/contract regression are verified separately; writing an ADR alone does not prove a passing test suite.

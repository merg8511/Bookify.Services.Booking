
# Module 22 — Existing Test Coverage Inventory

## 1. Baseline

Repository: merg8511/Bookify.Services.Booking

Branch inspected: main

Baseline commit:
6939eb6580f69e25a37914094076580102824811

Module 21 integration:
PR #49, merged 2026-10-26.

Previous reported suite:
- Passed: 1002
- Failed: 0
- Skipped: 0
- Duration: 25.4 seconds

Remote CI for the baseline commit: success.

The reported test count is historical evidence.
It is not a newly executed coverage measurement.

## 2. Test projects

| Project | C# files | Test-named files |
|---|---:|---:|
| Domain.Tests | 35 | 30 |
| Application.Tests | 55 | 45 |
| Integration.Tests | 124 | 110 |
| Total | 214 | 185 |

Test-named files are files ending in Tests.cs.

This count does not represent discovered xUnit test cases.

Framework and infrastructure:
- .NET 10
- xUnit v3
- Microsoft.NET.Test.Sdk
- coverlet.collector
- ASP.NET Core WebApplicationFactory
- Testcontainers.PostgreSql
- PostgreSQL 18.4 Alpine

## 3. Domain coverage

Existing areas:
- Result Pattern
- Value Objects
- Booking lifecycle
- Booking ownership and metadata
- Pricing and snapshots
- Availability conflict policy
- Payment and PaymentAttempt
- Payment initiation reservation
- Property and RentableUnit management
- Pricing seasons
- Domain Events

Representative tests:
- Bookings/BookingTests.cs
- Payments/PaymentTests.cs
- Payments/PaymentInitiationReservationTests.cs
- Properties/PropertyManagementTests.cs
- Properties/RentableUnitManagementTests.cs
- Properties/Pricing/ManagedPricingSeasonTests.cs

Assessment:
Existing coverage is substantial.
Scenario-level gaps for modules 15–21
must be identified before adding tests.

Destination: 22.2.

## 4. Application coverage

Existing areas:
- Booking Commands and Validators
- Payment initiation
- Payment reconciliation
- Payment cancellation
- Stripe webhook processing
- Approval and payment expiration
- Booking completion
- Notification composition
- Idempotency processing
- Domain Event dispatch
- Request execution pipeline
- Observability

Representative tests:
- Payments/Reconciliation/PaymentReconcilerTests.cs
- Payments/Initiate/InitiatePaymentCommandHandlerTests.cs
- Payments/Cancellation/PaymentCancellationCoordinatorTests.cs
- Bookings/ExpirePayment/ExpireBookingPaymentCommandHandlerTests.cs
- Notifications/BookingNotificationServiceTests.cs
- Idempotency/IdempotencyProcessorTests.cs

Assessment:
Application coverage exists.

Dedicated Application.Tests suites for
Module 21 administrative management handlers
were not identified in the repository inventory.

Evaluate additional unit coverage without
duplicating integration assertions.

Destination: 22.3.

## 5. Integration and API coverage

Existing areas:
- Booking persistence
- EF Core and Dapper
- Payment persistence
- Stripe webhook processing
- Payment initiation
- Administrative endpoints
- Authentication and authorization
- Guest booking access
- Problem Details
- Background Jobs
- Outbox persistence
- HTTP timeouts
- Observability

Representative tests:
- Payments/PaymentEndToEndTests.cs
- Endpoints/Payments/InitiatePaymentEndpointTests.cs
- Endpoints/Payments/StripePaymentWebhookEndpointTests.cs
- Endpoints/Bookings/AdminBookingWorkflowAndConcurrencyTests.cs
- ExceptionHandling/GlobalExceptionHandlerTests.cs
- Outbox/OutboxPersistenceIntegrationTests.cs

Assessment:
Existing suites should be retained.
A scenario-level gap analysis is required.

Destination: 22.4.

## 6. Concurrency coverage

Existing scenarios:
- Twenty competing booking requests
- Room versus entire-property conflicts
- Concurrent idempotency keys
- Concurrent duplicate Stripe webhooks
- Late terminal webhook observations
- PostgreSQL advisory claims
- Concurrent administrative approve/reject
- Durable payment initiation reservation

Representative tests:
- Concurrency/BookingConcurrencyTests.cs
- Idempotency/IdempotencyConcurrencyTests.cs
- Endpoints/Payments/StripePaymentWebhookConcurrencyTests.cs
- BackgroundJobs/BackgroundJobClaimManagerTests.cs
- Endpoints/Bookings/AdminBookingWorkflowAndConcurrencyTests.cs

Assessment:
Concurrency coverage exists.
It does not prove the absence of every race.

Destination: 22.6.

## 7. Security coverage

Existing scenarios include:
- JWT authorization
- Owner/Admin access
- Customer/Guest boundaries
- Cross-property isolation
- Guest access tokens
- Webhook signature rejection
- Rate limiting
- HTTP security configuration
- Sensitive data protection

Representative tests:
- Security/AdminModule21AccessMatrixTests.cs
- Security/CustomerGuestAuthorizationMatrixTests.cs
- Security/PaymentAuthorizationBoundaryTests.cs
- Endpoints/Payments/StripePaymentWebhookEndpointTests.cs
- RateLimiting/RateLimitingBehaviorTests.cs

Assessment:
Extend only uncovered security scenarios.

Destination: 22.7.

## 8. Shared test infrastructure

Existing reusable components:
- PostgreSqlTestDatabase
- BookingApiFactory
- BookingApiTestFixture
- BookingDatabaseTestSeeder
- BookingRequestTestFactory
- TestIdentityTokens
- TestIdentitySubjects
- FakeBookingNotificationSender
- Shared Domain/Application test factories

BookingApiFactory runs EF Core migrations
against the PostgreSQL test database.

Assessment:
Reuse and audit the existing infrastructure.

Destination: 22.8.

## 9. Architecture tests

Existing:
- PaymentProviderArchitectureTests
- CurrentUserArchitectureTests
- Related authorization and DI tests

No dedicated Architecture.Tests project
was identified.

Destination: 22.5.

## 10. CI and coverage

Existing:
- GitHub Actions CI
- Dependency restore
- Release build
- Release test execution
- coverlet.collector package references

Not identified in the CI workflow:
- Coverage artifact publication
- Dependency vulnerability quality gate
- Coverage thresholds

The baseline global.json selects
10.0.400-preview.0.26322.102.

SDK selection and reproducibility
require verification.

Destination: 22.9.

## 11. Migration smoke testing

Existing:
- PostgreSQL Testcontainers
- EF Core MigrateAsync in BookingApiFactory

A dedicated independent migration smoke
test was not identified.

Testcontainers migrations do not prove
production database upgrade compatibility.

Destination: 22.10.

## 12. Deferred register

No explicit unresolved DEFERRED targeting
22.x was identified in the inspected
roadmap and repository documentation.

Proposed roadmap clarification:

Frontend CI build/test belongs to
25.7 — Testing frontend.

This clarification requires agreement
before modifying the official roadmap.

## 13. Inventory conclusion

Preserve valid existing tests.

Do not create duplicate test suites.

Measure and analyze actual coverage
before implementing additional cases.

Every confirmed gap must have an
explicit destination in Module 22.

Final closure requires:
- Release build
- Full Release test suite
- Architecture validation
- Security and concurrency regression
- Migration smoke verification
- CI quality gate verification

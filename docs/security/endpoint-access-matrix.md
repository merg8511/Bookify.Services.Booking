# Bookify — Endpoint Access Matrix

## Purpose

This document defines the access classification of the Booking API.

Authentication identifies a caller.

Authorization decides whether that caller may perform a particular
operation on a particular resource.

Resource-level policies are implemented in module 17.6.

## Public endpoints

These endpoints intentionally allow anonymous access.

- GET /api/v1/properties
- GET /api/v1/properties/{propertyId}
- GET /api/v1/properties/{propertyId}/units
- GET /api/v1/properties/{propertyId}/availability
- POST /api/v1/bookings
- GET /health

POST /api/v1/bookings may also receive a valid Bearer token.

When the caller is authenticated, the Booking is associated with the
authenticated subject through Booking.CustomerSubjectId.

Otherwise guest checkout is used.

## Customer or guest booking access

These operations belong to the customer who owns the Booking or to the
guest holding its opaque access credential.

- GET /api/v1/bookings/{identifier}
- POST /api/v1/bookings/{bookingId}/cancel
- POST /api/v1/payments
- GET /api/v1/payments/bookings/{bookingId}/status

Authenticated access uses the Bearer token subject.

Guest access uses:

Booking-Guest-Token

The raw guest credential is never stored.

Module 17.5 classifies these routes.

Module 17.6 enforces Booking ownership and guest credential validation
through authorization policies.

## Owner or Admin endpoints

Existing Owner/Admin operations:

- POST /api/v1/properties
- POST /api/v1/bookings/{bookingId}/approve
- POST /api/v1/bookings/{bookingId}/reject

These endpoints require an Owner or Admin role.

For Booking actions, role membership alone does not prove ownership.

Module 17.6 adds resource ownership authorization.

Property management, RentableUnit management, pricing management and
calendar APIs that do not exist yet are not introduced by module 17.5.

Their implementation remains assigned to module 21.


### Module 21.2 — RentableUnit management

Administrative routes:

- POST /api/v1/admin/properties/{propertyId}/units
- GET /api/v1/admin/properties/{propertyId}/units
- GET /api/v1/admin/properties/{propertyId}/units/{unitId}
- PUT /api/v1/admin/properties/{propertyId}/units/{unitId}
- PATCH /api/v1/admin/properties/{propertyId}/units/{unitId}/status

Authorization:

- Admin may manage units belonging to any property.
- Owner may manage units only when Property.OwnerSubjectId matches the authenticated subject.
- Customer and anonymous callers cannot access these endpoints.
- Units are always scoped to the property in the route.

The administrative read includes active and inactive units.

The public GET /api/v1/properties/{propertyId}/units remains unchanged.

Business constraints:

- Unit type cannot change when bookings reference the unit.
- Maximum capacity cannot be reduced below the guest count of an existing booking.
- Deactivation does not cancel or delete existing bookings.
- Hard delete is not exposed.
- Pricing administration belongs to module 21.3.
- Season administration belongs to module 21.4.

Writes reuse the existing PostgreSQL property row lock.


### Module 21.3 — RentableUnit Pricing

Administrative routes:

- GET /api/v1/admin/properties/{propertyId}/units/{unitId}/pricing
- PUT /api/v1/admin/properties/{propertyId}/units/{unitId}/pricing

Authorization:

- Admin may access pricing for any property.
- Owner may access pricing only for owned properties.
- Customer and anonymous callers are not authorized.
- The rentable unit must belong to the property in the route.

Pricing configuration:

- Regular nightly rate.
- Weekend nightly rate.
- Extra guest nightly rate.
- One currency shared by all three rates.
- All amounts use Money domain validation.
- Amounts must fit PostgreSQL numeric(18,3).

PUT configures or replaces the complete pricing configuration.

GET returns IsConfigured=false and null amounts when the unit
exists but has no pricing configuration.

Existing pricing seasons must remain currency-compatible.

Existing Booking price snapshots are not recalculated when
administrative pricing changes.

Pricing season management belongs to module 21.4.

Comprehensive security and integration tests belong to module 21.9.

### Module 21.4 — Pricing Seasons


Administrative routes:
- POST /api/v1/admin/properties/{propertyId}/units/{unitId}/seasons
- GET /api/v1/admin/properties/{propertyId}/units/{unitId}/seasons
- GET /api/v1/admin/properties/{propertyId}/units/{unitId}/seasons/{seasonId}
- PUT /api/v1/admin/properties/{propertyId}/units/{unitId}/seasons/{seasonId}
- PATCH /api/v1/admin/properties/{propertyId}/units/{unitId}/seasons/{seasonId}/status

Authorization:
- Admin may manage pricing seasons for any property.
- Owner may only manage seasons belonging to owned properties.
- Customer and anonymous callers cannot manage pricing seasons.
- Units and seasons are scoped to the identifiers in the route.

Business rules:
- A rentable unit must have base Pricing configured.
- Season currency must match the base Pricing currency.
- Date ranges use [StartDate, EndDate).
- EndDate must be greater than StartDate.
- Priority must be non-negative.
- Active overlapping seasons with different priorities are allowed.
- Active overlapping seasons with equal priorities are rejected.
- The highest applicable priority determines the nightly rate.
- Inactive seasons do not affect availability quotes or new bookings.
- Deactivation is reversible and does not delete historical data.
- Existing Booking price snapshots remain unchanged.

Persistence:
- PricingSeason uses the existing integer database identifier.
- IsActive is persisted in rentable_unit_pricing_seasons.
- Existing seasons are active after the migration.

Full security and integration coverage belongs to module 21.9.

### Module 21.5 — Administrative Booking Reads

Administrative routes:

- GET /api/v1/admin/properties/{propertyId}/bookings
- GET /api/v1/admin/properties/{propertyId}/bookings/{bookingId}

Authorization:

- Admin may read bookings belonging to any property.
- Owner may only read bookings belonging to owned properties.
- Customer and anonymous callers cannot use these endpoints.
- All reads are scoped to the property identifier in the route.
- A booking belonging to another property returns NotFound.

Paged list filters:

- propertyId (required route parameter)
- status (optional)
- rangeStart / rangeEnd (optional pair)
- unitId (optional)
- customerSubjectId (optional)

The customerSubjectId filter consumes the deferred
client-filter requirement from module 6.5.

Date filtering uses overlapping stay intervals:
Booking.CheckInDate < RangeEnd
AND Booking.CheckOutDate > RangeStart.

The interval convention is [start, end).

Pagination:

- Default page number: 1
- Default page size: 20
- Maximum page size: 100
- TotalRecords and TotalPages are returned.

Ordering:

- CreatedAtUtc descending, nulls last.
- Booking ID descending as a deterministic tie-breaker.

The administrative detail includes guest information,
price snapshot data, booking status, payment status,
customer identity and lifecycle timestamps.

The existing customer/guest booking endpoint remains
unchanged and retains its own authorization policy.

Comprehensive tests belong to module 21.9.


### Module 21.6 — Administrative Calendar API

Administrative route:

- GET /api/v1/admin/properties/{propertyId}/calendar

Authorization:

- Admin may consult any existing property's calendar.
- Owner may consult calendars of owned properties only.
- Customer and anonymous callers cannot access calendars.
- Ownership is validated using propertyId from the route.

Required query parameters:

- rangeStart: inclusive date.
- rangeEnd: exclusive date.

Optional query parameters:

- unitId: filter by rentable unit.
- blocksInventory: filter by inventory-blocking status.

Date validation:

- Both dates are required.
- RangeEnd must be greater than RangeStart.
- Maximum requested range: 366 days.
- Intervals use [RangeStart, RangeEnd).

Response:

- PropertyId
- RangeStart
- RangeEnd
- Items

Each calendar item includes:

- BookingId
- RentableUnitId
- RentableUnitName
- CheckInDate
- CheckOutDate
- GuestCount
- Status
- BlocksInventory

The API reuses IBookingReadService.GetCalendarAsync.

BlocksInventory uses the existing status classification:

- PendingApproval: true
- PendingPayment: true
- Paid: true
- Completed: true
- Cancelled: false

Inactive properties remain administratively readable.

The calendar does not expose guest personal information.

This endpoint is read-only. It does not create, update
or cancel bookings.

Comprehensive authorization, filtering and regression
coverage belongs to module 21.9.


### Property management — Module 21.1

- PUT /api/v1/properties/{propertyId}
- PATCH /api/v1/properties/{propertyId}/status

Both endpoints require authentication and the Owner or Admin role.

Admin:
- May manage any Property.

Owner:
- May only manage a Property whose OwnerSubjectId matches the authenticated subject.

Customer and anonymous:
- Cannot modify Properties.

Property ownership is checked through a dedicated Dapper read service.
The write handler reuses the PostgreSQL property row lock used by booking creation.

Missing resources return 404 for authorized Admin requests.
An Owner without access to the resource receives 403.

Property deactivation does not delete existing bookings or units.


## System-only operations

The following operations are not public HTTP endpoints:

- mark Booking as paid
- expire Booking payment
- complete Booking

Payment success is driven by authoritative payment reconciliation.

Payment expiration and Booking completion are internal application
operations that will be invoked by backend processes.

No external client, Owner, Customer or Admin receives direct HTTP
access to these commands.

## Provider webhook

Stripe webhook:

- POST /api/v1/payments/webhooks/stripe

The webhook does not use Bookify user JWT authentication.

Its authenticity is established through the Stripe-Signature header
and provider signature validation.

## Authorization roadmap

17.5:
- endpoint classification;
- explicit public routes;
- Owner/Admin role boundary;
- removal of system-only HTTP routes;
- provider webhook classification.

17.6:
- AdminOnly;
- PropertyOwnerOrAdmin;
- BookingOwner;
- BookingGuestAccess;
- CustomerOrGuestBookingAccess.

17.9:
- complete authorization matrix tests.

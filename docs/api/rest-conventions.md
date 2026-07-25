# Bookify API REST Conventions

## Base Path

All public endpoints use:

```text
/api/v1
```

## Route Naming

- Lowercase paths.
- Plural resource names.
- Kebab-case for compound resources.
- Semantic route parameter names.
- No trailing slash in canonical examples.

### Examples:
- `GET /api/v1/properties`
- `GET /api/v1/rentable-units`
- `GET /api/v1/bookings/{bookingId}`

## HTTP Resource Methods

### GET
Use for safe, read-only operations.

**Examples:**
- `GET /api/v1/properties/{propertyId}`
- `GET /api/v1/bookings/{bookingId}`
- `GET /api/v1/properties/{propertyId}/availability`

> **Note:** `GET` requests do not use a request body.

### POST
Use to:
- Create a resource in a collection.
- Execute an explicit domain action.

**Examples:**
- `POST /api/v1/properties`
- `POST /api/v1/bookings`
- `POST /api/v1/bookings/{bookingId}/approve`
- `POST /api/v1/bookings/{bookingId}/cancel`

**Resource creation returns:**
- `201 Created`
- `Location: /api/v1/<resource>/{id}`

`POST` is not assumed to be idempotent. Commands that require retry safety will use the future `Idempotency-Key` mechanism.

### PUT
Use only for full replacement of the editable representation of an existing resource.

`PUT` is idempotent.

**Success returns:**
- `200 OK` when returning a representation.
- `204 No Content` when no response body is required.

### PATCH
Use for partial modification of resource data.

`PATCH` is not assumed to be idempotent.

> **Rule:** Do not use `PATCH` to bypass aggregate behavior or assign domain lifecycle states directly.

### DELETE
Use only when the resource is actually removed.

**Do not use DELETE for:**
- Cancelling a `Booking`
- Deactivating a `Property`
- Deactivating a `RentableUnit`

These operations are domain state transitions and use explicit `POST` action endpoints.

**Successful synchronous deletion without a body returns:**
- `204 No Content`

## Domain Actions

Important domain transitions use explicit action endpoints:
- `POST /api/v1/bookings/{bookingId}/approve`
- `POST /api/v1/bookings/{bookingId}/reject`
- `POST /api/v1/bookings/{bookingId}/cancel`

Clients cannot set `Booking` status directly.

**Invalid transitions return:**
- `409 Conflict` (when conflict support is implemented).

## HTTP Status Codes

### Success Status Codes
- `200 OK`: Successful operation with response content.
- `201 Created`: Resource created.
- `202 Accepted`: Processing accepted but not completed.
- `204 No Content`: Operation completed without response content.

### Error Status Codes
- `400 Bad Request`: Validation or malformed input.
- `401 Unauthorized`: Missing or invalid authentication.
- `403 Forbidden`: Authenticated but not authorized.
- `404 Not Found`: Resource does not exist.
- `409 Conflict`: Conflict with current state.
- `415 Unsupported Media Type`: Unsupported request content type.
- `500 Internal Server Error`: Unexpected technical failure.

> **Note:** Errors use `application/problem+json`.

## Query Parameters

Use query parameters for:
- Filtering
- Searching
- Pagination
- Sorting
- Date ranges
- Capacity
- Status

**Example:**
```http
GET /api/v1/bookings?pageNumber=1&pageSize=20&status=Paid
```

## Idempotency Summary

HTTP method semantics:
- **GET**: Idempotent
- **PUT**: Idempotent
- **DELETE**: Idempotent
- **POST**: Not idempotent by default
- **PATCH**: Not assumed to be idempotent

Booking creation, payment initiation, and other retry-sensitive `POST` operations will use `Idempotency-Key` in Module 10.

---

# API

Last reviewed: 2026-08-02.

## 1. Contract sources

Runtime status: Product Service now implements and tests the catalog/detail/create subset below. Order, Notification, Gateway, internal inventory, and Angular-facing routes remain planned until their owning phases.

Executable contract sources:

- ASP.NET Core endpoint/controller definitions;
- request/response DTOs and validation;
- generated OpenAPI JSON per HTTP service;
- Gateway route configuration;
- MassTransit message contract assembly;
- contract and integration tests.

This document is the human-readable catalog. If prose and runtime OpenAPI disagree, runtime code is the immediate truth and the discrepancy must be fixed.

## 2. URL model

### Browser-facing

Angular calls Gateway using same-origin paths:

```text
/api/products
/api/orders
/api/notifications
```

### Service-native public API

| Service | Base path |
| --- | --- |
| Product Service | `/api/v1/products` |
| Order Service | `/api/v1/orders` |
| Notification Service | `/api/v1/notifications` |

### Internal API

Product inventory commands:

```text
/internal/v1/inventory/reservations
```

Internal paths are not routed by Gateway.

## 3. Content and conventions

- Request/response content type: `application/json`.
- Errors: `application/problem+json`.
- IDs: UUID strings.
- Dates: ISO 8601 UTC.
- Money: JSON number with fixed server-side decimal semantics.
- JSON property casing: camelCase.
- Pagination: page/limit for baseline simplicity.
- Sorting: server-defined stable default.
- Authentication: none in baseline.
- Trace propagation: W3C `traceparent`; optional `X-Correlation-ID`.

## 4. Success shapes

Single resource example:

```json
{
  "id": "7f3e...",
  "name": "Mechanical Keyboard",
  "unitPrice": 1200000,
  "currency": "VND",
  "availableStock": 8,
  "isActive": true
}
```

Paged list:

```json
{
  "items": [],
  "page": 1,
  "limit": 20,
  "total": 0,
  "totalPages": 0
}
```

No generic `{ success: true }` wrapper is required. HTTP status and typed bodies provide success semantics.

## 5. Error shape

RFC 7807 Problem Details with stable extensions:

```json
{
  "type": "https://microshop.local/problems/order-state-conflict",
  "title": "Order state conflict",
  "status": 409,
  "detail": "The order cannot be cancelled from its current state.",
  "instance": "/api/v1/orders/7f3e.../cancel",
  "traceId": "00-...",
  "code": "ORDER_STATE_CONFLICT",
  "errors": {}
}
```

`detail` is diagnostic/user-friendly fallback. Angular behavior must branch on `code`.

## 6. Product endpoints

### `GET /api/v1/products`

Purpose: list products. Implemented by Product Service; Gateway exposure is deferred to Phase 4.

| Query | Type | Default | Notes |
| --- | --- | --- | --- |
| `page` | integer | 1 | >= 1 |
| `limit` | integer | 20 | 1..100 |
| `includeInactive` | boolean | false | operator/demo use |
| `search` | string | empty | optional name search extension |

Response: `200 ProductPage`.

Default ordering: active first when included, then name, then ID.

### `GET /api/v1/products/{productId}`

Implemented by Product Service.

Response:

- `200 ProductResponse`;
- `404 PRODUCT_NOT_FOUND`.

### `POST /api/v1/products`

Implemented by Product Service.

Request:

```json
{
  "name": "Mechanical Keyboard",
  "description": "Demo product",
  "unitPrice": 1200000,
  "currency": "VND",
  "initialStock": 10,
  "isActive": true
}
```

Response:

- `201 ProductResponse`;
- `400 VALIDATION_ERROR`.

Location header points to the product detail endpoint.

### `PATCH /api/v1/products/{productId}`

Planned for the next Product Service slice; the current service intentionally has no update or hard-delete endpoint.

Request may contain:

```json
{
  "name": "Updated name",
  "description": "Updated description",
  "unitPrice": 1250000,
  "availableStock": 12,
  "isActive": true
}
```

Response:

- `200 ProductResponse`;
- `404 PRODUCT_NOT_FOUND`;
- `409 PRODUCT_CONCURRENCY_CONFLICT`.

The baseline allows direct stock adjustment for learning. It must be clearly separate from reservation-driven stock changes in logs/history if stock movement auditing is added.

## 7. Order endpoints

### `POST /api/v1/orders`

Request:

```json
{
  "customerName": "Nguyen Van A",
  "customerEmail": "a@example.com",
  "items": [
    {
      "productId": "6aa7c15c-...",
      "quantity": 2
    }
  ]
}
```

The request must not accept price, product name, subtotal, total, or status.

Successful response: `201 OrderResponse`.

Representative response:

```json
{
  "id": "bca0...",
  "customerName": "Nguyen Van A",
  "customerEmail": "a@example.com",
  "status": "confirmed",
  "currency": "VND",
  "totalAmount": 2400000,
  "items": [
    {
      "productId": "6aa7...",
      "productName": "Mechanical Keyboard",
      "unitPrice": 1200000,
      "quantity": 2,
      "subtotal": 2400000
    }
  ],
  "canCancel": true,
  "createdAtUtc": "2026-08-02T00:00:00Z",
  "confirmedAtUtc": "2026-08-02T00:00:01Z"
}
```

Possible errors:

- `400 VALIDATION_ERROR`;
- `404 PRODUCT_NOT_FOUND`;
- `409 PRODUCT_INACTIVE`;
- `409 INSUFFICIENT_STOCK`;
- `503 PRODUCT_SERVICE_UNAVAILABLE`;
- `503 INVENTORY_OUTCOME_UNKNOWN`.

The order record may exist in a rejected/unknown state even when the public response is an error. Error details may include `orderId` so the learner can inspect it.

### `GET /api/v1/orders`

| Query | Type | Default |
| --- | --- | --- |
| `page` | integer | 1 |
| `limit` | integer | 20 |
| `status` | string | all |
| `customerEmail` | string | optional demo filter |

Response: `200 OrderPage`.

### `GET /api/v1/orders/{orderId}`

Response:

- `200 OrderResponse`;
- `404 ORDER_NOT_FOUND`.

### `POST /api/v1/orders/{orderId}/cancel`

Request body: none or optional reason in a later extension.

Response:

- `200 OrderResponse` when cancelled or repeated idempotent cancellation;
- `404 ORDER_NOT_FOUND`;
- `409 ORDER_STATE_CONFLICT`;
- `503 PRODUCT_SERVICE_UNAVAILABLE`;
- `503 INVENTORY_OUTCOME_UNKNOWN` when release outcome is ambiguous.

## 8. Notification endpoints

Notification Service may run a small HTTP read API alongside the consumer.

### `GET /api/v1/notifications`

| Query | Type | Notes |
| --- | --- | --- |
| `customerEmail` | string | optional |
| `orderId` | uuid | optional |
| `page` | integer | default 1 |
| `limit` | integer | default 20 |

Response:

```json
{
  "items": [
    {
      "id": "uuid",
      "orderId": "uuid",
      "customerEmail": "a@example.com",
      "subject": "Order confirmed",
      "body": "Your order ... was confirmed.",
      "totalAmount": 2400000,
      "currency": "VND",
      "isRead": false,
      "createdAtUtc": "2026-08-02T00:00:03Z"
    }
  ],
  "page": 1,
  "limit": 20,
  "total": 1,
  "totalPages": 1
}
```

### `POST /api/v1/notifications/{notificationId}/read`

Optional baseline endpoint.

Response:

- `200 NotificationResponse`;
- `404 NOTIFICATION_NOT_FOUND`.

## 9. Internal inventory API

These contracts are service-to-service only.

### `POST /internal/v1/inventory/reservations`

Headers:

- `traceparent`;
- `X-Correlation-ID` optional;
- optional internal authentication deferred.

Request:

```json
{
  "orderId": "bca0...",
  "items": [
    {
      "productId": "6aa7...",
      "quantity": 2
    }
  ]
}
```

Successful response:

```json
{
  "reservationId": "f832...",
  "orderId": "bca0...",
  "status": "reserved",
  "currency": "VND",
  "totalAmount": 2400000,
  "items": [
    {
      "productId": "6aa7...",
      "productName": "Mechanical Keyboard",
      "unitPrice": 1200000,
      "quantity": 2,
      "subtotal": 2400000
    }
  ],
  "idempotentReplay": false
}
```

Status behavior:

- `200` if existing identical reservation is returned;
- `201` if new reservation is created;
- `404 PRODUCT_NOT_FOUND`;
- `409 PRODUCT_INACTIVE`;
- `409 INSUFFICIENT_STOCK`;
- `409 RESERVATION_REQUEST_MISMATCH`.

### `POST /internal/v1/inventory/reservations/{orderId}/release`

Response:

```json
{
  "orderId": "bca0...",
  "reservationId": "f832...",
  "status": "released",
  "idempotentReplay": false,
  "releasedAtUtc": "2026-08-02T01:00:00Z"
}
```

Errors:

- `404 RESERVATION_NOT_FOUND`;
- `409 RESERVATION_STATE_CONFLICT`.

A repeated release returns `200` with `idempotentReplay: true`.

### Optional `GET /internal/v1/inventory/reservations/by-order/{orderId}`

Used only for learning/reconciliation. It must not become a general cross-service query API.

## 10. Integration event contract

Contract name:

```text
MicroShop.Contracts.Orders.OrderConfirmedV1
```

C# shape:

```csharp
public sealed record OrderConfirmedV1(
    Guid MessageId,
    Guid OrderId,
    string CustomerName,
    string CustomerEmail,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderConfirmedItemV1> Items,
    DateTimeOffset OccurredAtUtc,
    int SchemaVersion = 1);

public sealed record OrderConfirmedItemV1(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);
```

Required broker metadata:

- `MessageId` equals event `MessageId`;
- event type/version;
- trace context;
- producing service;
- content type.

Rules:

1. Contract assembly contains records/interfaces only.
2. No EF attributes/entities.
3. Additive fields are preferred for compatible evolution.
4. Renaming/removing fields requires a new event version.
5. Consumers may coexist for V1/V2 during migration.
6. Event describes confirmed state; it is not a command to confirm.

## 11. Gateway route catalog

| Route ID | Match | Cluster |
| --- | --- | --- |
| `products-route` | `/api/products/{**catch-all}` | `product-cluster` |
| `orders-route` | `/api/orders/{**catch-all}` | `order-cluster` |
| `notifications-route` | `/api/notifications/{**catch-all}` | `notification-cluster` |

Gateway transforms remove the public prefix and target `/api/v1/...` or preserve paths according to committed configuration. Tests must assert exact behavior.

Gateway must not route `/internal/*`.

## 12. Health, docs, and diagnostics

Each HTTP process:

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/health/live` | process liveness |
| GET | `/health/ready` | dependency readiness |
| GET | `/openapi/v1.json` or framework path | OpenAPI document in non-production or protected environment |
| GET | `/swagger` | interactive docs in development |

Notification Service includes a small read API, so normal HTTP health endpoints are available.

Product Service serves `/openapi/v1.json` in non-production environments. Its `/health/ready` endpoint includes the owned Product PostgreSQL database check; `/health/live` checks only process liveness.

## 13. Validation behavior

ASP.NET Core validation returns `400 VALIDATION_ERROR`.

```json
{
  "errors": {
    "customerEmail": ["A valid email is required."],
    "items[0].quantity": ["Quantity must be between 1 and 100."]
  }
}
```

Angular maps exact paths to Reactive Form controls where possible.

## 14. Pagination behavior

- `page` is 1-based.
- `limit` maximum is 100.
- stable ordering always includes ID as final tie-breaker.
- invalid page/limit returns validation error.
- empty pages return `items: []`.
- cursor pagination is unnecessary for the baseline.

## 15. Idempotency and retries

### Internal inventory calls

Idempotency is built into the body using `orderId`.

### Public order creation

The first baseline does not guarantee browser retry idempotency. Angular disables duplicate submission while one request is active.

Optional extension:

- accept `Idempotency-Key`;
- store key + request hash + response in Order DB;
- reject same key with different request.

### Messaging

Broker redelivery is expected; Notification Service deduplicates by message ID.

## 16. Rate and size limits

Even without authentication:

- product write endpoints receive conservative rate limits in public deployment;
- order endpoint has request rate and body-size limits;
- maximum 20 distinct items;
- text lengths are bounded;
- Gateway may enforce coarse limits, while services enforce authoritative limits.

Local tests may relax rate limits through explicit test configuration.

## 17. API versioning and compatibility

1. HTTP major version is in path: `/api/v1`.
2. Additive response fields are backward-compatible.
3. Removing/renaming fields or changing semantics requires `/api/v2`.
4. Internal APIs are versioned because independent deployment still creates compatibility needs.
5. Event version is part of contract name/schema.
6. Gateway changes and service changes must be deployed compatibly.
7. OpenAPI snapshots/contract tests detect accidental drift.

## 18. API review checklist

- [ ] Correct public or internal boundary.
- [ ] No browser-authoritative price/status.
- [ ] Stable error code.
- [ ] Cancellation token used.
- [ ] Trace context propagated.
- [ ] Idempotency considered.
- [ ] OpenAPI updated.
- [ ] Angular contract/client updated.
- [ ] Integration and failure tests added.
- [ ] Gateway route updated only when public.

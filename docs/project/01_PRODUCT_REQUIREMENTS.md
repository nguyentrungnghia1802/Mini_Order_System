# Product Requirements

Last reviewed: 2026-08-02.

## 1. Scope and terminology

Mini Order System is a learning application that supports a minimal product-and-order journey across multiple .NET services.

Status labels:

- **Specified**: behavior is defined in documentation but implementation has not been verified.
- **Implemented**: reachable runtime behavior and tests exist.
- **Partial**: a useful subset exists but one or more documented acceptance conditions remain.
- **Deferred**: intentionally excluded from the current baseline.

Terms:

| Term | Meaning |
| --- | --- |
| Product | A sellable demo item with name, price, active state, and finite stock |
| Inventory reservation | Product Service record linking stock allocation to one order |
| Order | Commercial snapshot stored by Order Service |
| Notification | Simulated customer message created from an integration event |
| Public API | Browser-facing API reached through YARP Gateway |
| Internal API | Service-to-service endpoint not intended for Angular |
| Integration event | Immutable fact published after a committed business state |
| Message ID | Unique broker message identifier used for deduplication |
| Trace ID | Identifier propagated through HTTP and messaging for diagnostics |
| Learning baseline | Minimum target that demonstrates the complete architecture |

## 2. Actors

| Actor | Capabilities |
| --- | --- |
| Shopper | Browse active products, submit an order, view order, cancel eligible order |
| Demo operator | Create and update products; inspect all orders and notifications |
| Developer | Run services, inspect logs/data/broker, trigger faults, execute tests |
| System | Validate requests, reserve/release stock, publish/consume events |
| Test runner | Provision isolated infrastructure and assert contracts |

No authentication exists in the first baseline. Shopper and operator are UI modes, not security principals. Production authorization must not be inferred from this demo behavior.

## 3. Functional requirements

### Product catalog

| ID | Requirement | Initial status |
| --- | --- | --- |
| FR-PROD-001 | List active products for the shopper catalog | Partial |
| FR-PROD-002 | Get one product by ID | Partial |
| FR-PROD-003 | Create a product with name, optional description, unit price, and initial stock | Partial |
| FR-PROD-004 | Update mutable product fields | Specified |
| FR-PROD-005 | Activate or deactivate a product | Specified |
| FR-PROD-006 | Reject negative price or stock | Partial |
| FR-PROD-007 | Hide inactive products from the shopper list while retaining operator visibility | Partial |
| FR-PROD-008 | Return current available stock | Partial |
| FR-PROD-009 | Preserve a product after it appears in an order; deletion is not required | Specified |
| FR-PROD-010 | Support pagination with deterministic ordering for operator lists | Partial |

### Inventory reservation

| ID | Requirement | Initial status |
| --- | --- | --- |
| FR-INV-001 | Product Service owns all stock mutations | Specified |
| FR-INV-002 | Reserve multiple order items atomically within Product Service | Specified |
| FR-INV-003 | Reject the complete reservation when any product is missing, inactive, or insufficient | Specified |
| FR-INV-004 | Never partially decrement stock for a failed bulk reservation | Specified |
| FR-INV-005 | Use `orderId` as an idempotency boundary for one reservation set | Specified |
| FR-INV-006 | Return authoritative product name and unit price snapshots to Order Service | Specified |
| FR-INV-007 | Release an active reservation once when an order is cancelled | Specified |
| FR-INV-008 | Repeated release calls return the already-released result without increasing stock twice | Specified |
| FR-INV-009 | Prevent available stock from becoming negative under concurrent requests | Specified |
| FR-INV-010 | Retain reservation history for diagnostics | Specified |

### Order management

| ID | Requirement | Initial status |
| --- | --- | --- |
| FR-ORD-001 | Create an order containing customer name, customer email, and one or more items | Specified |
| FR-ORD-002 | Generate the order ID in Order Service before inventory reservation | Specified |
| FR-ORD-003 | Store an initial `pending_inventory` order before calling Product Service | Specified |
| FR-ORD-004 | Call Product Service synchronously to reserve stock | Specified |
| FR-ORD-005 | Confirm the order only after Product Service confirms the full reservation | Specified |
| FR-ORD-006 | Build totals from authoritative product snapshots, never browser prices | Specified |
| FR-ORD-007 | Mark the order `rejected` when Product Service returns a business rejection | Specified |
| FR-ORD-008 | Mark the order `inventory_unknown` when the dependency outcome cannot be determined safely | Specified |
| FR-ORD-009 | List orders in reverse creation order with pagination | Specified |
| FR-ORD-010 | Get order detail with immutable item snapshots | Specified |
| FR-ORD-011 | Cancel only a confirmed order | Specified |
| FR-ORD-012 | Release stock before finalizing normal cancellation | Specified |
| FR-ORD-013 | Return current state when a repeated cancellation is idempotently safe | Specified |
| FR-ORD-014 | Publish `OrderConfirmed` only for a confirmed order | Specified |
| FR-ORD-015 | Publish `OrderCancelled` as an optional extension | Deferred |

### Messaging and notifications

| ID | Requirement | Initial status |
| --- | --- | --- |
| FR-MSG-001 | Order Service publishes an immutable `OrderConfirmed` integration event | Specified |
| FR-MSG-002 | Event includes all information Notification Service needs | Specified |
| FR-MSG-003 | Notification Service consumes from a durable RabbitMQ queue | Specified |
| FR-MSG-004 | One message ID creates at most one persisted notification | Specified |
| FR-MSG-005 | Notification contains customer destination, order ID, total, and readable text | Specified |
| FR-MSG-006 | Failed consumers use broker retry and ultimately an error/dead-letter path | Specified |
| FR-MSG-007 | Order confirmation does not wait for Notification Service | Specified |
| FR-MSG-008 | Notifications can be listed through a read-only API for demonstration | Specified |
| FR-MSG-009 | No real external delivery provider is required | Specified |
| FR-MSG-010 | Message contracts contain no EF entities or service-internal types | Specified |

### API Gateway

| ID | Requirement | Initial status |
| --- | --- | --- |
| FR-GW-001 | Angular uses one public origin for Product and Order APIs | Specified |
| FR-GW-002 | Gateway routes product paths to Product Service | Specified |
| FR-GW-003 | Gateway routes order paths to Order Service | Specified |
| FR-GW-004 | Gateway routes notification read paths to Notification Service when enabled | Specified |
| FR-GW-005 | Gateway forwards W3C trace headers | Specified |
| FR-GW-006 | Gateway exposes its own liveness/readiness endpoints | Specified |
| FR-GW-007 | Gateway does not contain product/order business rules | Specified |
| FR-GW-008 | Internal inventory endpoints are not routed publicly | Specified |

### Angular frontend

| ID | Requirement | Initial status |
| --- | --- | --- |
| FR-WEB-001 | Show active product cards/table with price and available stock | Specified |
| FR-WEB-002 | Let the user select positive quantities within visible stock | Specified |
| FR-WEB-003 | Collect customer name and valid email using Reactive Forms | Specified |
| FR-WEB-004 | Submit an order through the Gateway | Specified |
| FR-WEB-005 | Display confirmed, rejected, dependency-failure, and validation outcomes distinctly | Specified |
| FR-WEB-006 | Show order list and order detail | Specified |
| FR-WEB-007 | Allow cancellation only when the API indicates eligibility | Specified |
| FR-WEB-008 | Show simulated notifications | Specified |
| FR-WEB-009 | Provide a small operator product form | Specified |
| FR-WEB-010 | Avoid direct browser calls to internal service addresses | Specified |
| FR-WEB-011 | Handle loading, empty, retryable error, and offline dependency states | Specified |
| FR-WEB-012 | Remain usable at 320px width and with keyboard navigation | Specified |

### Health and diagnostics

| ID | Requirement | Initial status |
| --- | --- | --- |
| FR-OPS-001 | Every process exposes liveness | Specified |
| FR-OPS-002 | HTTP services expose readiness including required dependencies | Specified |
| FR-OPS-003 | Notification readiness checks RabbitMQ and its database | Specified |
| FR-OPS-004 | Logs include service name, environment, trace ID, and important entity IDs | Specified |
| FR-OPS-005 | HTTP calls propagate `traceparent` and optional `X-Correlation-ID` | Specified |
| FR-OPS-006 | Published messages carry trace context and message ID | Specified |
| FR-OPS-007 | Unexpected errors return safe problem details and are logged once | Specified |
| FR-OPS-008 | Development configuration allows deliberate dependency shutdown tests | Specified |

## 4. Business rules

### Product rules

1. `name` is required, trimmed, and limited to 200 characters.
2. `description` is optional and limited to 2,000 characters.
3. `unitPrice` is greater than or equal to zero.
4. Currency is fixed to `VND` for the baseline.
5. `availableStock` is an integer greater than or equal to zero.
6. Inactive products cannot be newly reserved.
7. Existing order snapshots remain valid after a product is edited or deactivated.
8. Hard deletion is deferred; deactivation is the normal lifecycle operation.

### Order input rules

1. `customerName` is required and limited to 150 characters.
2. `customerEmail` must be syntactically valid and normalized to lowercase for notification lookup.
3. An order contains between 1 and 20 distinct products.
4. Each quantity is an integer from 1 to 100.
5. Duplicate product IDs in a request are rejected.
6. Browser-provided names, prices, subtotals, and totals are ignored and should not be accepted by the request schema.
7. The maximum order total is `1,000,000,000 VND`.

### Order state rules

| Current | Action/result | Next |
| --- | --- | --- |
| new | Persist request accepted | `pending_inventory` |
| `pending_inventory` | Reservation confirmed | `confirmed` |
| `pending_inventory` | Product business rejection | `rejected` |
| `pending_inventory` | Unknown dependency outcome | `inventory_unknown` |
| `confirmed` | Cancellation release confirmed | `cancelled` |
| `confirmed` | Cancellation release outcome unknown | `cancellation_pending` |
| `cancellation_pending` | Reconciliation confirms release | `cancelled` |
| terminal | Repeated incompatible transition | no change; `409` or idempotent current result |

Terminal states for the baseline are `rejected` and `cancelled`. `confirmed` is operationally complete for ordering but remains cancellable.

### Inventory rules

1. Product Service reserves all requested items in one local database transaction.
2. Rows are locked or updated with an equivalent concurrency-safe method.
3. A reservation is uniquely identified by `(order_id, product_id)` through an order-level reservation and unique items.
4. A repeated request for the same order and same item set returns the existing reservation result.
5. A repeated request for the same order with different items returns `409 RESERVATION_REQUEST_MISMATCH`.
6. Release changes only `reserved` rows to `released`.
7. Stock restoration equals the originally reserved quantity.
8. Product Service never accepts an order total from Order Service.

### Event rules

1. Event names are past-tense facts.
2. `OrderConfirmed` is published only after the confirmed order state commits.
3. Event schemas are versioned and backward-compatible within a major contract version.
4. Consumers ignore unknown additive fields.
5. Event message IDs are globally unique.
6. Notification Service records the message ID before/with the side effect in one local transaction.
7. Event replay must not produce duplicate notifications.

## 5. Core acceptance criteria

### AC-001: Successful order

Given two active products with enough stock, when the shopper submits a valid order:

- Order Service persists `pending_inventory`;
- Product Service reserves both items atomically;
- Product Service returns authoritative snapshots;
- Order Service persists item snapshots and total;
- order becomes `confirmed`;
- stock decreases once;
- an `OrderConfirmed` event is published;
- the HTTP response does not wait for notification consumption;
- Notification Service eventually persists one notification.

### AC-002: Insufficient stock

Given one requested product without enough stock:

- Product Service rejects the full reservation;
- no requested product stock changes;
- Order Service marks the order `rejected`;
- no `OrderConfirmed` event is published;
- Angular displays a stock-specific error.

### AC-003: Product Service unavailable

When Product Service is stopped before order submission:

- the call fails within the configured timeout;
- Order Service does not hang;
- order ends in `inventory_unknown` or an equivalent explicit non-confirmed state;
- the API returns `503` with a stable dependency error code;
- no confirmation event is published.

### AC-004: Notification Service unavailable

When Notification Service is stopped but RabbitMQ remains available:

- order confirmation succeeds;
- the event remains in the durable queue;
- no synchronous order error is returned;
- after Notification Service restarts, the notification is created.

### AC-005: Duplicate delivery

Given the same `OrderConfirmed` message is delivered more than once:

- only one notification row exists for its message ID;
- the duplicate is acknowledged safely;
- logs identify duplicate suppression.

### AC-006: Cancellation

Given a confirmed order:

- Order Service requests reservation release;
- Product Service restores stock exactly once;
- Order Service becomes `cancelled`;
- a repeated cancel does not restore stock again.

### AC-007: Concurrent last-stock purchase

Given stock of one and two simultaneous orders requesting one unit:

- at most one reservation succeeds;
- available stock never becomes negative;
- one order confirms and the other rejects.

### AC-008: Database ownership

A static/repository review confirms:

- Product Service connection string cannot access Order database in normal runtime configuration;
- Order Service has no Product EF entities/repositories;
- Notification Service has no Order database dependency;
- cross-service reads occur only through contracts.

## 6. Non-functional requirements

| ID | Requirement |
| --- | --- |
| NFR-001 | The full local stack starts with one documented Docker Compose command |
| NFR-002 | Normal local order creation completes within 3 seconds excluding image pulls/startup |
| NFR-003 | Product dependency timeout is explicit and no greater than 5 seconds |
| NFR-004 | APIs use cancellation tokens for request-bound I/O |
| NFR-005 | Monetary values use `decimal`, never binary floating point |
| NFR-006 | Date/time values are stored and transmitted in UTC |
| NFR-007 | Public API errors use RFC 7807 Problem Details with stable extension code |
| NFR-008 | OpenAPI exists for Gateway-routed HTTP services |
| NFR-009 | Database migrations are repeatable and committed per service |
| NFR-010 | Logs must not contain connection passwords or RabbitMQ credentials |
| NFR-011 | Tests must run without real external providers |
| NFR-012 | UI meets basic keyboard, label, contrast, and status-message requirements |
| NFR-013 | Containers run as non-root when practical in production-like images |
| NFR-014 | Graceful shutdown allows in-flight consumer processing within a bounded time |
| NFR-015 | Broker queues and database volumes are persistent in production-like Compose |
| NFR-016 | Services use deterministic JSON casing and UTC serialization |
| NFR-017 | Configuration is validated at startup |
| NFR-018 | No service silently falls back to another service's database |

## 7. Error behavior

Errors use `application/problem+json`.

```json
{
  "type": "https://microshop.local/problems/insufficient-stock",
  "title": "Insufficient stock",
  "status": 409,
  "detail": "One or more products cannot be reserved.",
  "instance": "/api/v1/orders",
  "traceId": "00-...",
  "code": "INSUFFICIENT_STOCK",
  "errors": {
    "items[0].quantity": ["Only 2 units are available."]
  }
}
```

Stable error codes include:

| Code | Typical status | Meaning |
| --- | ---: | --- |
| `VALIDATION_ERROR` | 400 | Request shape/value failed |
| `PRODUCT_NOT_FOUND` | 404 | Product does not exist |
| `PRODUCT_INACTIVE` | 409 | Product cannot be reserved |
| `INSUFFICIENT_STOCK` | 409 | Full reservation rejected |
| `RESERVATION_REQUEST_MISMATCH` | 409 | Same order ID reused with different items |
| `ORDER_NOT_FOUND` | 404 | Order does not exist |
| `ORDER_STATE_CONFLICT` | 409 | Transition is not allowed |
| `PRODUCT_SERVICE_UNAVAILABLE` | 503 | Synchronous dependency unavailable |
| `INVENTORY_OUTCOME_UNKNOWN` | 503 | Safe final inventory result is unknown |
| `MESSAGE_BROKER_UNAVAILABLE` | 503 or deferred outbox | Event cannot be durably accepted |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

Client code branches on `code`, not localized `detail`.

## 8. Definition of product completion

The product baseline is complete only when:

- all core acceptance criteria pass;
- OpenAPI matches runtime routes;
- database migrations build empty databases;
- no shared database access exists;
- Docker Compose provides the documented topology;
- failure exercises are documented and repeatable;
- the learner can explain the trade-offs recorded in ADRs.

# Domain and Flows

Last reviewed: 2026-08-02.

## 1. Domain model

The system contains three bounded contexts.

```text
Product Context
Product 1 ---- * InventoryReservationItem * ---- 1 InventoryReservation

Order Context
Order 1 ---- * OrderItem
Order 1 ---- * OutboxMessage (hardening phase)

Notification Context
ConsumedMessage 1 ---- 0..1 Notification
```

Cross-context identity uses stable scalar IDs, not shared entity references:

```text
OrderItem.productId ---- references meaning, not a database foreign key ----> Product.id
InventoryReservation.orderId ---- references meaning ----> Order.id
Notification.orderId ---- event snapshot ----> Order.id
```

## 2. Entity responsibilities

### Product context

| Entity | Responsibility |
| --- | --- |
| Product | Current catalog state, unit price, active flag, available stock |
| InventoryReservation | One order-level idempotent reservation request and lifecycle |
| InventoryReservationItem | Product, quantity, price/name snapshot, reserved/released state |
| ProductStockMovement | Optional audit extension for stock changes |

### Order context

| Entity | Responsibility |
| --- | --- |
| Order | Customer snapshot, lifecycle, total, dependency outcome |
| OrderItem | Immutable product/name/price/quantity/subtotal snapshot |
| OutboxMessage | Durable integration event pending broker publication |

### Notification context

| Entity | Responsibility |
| --- | --- |
| Notification | Simulated message visible to the demo user |
| ConsumedMessage | Idempotency record for one broker message |

## 3. Value objects and contracts

| Concept | Fields/rules |
| --- | --- |
| Money | `amount: decimal`, `currency: VND`; nonnegative |
| CustomerContact | normalized name and email |
| RequestedItem | product ID and positive quantity |
| ProductSnapshot | product ID, name, unit price, quantity |
| ReservationResult | order ID, reservation ID, snapshots, total |
| Integration metadata | message ID, occurred-at UTC, schema version, trace context |

The event contract must be self-contained. Notification Service must not call Order Service merely to build a message.

## 4. Product state

A product has an independent activity state:

| Current | Action | Next |
| --- | --- | --- |
| new | Create valid product | `active` or `inactive` |
| `active` | Deactivate | `inactive` |
| `inactive` | Activate | `active` |
| any | Update name/description/price/stock | same state |

Deactivation blocks new reservations but does not invalidate historical order/reservation snapshots.

## 5. Inventory reservation state

| Current | Action | Next |
| --- | --- | --- |
| new | Reserve all requested stock | `reserved` |
| new | Validation/business failure | no reservation |
| `reserved` | Release | `released` |
| `released` | Repeated release | `released` |
| `reserved` | Consume | deferred |
| any | Same order with mismatched request | no change; conflict |

Baseline states:

- `reserved`
- `released`

The baseline treats a confirmed order as retaining a reservation until cancellation. A production system would introduce fulfillment/consumption; that is outside scope.

## 6. Order state machine

| Current | Trigger | Next | Notes |
| --- | --- | --- | --- |
| new | Request accepted | `pending_inventory` | Order ID exists before remote call |
| `pending_inventory` | Full reservation succeeds | `confirmed` | Items and total become immutable |
| `pending_inventory` | Known product/stock rejection | `rejected` | Failure reason stored |
| `pending_inventory` | Timeout/ambiguous dependency outcome | `inventory_unknown` | Requires human/test reconciliation |
| `confirmed` | Release succeeds | `cancelled` | Terminal |
| `confirmed` | Release result ambiguous | `cancellation_pending` | No repeated blind compensation |
| `cancellation_pending` | Reconciliation confirms release | `cancelled` | Optional hardening |
| `rejected` | any normal user action | `rejected` | Terminal |
| `cancelled` | repeated cancellation | `cancelled` | Idempotent result |

Forbidden examples:

- `rejected -> confirmed`;
- `cancelled -> confirmed`;
- `inventory_unknown -> confirmed` without reconciliation;
- cancellation before confirmation.

## 7. Notification state

The baseline notification record has a simple state:

- `created`: event consumed and simulated message persisted;
- `read`: optional UI action;
- `failed`: not persisted as a business notification; broker error queue is operational evidence.

No external provider delivery state is modeled.

## 8. Product browse flow

```text
Angular
  -> GET /api/products
Gateway
  -> Product Service
Product Service
  -> Product DB
  <- active products ordered by name/id
Gateway
  <- response
Angular
  -> render price and available stock
```

Rules:

- shopper route shows active products only;
- operator route may request active/inactive;
- current stock is informational until reservation commits;
- Angular must still handle stock conflict after submission.

## 9. Product creation flow

1. Operator opens product form.
2. Angular validates basic required fields.
3. Angular posts to Gateway.
4. Gateway routes to Product Service.
5. Product Service validates authoritative rules.
6. Product Service stores product and returns `201`.
7. Angular refreshes catalog.

This flow demonstrates ordinary service-local CRUD and does not use RabbitMQ.

The current implementation verifies the Product Service portion of this flow through its native `/api/v1/products` endpoints and PostgreSQL integration tests. Gateway routing and Angular screens are intentionally deferred until Phase 4 and the remaining Phase 1 frontend work.

### Product update and lifecycle flow

1. An operator reads the Product and receives its current `ETag`, such as `"1"`.
2. The operator sends `PATCH /api/v1/products/{productId}` with the mutable fields and `If-Match: "1"`.
3. Product Service validates the fields, loads the Product from its own database, and checks the expected version.
4. PostgreSQL persists the update with an optimistic concurrency predicate on `id` and `version`; direct stock adjustment remains a learning-baseline operation and is separate from future reservation movements.
5. A successful update returns `200`, the new Product representation, and the incremented `ETag`.
6. A stale or racing update returns `409 PRODUCT_CONCURRENCY_CONFLICT`; the client must reload before retrying.

Activation and deactivation use the same PATCH contract by changing `isActive`. Deactivation removes the Product from the default shopper list while retaining it for operator listing. Product records are never hard-deleted.

## 10. Successful order creation flow

### Input

```json
{
  "customerName": "Nguyen Van A",
  "customerEmail": "a@example.com",
  "items": [
    { "productId": "uuid-1", "quantity": 2 },
    { "productId": "uuid-2", "quantity": 1 }
  ]
}
```

### Steps

1. Angular validates customer fields and quantities.
2. Angular sends `POST /api/orders` to Gateway.
3. Gateway forwards to Order Service and preserves trace context.
4. Order Service validates request and duplicate product IDs.
5. Order Service generates `orderId`.
6. Order Service stores customer snapshot, request hash, and status `pending_inventory`.
7. Order Service sends internal reservation request with `orderId`.
8. Product Service validates, checks idempotency, locks products, verifies all items, decrements stock, inserts reservation snapshots, and commits.
9. Product Service returns snapshots and total.
10. Order Service verifies response order ID and item set.
11. Order Service inserts immutable `OrderItem` rows.
12. Order Service calculates/verifies total from snapshots.
13. Order Service changes status to `confirmed`.
14. Direct-publish milestone publishes the event after commit; outbox milestone writes event in the same transaction.
15. Order Service returns `201 Created`.
16. Angular navigates to order detail.
17. Notification Service eventually consumes the event.
18. Notification Service persists one notification and consumed-message ID.
19. Angular notification page displays it after refresh.

## 11. Insufficient stock flow

1. Product Service locks all requested rows.
2. At least one row has insufficient stock.
3. Product Service rolls back the whole local transaction.
4. It returns `409 INSUFFICIENT_STOCK` with item details.
5. Order Service changes `pending_inventory` to `rejected`.
6. Order Service stores sanitized rejection code/detail.
7. No `OrderConfirmed` event is published.
8. Angular displays the failed item and refreshes product stock.

At no point may some items remain decremented.

## 12. Product not found or inactive flow

These are known business outcomes.

- Product Service returns `404 PRODUCT_NOT_FOUND` or `409 PRODUCT_INACTIVE`.
- Order Service records `rejected`.
- API returns a client-correctable response.
- No automatic retry occurs.

## 13. Product Service timeout flow

The remote outcome may be ambiguous: Product Service could have committed reservation just before the network response was lost.

Therefore Order Service must not assume failure and blindly call reserve again with a different identity.

Flow:

1. typed client reaches timeout/network failure;
2. Order Service records `inventory_unknown`;
3. API returns `503 INVENTORY_OUTCOME_UNKNOWN`;
4. log contains order ID and trace ID;
5. developer inspects Product reservation by order ID;
6. optional reconciliation endpoint/process determines final state.

Because reserve is idempotent by order ID, a controlled reconciliation may repeat the same request and receive the existing result.

## 14. Duplicate reservation request flow

Given Product Service receives the same `orderId` again:

### Same canonical item set

- return existing reservation and snapshots;
- do not decrement stock again;
- log idempotent replay.

### Different item set

- return `409 RESERVATION_REQUEST_MISMATCH`;
- do not mutate stock;
- log a high-value warning.

Canonical item set comparison sorts by product ID and compares quantity; request order is irrelevant.

## 15. Order cancellation flow

1. Angular loads order detail.
2. API reports `canCancel=true` only for `confirmed`.
3. Angular posts cancellation.
4. Order Service enters a guarded cancellation transition.
5. Order Service calls Product Service release using `orderId`.
6. Product Service finds and locks reservation/items/products, restores quantities, marks released, and commits.
7. Order Service changes order to `cancelled`.
8. API returns current order.
9. Repeated cancellation returns current cancelled state or a stable conflict without another stock change.

If the release response is lost, Order Service uses `cancellation_pending`, not a false success.

## 16. Event publishing flow

### Direct-publish learning milestone

```text
Order DB commit -> publish event -> return
```

Known gap: process failure between commit and publish loses the event.

### Transactional outbox milestone

```text
Order confirmation transaction
  -> update order
  -> insert outbox row
  -> commit

Outbox dispatcher
  -> claim pending row
  -> publish RabbitMQ message
  -> mark published
```

The outbox milestone is preferred for the final repository because it teaches the database/broker consistency boundary.

## 17. Notification consume flow

1. RabbitMQ delivers `OrderConfirmedV1`.
2. MassTransit invokes consumer.
3. Consumer validates supported schema version.
4. Consumer opens Notification DB transaction.
5. Consumer checks/inserts `ConsumedMessage(messageId)`.
6. If duplicate, no new notification is created and the message is acknowledged.
7. If new, consumer creates a readable notification and commits.
8. Read API exposes the record.

## 18. Consumer failure flow

Transient exception:

- MassTransit applies bounded retry;
- logs include attempt number;
- transaction is rolled back.

Exhausted exception:

- message moves to MassTransit error queue;
- main queue continues;
- operator/developer inspects message and exception headers;
- replay is manual in the learning baseline.

The consumer must not swallow exceptions and acknowledge unprocessed messages.

## 19. Notification Service downtime flow

```text
Order Service -> RabbitMQ durable queue -> [Notification Service stopped]
```

Expected behavior:

- order remains confirmed;
- message remains ready/unacked depending timing;
- restarting consumer processes backlog;
- one notification appears.

This demonstrates temporal decoupling.

## 20. Concurrent stock flow

Two Product Service requests target the last unit.

Required result:

- transaction/locking serializes competing stock checks;
- first successful transaction decrements to zero;
- second sees zero and rejects;
- stock never becomes negative;
- no two active reservations own the same last unit.

Tests should use parallel tasks and real PostgreSQL, not an in-memory provider.

## 21. Gateway failure flow

When Gateway is unavailable:

- Angular API calls fail;
- internal services may still be healthy;
- direct service URLs are not a supported browser fallback;
- UI shows a general connectivity error;
- health checks distinguish Gateway from downstream services.

## 22. Database failure flows

### Product DB unavailable

- Product readiness fails;
- product endpoints return dependency failure;
- new orders cannot reserve;
- Order Service handles Product HTTP failure.

### Order DB unavailable

- order endpoints fail before remote reservation should begin;
- Product browsing remains available.

### Notification DB unavailable

- consumer retries and eventually moves message to error queue if outage exceeds policy;
- order confirmation is unaffected.

## 23. RabbitMQ failure flows

### Direct publish mode

If RabbitMQ is unavailable after order confirmation:

- order may already be confirmed;
- API behavior must be explicit;
- returning `500/503` can cause caller confusion even though order committed;
- this is the reason to add the outbox.

### Outbox mode

- order confirmation commits with pending outbox row;
- dispatcher retries later;
- API can return confirmed order;
- readiness/metrics show broker dispatch backlog.

## 24. Frontend state flows

### Catalog

`loading -> loaded | empty | error`

### Checkout

`editing -> submitting -> confirmed | rejected | dependency_error`

### Order detail

`loading -> loaded -> cancelling -> cancelled | cancellation_pending | error`

Angular must not optimistically claim confirmation or cancellation before API confirmation.

## 25. Trace flow

A single trace should connect:

```text
Gateway inbound span
  -> Order API span
     -> PostgreSQL span
     -> Product HTTP client span
        -> Product API span
           -> Product PostgreSQL span
     -> publish/outbox span
        -> Notification consume span
           -> Notification PostgreSQL span
```

Even before a trace backend is installed, structured logs should expose trace IDs.

## 26. Manual learning experiments

| Experiment | Expected lesson |
| --- | --- |
| Stop Product Service | Synchronous dependency blocks order creation |
| Stop Notification Service | Asynchronous consumer can recover later |
| Stop RabbitMQ in direct mode | DB/broker dual-write problem |
| Enable outbox then stop RabbitMQ | Durable pending event |
| Send duplicate message | At-least-once delivery and idempotency |
| Run concurrent last-stock orders | Database concurrency control |
| Change product price after order | Snapshot vs current catalog state |
| Try cross-database access | Service ownership boundary |

# System Architecture

Last reviewed: 2026-08-02.

## 1. Architecture summary

The target topology below is implemented incrementally. In Phase 0, the repository contains independent ASP.NET Core host projects, a YARP-capable Gateway skeleton, an Angular workspace, and PostgreSQL/RabbitMQ infrastructure. Business routes, database schemas, and message flows remain phase-scoped work.

Mini Order System is a small distributed system with one Angular SPA, one YARP Gateway, two HTTP business services, one message-consuming worker/API, RabbitMQ, and service-owned PostgreSQL databases.

```text
Browser
  |
  | HTTP
  v
Angular SPA
  |
  | /api/*
  v
YARP Gateway
  |-------------------------------|
  |                               |
  v                               v
Product Service <---HTTP--- Order Service
  |                               |
  v                               | publish OrderConfirmed
Product DB                        v
                              RabbitMQ
                                  |
                                  v
                         Notification Service
                                  |
                                  v
                         Notification DB
```

The primary learning boundary is:

- **HTTP request/response** when Order Service needs inventory confirmation before responding;
- **broker event** when Order Service only announces that confirmation already happened.

## 2. Runtime boundaries

| Process/container | Technology | Responsibility |
| --- | --- | --- |
| `web` | Angular static build served by nginx; Angular dev server locally | User interface and browser state |
| `gateway` | ASP.NET Core + YARP | Public routing, trace propagation, optional cross-cutting policy |
| `product-service` | ASP.NET Core Web API | Product catalog, stock, inventory reservations |
| `order-service` | ASP.NET Core Web API | Order state, item snapshots, totals, event production |
| `notification-service` | .NET Worker plus optional minimal read API | Event consumption, idempotency, simulated notifications |
| `postgres` | PostgreSQL | Hosts separate logical databases for local simplicity |
| `rabbitmq` | RabbitMQ Management image | Durable message transport |
| optional `otel-collector` | OpenTelemetry Collector | Trace/metric export in hardening phase |

Using one PostgreSQL container locally does not mean one shared database. Each service uses a different database and credential. Production may use separate server instances without changing ownership.

## 3. Public network boundary

Only these components should be published in production-like deployment:

- web/nginx or Gateway public HTTP/HTTPS;
- RabbitMQ management only in restricted development environments;
- PostgreSQL and RabbitMQ application ports remain internal.

Recommended public request path:

```text
Browser -> nginx/web -> /api -> gateway -> service
```

For native development, Angular may proxy `/api` to Gateway. The frontend must not compile internal host names such as `http://order-service:8080`.

## 4. Service boundaries

### Product Service

Owns:

- products;
- available stock;
- inventory reservations;
- stock release;
- authoritative product snapshots at reservation time.

Must not own:

- customer identity;
- order status;
- notifications;
- order totals as a separate source of truth.

### Order Service

Owns:

- order request and lifecycle;
- customer contact snapshot;
- order-item commercial snapshots returned by Product Service;
- total amount;
- event production intent/outbox when enabled.

Must not own:

- current product record;
- current inventory;
- notification delivery state.

### Notification Service

Owns:

- consumed message IDs;
- generated notification records;
- consumer retry/duplicate diagnostics.

Must not:

- query Order Service database;
- call Product Service to reconstruct order content;
- decide whether an order is confirmed.

### Gateway

Owns no business state. It may own:

- routes and clusters;
- request limits;
- CORS policy;
- optional public authentication in a later phase;
- gateway logs and health.

## 5. Communication model

### Browser to Gateway

Angular calls relative URLs:

```text
/api/products
/api/orders
/api/notifications
```

Gateway maps them to service clusters.

### Gateway to services

YARP uses route/cluster configuration. A representative mapping:

| Public prefix | Destination |
| --- | --- |
| `/api/products/{**catch-all}` | Product Service `/api/v1/products/{**catch-all}` |
| `/api/orders/{**catch-all}` | Order Service `/api/v1/orders/{**catch-all}` |
| `/api/notifications/{**catch-all}` | Notification Service `/api/v1/notifications/{**catch-all}` |

Exact transforms are executable in Gateway configuration and covered by tests.

### Order Service to Product Service

Order Service uses a typed `HttpClient` against an internal base address:

```text
http://product-service:8080/internal/v1/inventory
```

The call:

- carries W3C trace context;
- has an explicit timeout;
- sends a stable `orderId`;
- uses JSON contracts independent from EF entities;
- distinguishes business `409` responses from infrastructure failures.

Automatic retry is not applied blindly to reservation POST requests. Idempotency allows a carefully bounded retry only when the request identity and server behavior make it safe.

### Order Service to RabbitMQ

After confirmation, Order Service publishes `OrderConfirmedV1`.

Baseline implementation options:

1. direct publish after database commit for first learning milestone;
2. transactional outbox for the final hardening milestone.

Documentation and status must state which option is implemented. Direct publish has a known message-loss window; outbox closes that window.

### RabbitMQ to Notification Service

MassTransit creates a durable receive endpoint. Delivery is at least once.

Notification Service:

1. begins a local database transaction;
2. checks/inserts the message ID;
3. inserts notification when new;
4. commits;
5. acknowledges the message.

If processing throws, MassTransit retry/error behavior applies. Poison messages move to an error queue rather than blocking the main queue indefinitely.

## 6. Request flow: successful order

```text
1. Angular POST /api/orders
2. Gateway forwards to Order Service
3. Order Service validates request
4. Order Service creates pending_inventory order
5. Order Service POSTs internal reservation request to Product Service
6. Product Service locks/checks product rows
7. Product Service decrements stock and stores reservations in one transaction
8. Product Service returns authoritative snapshots
9. Order Service stores item snapshots/total and marks confirmed
10. Order Service publishes or enqueues OrderConfirmedV1
11. Order Service returns 201
12. Notification Service consumes later and stores notification
13. Angular polls/refreshes notification list for the baseline
```

The notification step is intentionally outside the synchronous response path.

## 7. Distributed consistency

There is no atomic transaction across Order DB and Product DB.

The baseline uses explicit states and compensating actions:

- `pending_inventory` indicates order persisted but inventory not yet confirmed;
- `rejected` records a known business rejection;
- `inventory_unknown` records an ambiguous infrastructure outcome;
- cancellation calls Product Service release and does not mark final cancellation until release is known.

A reconciliation extension may inspect non-terminal technical states. The baseline does not pretend that local ACID transactions solve cross-service consistency.

## 8. Data ownership and access control

| Data | Owner | Other service access |
| --- | --- | --- |
| Product definitions | Product Service | HTTP read/reservation contracts |
| Stock | Product Service | Internal reservation/release HTTP only |
| Inventory reservations | Product Service | Internal HTTP result only |
| Orders and item snapshots | Order Service | Public Order API and integration events |
| Notifications | Notification Service | Read-only Notification API |
| Broker topology | Messaging infrastructure | Producer/consumer configuration |

Rules:

- no cross-database joins;
- no service receives another service's connection string;
- no shared EF model;
- shared contracts are DTOs/messages only;
- analytics across services are deferred.

## 9. Backend internal architecture

Each small service uses a pragmatic feature-oriented structure:

```text
<Service>.Api/
|-- Features/
|   |-- Products/
|   |-- Inventory/
|   \-- ...
|-- Persistence/
|   |-- AppDbContext.cs
|   |-- Entities/
|   \-- Migrations/
|-- Contracts/
|-- Infrastructure/
|   |-- Messaging/
|   |-- Http/
|   |-- Errors/
|   \-- Observability/
|-- Program.cs
\-- appsettings.json
```

Rules:

```text
Endpoint -> application handler/service -> DbContext or adapter
                                      \-> HTTP/message contract
```

For this project, adding separate Domain/Application/Infrastructure assemblies to every service is optional and discouraged unless it clarifies rather than obscures the learning flow.

## 10. Angular architecture

Angular uses standalone APIs and feature folders:

```text
src/app/
|-- core/
|   |-- api/
|   |-- error/
|   |-- layout/
|   \-- config/
|-- shared/
|   |-- components/
|   |-- pipes/
|   \-- models/
|-- features/
|   |-- catalog/
|   |-- checkout/
|   |-- orders/
|   |-- notifications/
|   \-- product-admin/
|-- app.routes.ts
\-- app.config.ts
```

Responsibilities:

- components display and collect user interaction;
- feature services call Gateway APIs;
- Reactive Forms own checkout/product form validation;
- RxJS/signals coordinate local view state;
- API responses remain the authoritative state;
- browser storage is optional for non-authoritative draft quantities only.

## 11. Reliability architecture

### Timeouts

- Gateway proxy timeout is bounded.
- Order-to-Product HTTP timeout is explicit.
- Database commands use provider timeouts.
- graceful shutdown has a bounded consumer completion window.

### Retry

Safe candidates:

- transient GET calls;
- broker consumer processing with bounded attempts;
- outbox dispatch.

Unsafe by default:

- creating a new order without idempotency;
- inventory reservation without stable `orderId`;
- cancellation without idempotent release.

### Circuit breaker

A circuit breaker may be added to the Product client after the basic flow is understood. It should return a clear dependency-unavailable result rather than obscure repeated failures.

### Idempotency

| Operation | Key |
| --- | --- |
| Inventory reserve | `orderId` plus canonical item-set hash |
| Inventory release | `orderId` |
| Notification consume | broker `MessageId` |
| Optional order create retry | client `Idempotency-Key` extension |

### Back pressure

Notification throughput is controlled by consumer concurrency/prefetch. The baseline uses conservative values so logs remain understandable.

## 12. Observability architecture

All processes use structured logs with:

- `service.name`;
- environment;
- trace ID/span ID;
- order ID when available;
- product ID when available;
- message ID;
- event type;
- dependency outcome.

W3C trace context is the primary distributed trace standard. `X-Correlation-ID` may be accepted for human support, but it must not replace `traceparent`.

Optional OpenTelemetry pipeline:

```text
Services -> OTLP -> OpenTelemetry Collector -> Jaeger/Tempo
```

The project can later send logs to the user's centralized Log Monitoring System, but this integration is not required for the baseline.

## 13. Health model

| Check | Liveness | Readiness |
| --- | --- | --- |
| Process is running | yes | yes |
| Own database reachable | no | yes |
| RabbitMQ reachable for producer/consumer | no | yes where required |
| Product Service reachable from Order Service | no | optional/dependency readiness |
| Business stock/order valid | no | no |

Liveness must not fail because an external dependency is temporarily unavailable; otherwise orchestration may restart healthy processes unnecessarily.

## 14. Security baseline

This is a local learning project, but it still applies basic controls:

- request body size limits;
- input validation;
- safe Problem Details;
- parameterized EF Core queries;
- no secrets in browser configuration;
- separate database users;
- RabbitMQ credentials from environment/secret input;
- CORS restricted to configured frontend origin;
- HTTPS required for any public deployment;
- internal endpoints not routed by Gateway;
- no verbose stack traces outside development;
- container ports minimized.

Authentication is deferred. The documentation must not describe the operator interface as secure.

## 15. Scalability boundary

The architecture demonstrates independent processes, not production scale.

Possible independent scale:

- Product Service for read/reservation load;
- Order Service for order traffic;
- Notification consumers for queue depth.

Constraints before scaling replicas:

- outbox/consumer idempotency must be implemented;
- migrations must run as an explicit deployment step;
- broker queues must be durable;
- readiness must be correct;
- no process-local singleton may be the source of truth.

## 16. Failure boundaries

| Failure | Expected effect |
| --- | --- |
| Gateway down | Browser cannot reach APIs |
| Product Service down | Product reads and new order reservation fail |
| Order Service down | Order APIs fail; product browsing can still work |
| Notification Service down | Orders can confirm; notifications wait in queue |
| RabbitMQ down | Direct-publish baseline risks order response/event failure; outbox mode retains events in DB |
| Product DB down | Product Service not ready; orders cannot reserve |
| Order DB down | Orders unavailable; products remain browsable |
| Notification DB down | Consumer retries/fails; order confirmation remains independent |

These boundaries are core teaching material and must be tested manually and automatically where practical.

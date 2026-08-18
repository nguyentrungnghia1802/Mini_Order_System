# Development and Testing

Last reviewed: 2026-08-18.

## 1. Prerequisites

Recommended:

- .NET 10 SDK;
- Node.js version supported by the pinned Angular version;
- npm;
- Docker Desktop with Docker Compose;
- Git;
- optional PostgreSQL client;
- optional RabbitMQ management UI/browser.

The repository pins .NET SDK `10.0.302`, Node.js `24.15.0`, and npm `11.12.1`. Angular 22.1.2 requires the pinned Node version or a compatible newer version.

The repository must pin:

- .NET SDK in `global.json`;
- NuGet packages in central package management;
- Node/npm expectations in `.nvmrc` or `package.json`;
- Angular packages in lockfile.

## 2. Environment setup

```bash
git clone <repository>
cd MicroShop
cp .env.example .env
```

Example non-secret local variables:

```dotenv
POSTGRES_HOST=postgres
POSTGRES_PORT=5432

PRODUCT_DB_NAME=microshop_product
PRODUCT_DB_USER=product_app
PRODUCT_DB_PASSWORD=local-product-password

ORDER_DB_NAME=microshop_order
ORDER_DB_USER=order_app
ORDER_DB_PASSWORD=local-order-password

NOTIFICATION_DB_NAME=microshop_notification
NOTIFICATION_DB_USER=notification_app
NOTIFICATION_DB_PASSWORD=local-notification-password

RABBITMQ_HOST=rabbitmq
RABBITMQ_USER=microshop
RABBITMQ_PASSWORD=local-rabbitmq-password

PRODUCT_SERVICE_URL=http://product-service:8080
ALLOWED_ORIGINS=http://localhost:4200,http://localhost:8080
```

Local passwords are demo values. Public deployment must use generated secrets and untracked configuration.

## 3. Recommended development modes

### Full Docker mode

Best for learning networking and service independence.

```bash
docker compose --env-file .env -f deploy/compose.yaml up --build -d
docker compose --env-file .env -f deploy/compose.yaml ps --all
```

Expected public endpoints:

| Component | URL |
| --- | --- |
| Web | `http://localhost:8080` |
| Gateway direct | `http://localhost:8081` if the debug override is selected |
| RabbitMQ management | `http://localhost:15672` in development only |

Service-native ports may be published only in `compose.override.yaml` for debugging. The base file keeps Gateway, Product, Order, Notification, PostgreSQL, and RabbitMQ AMQP ports private; only Web 8080 and RabbitMQ management 15672 are published.

### Infrastructure in Docker, apps native

Best for debugging .NET/Angular:

```bash
docker compose -f deploy/compose.yaml up postgres rabbitmq
dotnet run --project src/Services/ProductService/MicroShop.ProductService
dotnet run --project src/Services/OrderService/MicroShop.OrderService
dotnet run --project src/Services/NotificationService/MicroShop.NotificationService
dotnet run --project src/Gateway/MicroShop.Gateway
npm install --prefix web/microshop-ui
npm start --prefix web/microshop-ui
```

Native service URLs use localhost-specific configuration, not Docker DNS names.

## 4. Build commands

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release

cd web/microshop-ui
npm ci
npm run build
npm test
```

Root scripts may wrap these commands.

## 5. Database commands

Recommended repository scripts:

```bash
./scripts/db-migrate-product.sh
./scripts/db-migrate-order.sh
./scripts/db-migrate-notification.sh
./scripts/db-migrate-all.sh .env
./scripts/db-seed-products.sh
```

PowerShell equivalents should exist for a Windows-first learning environment.

Direct EF example:

```bash
dotnet ef database update \
  --project src/Services/ProductService/MicroShop.ProductService \
  --startup-project src/Services/ProductService/MicroShop.ProductService
```

For the current Product slice, set `PRODUCT_DB_HOST`, `PRODUCT_DB_PORT`, `PRODUCT_DB_NAME`, `PRODUCT_DB_USER`, and the untracked `PRODUCT_DB_PASSWORD` environment variable before using `scripts/db-migrate-product.*` or `scripts/db-seed-products.*`. Install the pinned CLI with `dotnet tool install --tool-path <local-tool-dir> dotnet-ef --version 10.0.10` when `dotnet ef` is not already available.

`db-reset-local` is destructive and requires explicit reset/volume-deletion flags, an exact typed confirmation, and an explicit non-Development override when applicable. Normal `docker compose down` preserves named volumes.

## 6. RabbitMQ inspection

Development management UI should show:

- exchange(s) created by MassTransit;
- Notification receive queue;
- ready/unacked counts;
- error queue after poison-message exercise.

Do not manually delete queues during normal tests unless the test explicitly studies topology recovery.

Useful learning questions:

- Is the queue durable?
- What happens to ready messages when the consumer stops?
- What headers identify message type and trace?
- Where do exhausted messages go?
- Does restarting a service create duplicate topology?

## 7. Seed data

Minimal product seed:

| Product | Price | Stock | Active |
| --- | ---: | ---: | --- |
| Mechanical Keyboard | 1,200,000 VND | 10 | yes |
| Wireless Mouse | 450,000 VND | 20 | yes |
| USB-C Hub | 800,000 VND | 0 | yes |
| Archived Headset | 600,000 VND | 5 | no |

Seed is idempotent by deterministic product ID or unique seed key.

No real names/emails are used.

## 8. Validation pipeline

Recommended CI/local validation order:

```bash
dotnet format MicroShop.sln --verify-no-changes
dotnet build --configuration Release
dotnet test --configuration Release

cd web/microshop-ui
npm ci
npm run lint
npm test -- --watch=false
npm run build
```

Then integration/E2E:

```bash
docker compose -f deploy/compose.test.yaml up --build --abort-on-container-exit
```

The current infrastructure, Product migration, and native Order API smoke checks are:

```bash
docker compose --env-file .env.example -f deploy/compose.yaml config
docker compose --env-file .env.example -f deploy/compose.yaml up -d
docker compose --env-file .env.example -f deploy/compose.yaml ps

dotnet ef database update \
  --project src/Services/ProductService/MicroShop.ProductService \
  --startup-project src/Services/ProductService/MicroShop.ProductService
```

The five application images and full application Compose stack are implemented. The Compose smoke path applies the three migrations, starts all application services, and verifies Web/Gateway Product/Order/Notification routing. `compose.test.yaml`, browser Playwright coverage, and CI image execution remain deferred. CI applies the Product migration to an empty PostgreSQL service database.

The current Order API integration suite applies `20260801204113_InitialOrderSchema` to PostgreSQL Testcontainers and exercises create/list/detail, browser-field rejection, Product business failures, pagination, stable Problem Details, and the typed Product HTTP path. The Gateway suite uses a real in-process Kestrel downstream to verify public Product/Order path transforms, trace propagation, CORS/health, stable downstream `502`, and internal-route rejection.

The Angular workspace now centralizes browser API calls in `ProductApiService` and `OrderApiService`, both using same-origin Gateway paths. Its interceptor maps browser network failures and Gateway `502`/`503`/`504` responses to `GatewayApiError`; the Angular Product catalog/operator UI suite contains 11 passing tests.

The exact scripts become source of truth when repository exists.

## 9. Test strategy

### Unit tests

Use for:

- input canonicalization;
- money/total calculation;
- order transition guards;
- request hash calculation;
- notification message formatting;
- error mapping;
- Angular pure services/pipes/components where valuable.

Do not mock the entire system and call it a microservices test.

### Product integration tests

Use real PostgreSQL through Testcontainers.

Required cases:

- create/list/update Product and activate/deactivate lifecycle;
- reject negative stock/price;
- inactive Product visibility and database constraints;
- stale or competing `If-Match` updates return a stable concurrency conflict;
- reserve one item;
- reserve multiple items atomically;
- insufficient stock rolls back all;
- duplicate same reservation returns existing result;
- duplicate mismatched reservation conflicts;
- release restores once;
- concurrent last-stock request permits one success.

The implemented Product API tests use PostgreSQL Testcontainers and apply the real Product migrations. They cover update rounding/versioning, activation filtering, competing PATCH requests, atomic reservation failures, authoritative snapshots, replay/mismatch, idempotent release, and concurrent last-stock requests. EF Core InMemory is not used.

The current Product suite contains 21 passing tests and the reservation cases are implemented in `InventoryApiTests`, including controlled reservation lookup by order ID. The Order suite contains 60 passing tests; the fake client is only enabled explicitly in the legacy API fixture, while the runtime path and integration tests use the typed Product HTTP client, bounded idempotent release/lookup retry, cancellation release flow, and transactional outbox dispatcher.

### Order integration tests

Use real Order PostgreSQL and either Product Service test host/container or an explicit HTTP stub for isolated orchestration cases.

The current Order tests apply `20260801204113_InitialOrderSchema`, `20260817203650_AddOrderOutbox`, and `20260818044623_AddOrderReconciliation` to fresh PostgreSQL Testcontainers, persist immutable item snapshots/state history/outbox/reconciliation data, verify status constraints, readiness/OpenAPI, database credential isolation, and exercise the typed Product HTTP boundary. The 60-test suite covers creation, known rejection, listing, detail, cancellation, pagination, stable error codes, authoritative snapshots, unavailable dependency, timeout ambiguity, caller cancellation, bounded idempotent release/lookup retry, reserve no-retry safety, `inventory_unknown`, `cancellation_pending` persistence and reconciliation, optimistic-concurrency rejection, direct-publish demonstration isolation, atomic outbox persistence, stable event identity, concurrent claim, retry/dead-letter, lease-expiry recovery, RabbitMQ outage/recovery, lookup mapping, and audit-safe reconciliation with a durable confirmed Order. The Angular suite has 21 passing tests covering Gateway API contracts, Product catalog/operator screens, checkout outcomes, Order list/detail, cancellation, duplicate-submit suppression, and Notification list/mark-as-read UI behavior.

### Outbox operations and outage test

`OrderOutboxRabbitMqTests` starts PostgreSQL and RabbitMQ Testcontainers, stops RabbitMQ before a confirmed Order is submitted, verifies that the Order and un-published outbox record are durable and that readiness remains healthy within policy, then restarts RabbitMQ and a dispatcher host after lease expiry. The test waits for the same outbox row to publish and asserts a retry attempt. `OutboxBacklogQuery` and `OrderOutboxHealthCheck` are also exercised through the service readiness endpoint; the read-only operator SQL is available through `scripts/db-outbox-status.ps1/.sh`.

Cases:

- pending -> confirmed;
- known Product conflict -> rejected;
- timeout -> inventory_unknown;
- total from Product snapshot;
- confirmation event/outbox row;
- cancellation state guards;
- release unknown -> cancellation_pending.

### Transactional outbox tests

`OutboxDispatcherTests` use PostgreSQL Testcontainers and a fake transport at the transport boundary. They verify stable MessageId publication and completion state, `FOR UPDATE SKIP LOCKED` claim exclusivity under concurrent dispatchers, bounded retry/dead-letter behavior, and recovery after a worker loses its lease. The service-level direct publisher remains tested only to demonstrate the historical post-commit failure window; it is not registered in the production confirmation path.

### Resilience and shutdown tests

`ProductInventoryClientTests` verifies that reserve does not retry after a transient response, while the same-order idempotent release and read-only reservation lookup retry one transient HTTP/transport failure within the shared five-second operation cap. It also verifies malformed/business responses are not retried and caller cancellation remains an `OperationCanceledException`. `ServiceReadinessTests` verifies that the shared lifecycle check changes readiness to unhealthy when `ApplicationStopping` is signaled and that host shutdown options stay within the 1–60 second bound. Order and Notification configure MassTransit startup/stop timeouts from the same bounded host timeout.

The resilience checks run with:

```powershell
dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ProductInventoryClientTests|FullyQualifiedName~OrderProductHttpIntegrationTests"
dotnet test tests/MicroShop.Gateway.Tests/MicroShop.Gateway.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ServiceReadinessTests
```

### Notification integration tests

The current `MicroShop.NotificationService.Tests` project starts PostgreSQL 17 and RabbitMQ 4 Testcontainers, applies `20260817185808_InitialNotificationSchema`, and verifies the host readiness check, consumer persistence boundary, read API, and real broker behavior:

- consume event creates one Notification and one `ConsumedMessage`;
- duplicate event creates one Notification;
- unsupported schema fails before side effects;
- customer email, order ID, amount, currency, body, and trace ID survive the readback.
- read API email/order filters, stable pagination, OpenAPI, validation, not-found, and idempotent mark-as-read behavior.
- RabbitMQ publish/consume creates the Notification through the actual service host;
- duplicate broker delivery keeps one Notification;
- unsupported schema retries and moves to the durable endpoint error queue;
- publisher completion is independent of a stopped Notification consumer;
- a queued message survives Notification host restart and is consumed after restart.

- concurrent duplicate redelivery through independent PostgreSQL contexts creates one inbox row and one Notification;
- a failed Notification insert rolls back the consumed-message insert;
- a Notification process restart between redelivery attempts preserves idempotency.

The integration fixture uses a disposable RabbitMQ container because the current infrastructure Compose intentionally keeps AMQP port 5672 private. The consumer uses only the Notification connection and EF model; full application-container Compose and Angular E2E remain later gates.

### Gateway integration tests

Use `WebApplicationFactory` for the Gateway and a dynamic loopback Kestrel server as the downstream. The suite must verify:

- Product public route and `/api/v1/products` transform;
- Order public route and `/api/v1/orders` transform;
- query preservation and W3C `traceparent` forwarding;
- CORS, liveness, and readiness;
- stable `502 DOWNSTREAM_UNAVAILABLE` for an unavailable destination;
- `404 GATEWAY_ROUTE_NOT_FOUND` for `/internal/*` without forwarding.

The current `MicroShop.Gateway.Tests` project contains 9 passing tests, including lifecycle readiness transition and bounded shutdown-option wiring. The full .NET solution contains 108 passing tests: 1 Architecture, 2 Contracts, 15 Notification, 9 Gateway, 60 Order, and 21 Product. The contract suite verifies the stable JSON shape for `OrderConfirmedV1`; the Notification suite verifies liveness/readiness, PostgreSQL-backed consumer persistence, filters/pagination, OpenAPI, mark-as-read, real RabbitMQ publish/consume, retry/error queue, duplicate delivery, concurrent duplicate redelivery, transaction rollback, publisher independence, queued restart, and process restart between redelivery attempts; the Gateway suite verifies the Notification public path transform in addition to Product/Order routes and shutdown readiness; the Order suite covers direct-publish demonstration isolation plus transactional outbox identity, claim, bounded retry/dead-letter, lease recovery, RabbitMQ outage/recovery, safe Product-client retries, cancellation, and reconciliation.

### Contract tests

Verify:

- Product internal request/response JSON;
- `OrderConfirmedV1` serialization;
- Gateway paths;
- OpenAPI route inventory;
- event additive compatibility.

### End-to-end tests

Minimal Playwright flow:

1. load catalog;
2. add quantity;
3. submit checkout;
4. observe confirmed order detail;
5. open notification list and observe eventual notification;
6. cancel order;
7. observe stock restored.

E2E should poll with a bounded timeout for asynchronous notification, not use arbitrary long sleeps.

## 10. Failure-injection exercises

These are part of the curriculum.

### Exercise A: Stop Product Service

```bash
docker compose -f deploy/compose.yaml stop product-service
```

Expected:

- catalog fails;
- order creation returns bounded dependency error;
- Order Service remains running;
- logs connect failure to trace/order ID.

### Exercise B: Stop Notification Service

```bash
docker compose -f deploy/compose.yaml stop notification-service
```

Create an order, inspect RabbitMQ ready count, restart consumer, verify notification.

### Exercise C: Stop RabbitMQ

Compare direct publish milestone and transactional outbox milestone. Document the observed difference.

### Exercise D: Duplicate message

Use a test publisher or replay the same message ID. Verify one notification.

### Exercise E: Concurrent stock

Run two requests against stock `1`; verify one success.

## 11. Test data isolation

- each integration test uses unique IDs;
- tests reset/truncate only their isolated database/container;
- tests do not share developer databases;
- parallel tests are enabled only when fixtures are independent;
- broker endpoint names include test-run identifier when necessary;
- E2E uses a dedicated Compose project name/volumes.

## 12. Definition of done

A feature is done when:

- requirements and state rules are clear;
- code compiles with strict settings;
- unit tests cover pure rules;
- integration tests cover database/HTTP/broker behavior;
- expected error code exists;
- OpenAPI/event contract is updated;
- logs/trace context are present;
- Docker mode works;
- docs are updated;
- no service boundary is bypassed.

## 13. Common errors

### `Connection refused` to `product-service`

Cause:

- native process is using Docker DNS name; or
- Product container is not ready.

Fix:

- native config uses `http://localhost:<debug-port>`;
- Compose config uses `http://product-service:8080`;
- inspect readiness and network.

### Gateway returns `502 Bad Gateway`

Check:

- cluster destination;
- service container health;
- container network membership;
- path transform;
- service listening address `0.0.0.0`, not only localhost.

### EF migration reports wrong database

Check the service-specific connection string. Never point all services to one database as a shortcut.

### RabbitMQ consumer queue not visible

Check:

- Notification Service started;
- MassTransit endpoint name;
- credentials/vhost;
- startup logs;
- management UI vhost selection.

### Order confirms but no notification

Check:

1. event/outbox exists;
2. broker publish succeeded;
3. queue ready/unacked count;
4. consumer logs;
5. notification DB;
6. error queue.

### Duplicate notification appears

Check unique `source_message_id` and consumed-message transaction. Do not fix only with in-memory caching.

### Stock becomes negative

This is a critical bug. Check:

- database constraint;
- transaction isolation/row locking;
- concurrent integration test;
- direct stock updates;
- reservation idempotency.

### Angular calls service port directly

Replace absolute service URL with Gateway-relative `/api`. Internal Docker names must never enter browser bundles.

### Test passes with EF InMemory but fails in PostgreSQL

Move correctness test to PostgreSQL Testcontainers. InMemory does not model relational locks, constraints, or transactions accurately.

## 14. Debugging distributed requests

Use trace/order/message IDs:

1. copy trace ID from Angular error or Gateway response;
2. search Gateway logs;
3. search Order logs;
4. inspect Product logs and DB reservation;
5. inspect outbox/message ID;
6. inspect RabbitMQ;
7. inspect Notification logs/database.

The learner should practice this sequence instead of debugging each process in isolation.

## 15. Optional integration with centralized log monitoring

After baseline:

- configure structured JSON logs;
- add an exporter/agent;
- attach `service.name`, environment, trace ID;
- send logs from each process to the existing Log Monitoring System;
- verify one order trace can be searched across services.

This is an extension, not a dependency of Mini Order System.

### Observability core validation

`MicroShop.ServiceDefaults` configures JSON console logging scopes, W3C activity IDs, ASP.NET Core request instrumentation, HttpClient tracing, a shared ActivitySource/Meter, and optional OTLP export through `OTEL_EXPORTER_OTLP_ENDPOINT`. `UseMicroShopRequestObservability` records only method, endpoint, status, duration, trace/span IDs, service/environment, and bounded route IDs; it does not log request bodies, query payloads, credentials, or tokens. Order/Product/Notification code adds low-cardinality outcome metrics and stable event codes while retaining Order, Reservation, and Message IDs in relevant logs.

`ObservabilityTests` verifies service identity registration, W3C parent/consumer span relationships, and bounded operation/result metric tags. Gateway route tests continue to verify incoming `traceparent` reaches the downstream route; Order Product-client integration tests verify the same context reaches Product; Notification RabbitMQ integration tests verify durable publish/consume behavior used by the consumer span boundary. The cross-service trace/log walkthrough remains a Phase 8 final-gate check.

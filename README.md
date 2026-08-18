# MicroShop — Mini Order System

MicroShop is a deliberately small learning project for Angular, ASP.NET Core, YARP, PostgreSQL, RabbitMQ, synchronous HTTP, asynchronous events, database-per-service ownership, idempotency, and distributed failure handling. It is not a production e-commerce platform.

## Current status

Phase 0 bootstrap is implemented. Product has its own PostgreSQL model/migrations, deterministic development seed, service-native catalog/detail/create/update API, activate/deactivate lifecycle, optimistic version checks, Product-owned atomic inventory reservation/release/query API, readiness/OpenAPI, and PostgreSQL Testcontainers tests. Order has its own database and state history, a native HTTP API, a typed Product inventory client, authoritative reservation orchestration, timeout/availability mapping, `inventory_unknown`, guarded cancellation with `cancellation_pending`, and a transactional outbox for confirmed events. The versioned `MicroShop.Contracts.Orders.OrderConfirmedV1` wire contract is implemented and JSON-tested, MassTransit RabbitMQ transport/options, bounded retry, a lease-based Order outbox dispatcher, and a durable Notification endpoint are configured, and Notification now owns its PostgreSQL schema/migration, idempotent consumer, read/mark-as-read API, and OpenAPI document. The direct publish gap remains as a documented/test-only learning demonstration; the runtime confirmation path writes the Order and outbox row atomically and returns without waiting for RabbitMQ. The YARP Gateway now exposes tested `/api/products/*`, `/api/orders/*`, and `/api/notifications/*` routes with path transforms, CORS, trace propagation, request limits, health endpoints, and stable downstream errors; `/internal/*` is rejected. Angular uses same-origin Product/Order/Notification API clients under `/api`, has shared Gateway error handling, and includes Product catalog/operator management, checkout, Order list/detail/cancellation, and Notification screens with bounded polling, manual refresh, and loading/empty/error states. RabbitMQ publish/consume, bounded retry/error queue, duplicate delivery, service restart, and queued recovery are verified with container-backed Notification integration tests. The fake Product client is test-only compatibility coverage. Phase 6.1 provides five multi-stage runtime images with non-root application runtimes, and Phase 6.2 composes Web, Gateway, the three services, three migration one-shots, PostgreSQL, and RabbitMQ with private application ports. A seeded Compose Order reaches `confirmed` and produces a readable Notification through the Web/Gateway path.

Phase 7.2 adds structured outbox backlog logs, configurable readiness thresholds, read-only operator status queries, a recovery runbook, and a RabbitMQ outage/recovery test proving durable confirmation and backlog drain.

Phase 7.3 makes Notification inbox handling explicit and restart-safe: `ConsumedMessage` plus generated Notification commit in one transaction, concurrent duplicate MessageIds are suppressed by database constraints, bounded retry is configurable, and RabbitMQ redelivery across a Notification host restart is covered by integration tests. Inbox/notification cleanup is intentionally deferred because no production retention policy is in scope.

Phase 7.4 adds a controlled Order-native reconciliation path at `/internal/v1/reconciliation/orders/{orderId}`. It queries Product's reservation by Order ID, verifies persisted inventory intent against Product-authoritative snapshots before confirming, safely rejects known absent/released reservations, reconciles `cancellation_pending` without blind repeated release, and records an audit row for each outcome. The path is intentionally excluded from the public Gateway and is for local/manual operations only.

Phase 7.5 adds bounded resilience policies: Product calls retain an explicit timeout of at most five seconds, reserve never retries an ambiguous command, and only the order-keyed idempotent release operation plus the read-only reservation lookup use a configurable safe retry (`PRODUCT_SERVICE_SAFE_RETRY_COUNT`, default 1; delay default 100 ms). All .NET hosts use a bounded shutdown timeout (`MICROSHOP_SHUTDOWN_TIMEOUT_MS`, default 10 seconds); MassTransit startup/consumer stop is tied to that bound, cancellation tokens propagate through dependency calls, and `/health/ready` becomes unhealthy during shutdown while `/health/live` remains a liveness signal.

Phase 8.1–8.6 now covers the observability core plus quality automation: every .NET host uses JSON console scopes with `service.name`, environment, W3C trace/span IDs, and bounded entity identifiers; ASP.NET Core and HttpClient OpenTelemetry tracing/metrics are registered centrally; Order, Product inventory, Product dependency, outbox, and Notification outcomes emit low-cardinality meters; and an optional `OTEL_EXPORTER_OTLP_ENDPOINT` enables OTLP export without requiring a collector for local operation. Playwright runs a real Compose browser flow for catalog management, checkout, confirmation, eventual Notification, cancellation/stock restoration, insufficient stock, and dependency UI behavior. Non-destructive failure-injection wrappers cover stopped services, RabbitMQ/outbox recovery, duplicate/error-queue behavior, and concurrent last-stock protection. The default Compose stack remains limited to the planned services and does not add an observability backend.

## Target architecture

```text
Angular -> YARP Gateway -> Product Service -> Product DB
                       \-> Order Service -> Order DB
                                      \-> HTTP Product Service
                                      \-> Order outbox dispatcher -> RabbitMQ -> Notification Service -> Notification DB
```

Each business service owns its own database. Services do not share EF entities, business logic, connection strings, tables, joins, or cross-service transactions.

## Prerequisites

- .NET SDK `10.0.302` (pinned in `global.json`)
- Node.js `24.15.0` and npm `11.12.1` (pinned in `.nvmrc` and `package.json`)
- Docker Desktop with Docker Compose

The local validation environment used for this bootstrap has .NET SDK 10.0.302, Node 24.15.0, Docker Compose 2.40.3, and Docker 28.5.2.

## Run the bootstrap

From the repository root:

```powershell
Copy-Item .env.example .env

dotnet restore
dotnet format MicroShop.sln --verify-no-changes
dotnet build --configuration Release
dotnet test --configuration Release

Push-Location web/microshop-ui
npm ci
npm run lint
npm run test -- --watch=false
npm run build
Pop-Location

docker compose --env-file .env -f deploy/compose.yaml config
docker compose --env-file .env -f deploy/compose.yaml up --build -d
docker compose --env-file .env -f deploy/compose.yaml ps --all
```

Run the browser and failure-injection gates after the stack is healthy:

```powershell
./scripts/e2e-compose.ps1 -EnvFile .env
./scripts/failure-injection.ps1 -Scenario all -EnvFile .env
```

The wrappers preserve containers and named volumes. Playwright uses Chromium and bounded polling; the failure harness creates uniquely named demo data and only stops/starts named services. `npm run e2e:install` installs the local browser binary when running Playwright directly from `web/microshop-ui`.

The base Compose file starts PostgreSQL, RabbitMQ, three migration one-shots, Product, Order, Notification, Gateway, and Web. PostgreSQL initialization creates separate logical databases and users for Product, Order, and Notification; application services wait for their own migration one-shot and dependency health. Only Web `http://localhost:8080` and RabbitMQ management `http://localhost:15672` are published by default. The Web container proxies `/api/*` to the private Gateway. Existing PostgreSQL/RabbitMQ volumes are retained by normal `docker compose down`.

Leave `OTEL_EXPORTER_OTLP_ENDPOINT` empty for the self-contained local workflow. To connect an existing OTLP collector, set that variable in the uncommitted `.env` file; no collector, trace viewer, credentials, or telemetry volume is added to the baseline Compose stack.

Useful local operations:

```powershell
docker compose --env-file .env -f deploy/compose.yaml logs -f gateway
docker compose --env-file .env -f deploy/compose.yaml ps --all
docker compose --env-file .env -f deploy/compose.yaml run --rm --no-deps product-service dotnet MicroShop.ProductService.dll --seed
docker compose --env-file .env -f deploy/compose.yaml run --rm migrate-product
docker compose --env-file .env -f deploy/compose.yaml down
```

`docker compose up --build` builds or reuses the five pinned application images and runs the three explicit database migrations before application startup. The dedicated `migrate-product`, `migrate-order`, and `migrate-notification` services can be run again for an explicit migration operation; `scripts/db-migrate-all.ps1/.sh` wraps those three services. The permission-gated `scripts/db-reset-local.ps1/.sh` is never needed for normal stop/start and must not be run without owner approval. The RabbitMQ management UI is available at `http://localhost:15672` for local learning.

Build the verified runtime images from the repository root:

```powershell
docker build --file deploy/docker/product-service.Dockerfile --tag microshop-product-service:phase6 .
docker build --file deploy/docker/order-service.Dockerfile --tag microshop-order-service:phase6 .
docker build --file deploy/docker/notification-service.Dockerfile --tag microshop-notification-service:phase6 .
docker build --file deploy/docker/gateway.Dockerfile --tag microshop-gateway:phase6 .
docker build --file deploy/docker/web.Dockerfile --tag microshop-web:phase6 .
```

The .NET images contain only ASP.NET runtime layers and run as UID 1654 on port 8080. The Web image uses an unprivileged Nginx runtime, proxies `/api/*` to the Compose Gateway, and serves the Angular SPA with a `/health` endpoint. These images are wired into the base Compose stack; the native-debug override publishes additional service ports only when explicitly selected.

For the current Product slice, start infrastructure, set the untracked Product database password, apply the migration, and optionally seed demo products:

```powershell
$env:PRODUCT_DB_HOST = "localhost"
$env:PRODUCT_DB_PORT = "5432"
$env:PRODUCT_DB_NAME = "microshop_product"
$env:PRODUCT_DB_USER = "product_app"
$env:PRODUCT_DB_PASSWORD = "<local-password>"

dotnet ef database update --project src/Services/ProductService/MicroShop.ProductService --startup-project src/Services/ProductService/MicroShop.ProductService
./scripts/db-seed-products.ps1
dotnet run --project src/Services/ProductService/MicroShop.ProductService
```

Use `scripts/db-migrate-product.ps1` or `.sh` for the explicit migration command. In non-production environments Product exposes `/api/v1/products` and `/openapi/v1.json`; `/health/ready` checks the owned Product database. The native-debug Compose override publishes PostgreSQL through `${POSTGRES_DEBUG_PORT:-5432}`; choose another local port if `5432` is already in use.

The current Order slice has a separate migration and database owner. Set the untracked Order database password before running its migration wrapper or `dotnet run --project src/Services/OrderService/MicroShop.OrderService -- --migrate`:

```powershell
$env:ORDER_DB_HOST = "localhost"
$env:ORDER_DB_PORT = "5432"
$env:ORDER_DB_NAME = "microshop_order"
$env:ORDER_DB_USER = "order_app"
$env:ORDER_DB_PASSWORD = "<local-password>"

./scripts/db-migrate-order.ps1
```

Order exposes persistence health/readiness, `/openapi/v1.json`, and the native API under `/api/v1/orders` for create, paginated list, and detail. Runtime uses `ProductService:BaseUrl`/`PRODUCT_SERVICE_URL` and an explicit timeout no greater than five seconds to call Product's internal reservation API. `PRODUCT_SERVICE_SAFE_RETRY_COUNT` and `PRODUCT_SERVICE_SAFE_RETRY_DELAY_MS` apply only to idempotent release and read-only lookup calls; reserve remains single-attempt because a lost response can mean that Product already committed stock. Product returns authoritative snapshots and Order persists `confirmed`, known `rejected`, or infrastructure `inventory_unknown` outcomes. Confirming an Order inserts a durable `OrderConfirmedV1` outbox record in the same database save as the confirmed state; a bounded dispatcher claims rows with a PostgreSQL lease, publishes with the stable outbox message ID, retries with backoff, and dead-letters after the configured maximum attempts. Configure `ORDER_OUTBOX_ENABLED`, `ORDER_OUTBOX_MAX_ATTEMPTS`, `ORDER_OUTBOX_POLL_INTERVAL_MS`, `ORDER_OUTBOX_LEASE_DURATION_MS`, `ORDER_OUTBOX_RETRY_BASE_DELAY_MS`, `ORDER_OUTBOX_RETRY_MAX_DELAY_MS`, `ORDER_OUTBOX_MAX_PENDING_MESSAGES`, `ORDER_OUTBOX_MAX_PENDING_AGE_MS`, `ORDER_OUTBOX_BACKLOG_LOG_INTERVAL_MS`, and `ORDER_OUTBOX_FAIL_READINESS_ON_DEAD_LETTERED` for local operation. Use `./scripts/db-outbox-status.ps1 -EnvFile .env` (or `.sh` on POSIX) to inspect pending/dead-lettered rows. During a RabbitMQ outage, keep Order running, confirm the Order DB row and pending outbox row, restore RabbitMQ, and restart Order if the broker client does not reconnect; repeat the read-only status query until pending count is zero. Set `ProductService:UseFakeClient=true` only for the legacy deterministic Phase 2 test fixture; the default runtime path is the typed HTTP client.

Notification has a separate database owner and applies its own migration explicitly. Set the untracked Notification database password before running its migration command:

```powershell
$env:NOTIFICATION_DB_HOST = "localhost"
$env:NOTIFICATION_DB_PORT = "5432"
$env:NOTIFICATION_DB_NAME = "microshop_notification"
$env:NOTIFICATION_DB_USER = "notification_app"
$env:NOTIFICATION_DB_PASSWORD = "<local-password>"

dotnet run --project src/Services/NotificationService/MicroShop.NotificationService -- --migrate
```

The Notification runtime consumes `OrderConfirmedV1` through its durable MassTransit endpoint, stores `ConsumedMessage` and the generated Notification in one owned PostgreSQL transaction, suppresses duplicate message IDs, and exposes `/api/v1/notifications` for filtered/paginated reads plus optional mark-as-read. The Angular Notification screen now uses the Gateway API with bounded polling, manual refresh, and explicit loading/empty/error states. The Compose HTTP smoke flow and Playwright suite confirm Order/Notification delivery; broker recovery behavior is covered by both the RabbitMQ Testcontainers suite and the non-destructive failure-injection harness.

Notification consumer retry is bounded and configurable with `NOTIFICATION_CONSUMER_RETRY_COUNT` and `NOTIFICATION_CONSUMER_RETRY_DELAY_MS` (defaults: 3 and 250 ms). Concurrent duplicate delivery is protected by the `consumed_messages` primary key and unique `notifications.source_message_id`; the test suite also verifies transaction rollback and idempotency across a Notification host restart.

For the current Gateway slice, run the native Gateway after Product, Order, and Notification and use the public routes below. Destination addresses can be overridden with `PRODUCT_SERVICE_URL`, `ORDER_SERVICE_URL`, and `NOTIFICATION_SERVICE_URL`; Angular uses the same-origin Gateway paths and should not call service-native ports as a browser fallback.

```powershell
dotnet run --project src/Gateway/MicroShop.Gateway
```

Gateway routes:

- `GET|POST|PATCH /api/products/*` -> Product `/api/v1/products/*`
- `GET|POST /api/orders/*` -> Order `/api/v1/orders/*`
- `GET|POST /api/notifications/*` -> Notification `/api/v1/notifications/*`
- `/internal/*` -> `404 GATEWAY_ROUTE_NOT_FOUND`
- unavailable downstream -> `502 DOWNSTREAM_UNAVAILABLE`

For native application debugging, use the non-default override to publish PostgreSQL and RabbitMQ application ports:

```powershell
docker compose --env-file .env -f deploy/compose.yaml -f deploy/compose.override.yaml up -d
```

Do not commit `.env`. The committed `.env.example` contains placeholders only. Do not use volume-removal/reset commands without explicit permission.

## Repository layout

```text
MicroShop.sln
src/
  Gateway/MicroShop.Gateway/
  BuildingBlocks/MicroShop.Contracts/
  BuildingBlocks/MicroShop.ServiceDefaults/
  Services/ProductService/MicroShop.ProductService/
  Services/OrderService/MicroShop.OrderService/
  Services/NotificationService/MicroShop.NotificationService/
tests/MicroShop.Architecture.Tests/
tests/MicroShop.ProductService.Tests/
web/microshop-ui/
deploy/
  docker/
scripts/
docs/
.github/workflows/ci.yml
```

## Canonical documentation

- [Agent instructions](docs/agent/AGENT.md)
- [Implementation task checklist](docs/agent/task.md)
- [Verified completion snapshot](docs/PROJECT_COMPLETION_CHECKLIST.md)
- [Project context](docs/project/00_PROJECT_CONTEXT.md)
- [Product requirements](docs/project/01_PRODUCT_REQUIREMENTS.md)
- [System architecture](docs/project/02_SYSTEM_ARCHITECTURE.md)
- [Domain and flows](docs/project/03_DOMAIN_AND_FLOWS.md)
- [Database rules](docs/project/04_DATABASE.md)
- [API contracts](docs/project/05_API.md)
- [Codebase guide](docs/project/06_CODEBASE_GUIDE.md)
- [Development and testing](docs/project/07_DEVELOPMENT_AND_TESTING.md)
- [Deployment and operations](docs/project/08_DEPLOYMENT_AND_OPERATIONS.md)
- [Roadmap and ADRs](docs/project/09_ROADMAP_AND_DECISIONS.md)

The roadmap order is Phase 0 repository bootstrap, Phase 1 Product Service, Phase 2 Order foundation, Phase 3 synchronous inventory communication, Phase 4 Gateway, Phase 5 messaging/Notification, Phase 6 full Compose, Phase 7 reliability, and Phase 8 observability/quality. Authentication remains optional Phase 9.

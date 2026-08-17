# MicroShop — Mini Order System

MicroShop is a deliberately small learning project for Angular, ASP.NET Core, YARP, PostgreSQL, RabbitMQ, synchronous HTTP, asynchronous events, database-per-service ownership, idempotency, and distributed failure handling. It is not a production e-commerce platform.

## Current status

Phase 0 bootstrap is implemented. Product has its own PostgreSQL model/migrations, deterministic development seed, service-native catalog/detail/create/update API, activate/deactivate lifecycle, optimistic version checks, Product-owned atomic inventory reservation/release/query API, readiness/OpenAPI, and PostgreSQL Testcontainers tests. Order has its own database and state history, a native HTTP API, a typed Product inventory client, authoritative reservation orchestration, timeout/availability mapping, `inventory_unknown`, guarded cancellation with `cancellation_pending`, and the direct `OrderConfirmedV1` publish milestone after the confirmed DB commit. The versioned `MicroShop.Contracts.Orders.OrderConfirmedV1` wire contract is implemented and JSON-tested, MassTransit RabbitMQ transport/options, bounded retry, and a durable Notification endpoint are configured, and Notification now owns its PostgreSQL schema/migration, idempotent consumer, read/mark-as-read API, and OpenAPI document. The direct publish gap is intentionally deferred to the Phase 7 outbox. The YARP Gateway now exposes tested `/api/products/*`, `/api/orders/*`, and `/api/notifications/*` routes with path transforms, CORS, trace propagation, request limits, health endpoints, and stable downstream errors; `/internal/*` is rejected. Angular uses same-origin Product/Order/Notification API clients under `/api`, has shared Gateway error handling, and includes Product catalog/operator management, checkout, Order list/detail/cancellation, and Notification screens with bounded polling, manual refresh, and loading/empty/error states. RabbitMQ publish/consume, bounded retry/error queue, duplicate delivery, service restart, and queued recovery are verified with container-backed Notification integration tests. The fake Product client is test-only compatibility coverage. Phase 6.1 provides five multi-stage runtime images with non-root application runtimes, and Phase 6.2 composes Web, Gateway, the three services, three migration one-shots, PostgreSQL, and RabbitMQ with private application ports. A seeded Compose Order reaches `confirmed` and produces a readable Notification through the Web/Gateway path.

## Target architecture

```text
Angular -> YARP Gateway -> Product Service -> Product DB
                       \-> Order Service -> Order DB
                                      \-> HTTP Product Service
                                      \-> RabbitMQ -> Notification Service -> Notification DB
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

The base Compose file starts PostgreSQL, RabbitMQ, three migration one-shots, Product, Order, Notification, Gateway, and Web. PostgreSQL initialization creates separate logical databases and users for Product, Order, and Notification; application services wait for their own migration one-shot and dependency health. Only Web `http://localhost:8080` and RabbitMQ management `http://localhost:15672` are published by default. The Web container proxies `/api/*` to the private Gateway. Existing PostgreSQL/RabbitMQ volumes are retained by normal `docker compose down`.

Useful local operations:

```powershell
docker compose --env-file .env -f deploy/compose.yaml logs -f gateway
docker compose --env-file .env -f deploy/compose.yaml ps --all
docker compose --env-file .env -f deploy/compose.yaml run --rm --no-deps product-service dotnet MicroShop.ProductService.dll --seed
docker compose --env-file .env -f deploy/compose.yaml run --rm migrate-product
docker compose --env-file .env -f deploy/compose.yaml down
```

`docker compose up --build` builds or reuses the five pinned application images and runs the three explicit database migrations before application startup. The dedicated `migrate-product`, `migrate-order`, and `migrate-notification` services can be run again for an explicit migration operation; `migrate-all` and a permission-gated reset wrapper are tracked in the next Phase 6.3 slice. The RabbitMQ management UI is available at `http://localhost:15672` for local learning.

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

Order exposes persistence health/readiness, `/openapi/v1.json`, and the native API under `/api/v1/orders` for create, paginated list, and detail. Runtime uses `ProductService:BaseUrl`/`PRODUCT_SERVICE_URL` and an explicit timeout no greater than five seconds to call Product's internal reservation API. Product returns authoritative snapshots and Order persists `confirmed`, known `rejected`, or infrastructure `inventory_unknown` outcomes. Set `ProductService:UseFakeClient=true` only for the legacy deterministic Phase 2 test fixture; the default runtime path is the typed HTTP client.

Notification has a separate database owner and applies its own migration explicitly. Set the untracked Notification database password before running its migration command:

```powershell
$env:NOTIFICATION_DB_HOST = "localhost"
$env:NOTIFICATION_DB_PORT = "5432"
$env:NOTIFICATION_DB_NAME = "microshop_notification"
$env:NOTIFICATION_DB_USER = "notification_app"
$env:NOTIFICATION_DB_PASSWORD = "<local-password>"

dotnet run --project src/Services/NotificationService/MicroShop.NotificationService -- --migrate
```

The Notification runtime consumes `OrderConfirmedV1` through its durable MassTransit endpoint, stores `ConsumedMessage` and the generated Notification in one owned PostgreSQL transaction, suppresses duplicate message IDs, and exposes `/api/v1/notifications` for filtered/paginated reads plus optional mark-as-read. The Angular Notification screen now uses the Gateway API with bounded polling, manual refresh, and explicit loading/empty/error states. The Compose HTTP smoke flow confirms a seeded Order and eventual Notification delivery; browser Playwright coverage remains a later roadmap task, and broker recovery behavior is covered in the RabbitMQ Testcontainers suite.

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

# Development and Testing

Last reviewed: 2026-08-02.

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
docker compose -f deploy/compose.yaml up --build
```

Expected public endpoints:

| Component | URL |
| --- | --- |
| Web | `http://localhost:8080` |
| Gateway direct | `http://localhost:8088` if published for debugging |
| RabbitMQ management | `http://localhost:15672` in development only |

Service-native ports may be published only in `compose.override.yaml` for debugging.

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
./scripts/db-migrate-all.sh
./scripts/db-seed-products.sh
./scripts/db-reset-local.sh
```

PowerShell equivalents should exist for a Windows-first learning environment.

Direct EF example:

```bash
dotnet ef database update \
  --project src/Services/ProductService/MicroShop.ProductService \
  --startup-project src/Services/ProductService/MicroShop.ProductService
```

For the current Product slice, set `PRODUCT_DB_HOST`, `PRODUCT_DB_PORT`, `PRODUCT_DB_NAME`, `PRODUCT_DB_USER`, and the untracked `PRODUCT_DB_PASSWORD` environment variable before using `scripts/db-migrate-product.*` or `scripts/db-seed-products.*`. Install the pinned CLI with `dotnet tool install --tool-path <local-tool-dir> dotnet-ef --version 10.0.10` when `dotnet ef` is not already available.

`db-reset-local` is destructive and must refuse production-like hosts unless explicitly overridden.

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

The current infrastructure and Product migration smoke checks are:

```bash
docker compose --env-file .env.example -f deploy/compose.yaml config
docker compose --env-file .env.example -f deploy/compose.yaml up -d
docker compose --env-file .env.example -f deploy/compose.yaml ps

dotnet ef database update \
  --project src/Services/ProductService/MicroShop.ProductService \
  --startup-project src/Services/ProductService/MicroShop.ProductService
```

Application images, the full application Compose stack, and `compose.test.yaml` remain deferred until the owning roadmap phases. CI applies the Product migration to an empty PostgreSQL service database.

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

- create/list product (implemented); update product (planned);
- reject negative stock/price;
- inactive Product visibility and database constraints;
- reserve one item;
- reserve multiple items atomically;
- insufficient stock rolls back all;
- duplicate same reservation returns existing result;
- duplicate mismatched reservation conflicts;
- release restores once;
- concurrent last-stock request permits one success.

The implemented Product API tests use PostgreSQL Testcontainers and apply the real `InitialProductSchema` migration. EF Core InMemory is not used.

### Order integration tests

Use real Order PostgreSQL and either Product Service test host/container or an explicit HTTP stub for isolated orchestration cases.

Cases:

- pending -> confirmed;
- known Product conflict -> rejected;
- timeout -> inventory_unknown;
- total from Product snapshot;
- confirmation event/outbox row;
- cancellation state guards;
- release unknown -> cancellation_pending.

### Notification integration tests

Use RabbitMQ and PostgreSQL Testcontainers or Compose.

Cases:

- consume event creates notification;
- duplicate event creates one notification;
- transient failure retries;
- poison message enters error queue;
- consumer restart processes queued message.

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

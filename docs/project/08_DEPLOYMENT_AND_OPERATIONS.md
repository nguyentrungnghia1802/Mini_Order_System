# Deployment and Operations

Last reviewed: 2026-08-18.

## 1. Environment model

The repository now provides a full local Compose stack: Web, Gateway, Product, Order, Notification, PostgreSQL, RabbitMQ, and three explicit migration one-shots. Product includes a Product-owned internal reservation/release API; Order includes a native create/list/detail/cancel API backed at runtime by a typed Product reservation client with explicit timeout, bounded idempotent release/lookup retry, `inventory_unknown`, and `cancellation_pending` handling plus an Order-owned transactional outbox/dispatcher; Notification consumes and reads generated notifications from its own database; Gateway exposes tested Product/Order/Notification public routes and rejects `/internal/*`. The Angular application includes the Notification screen and same-origin Gateway integration. Phase 6.1 supplies buildable non-root application images, Phase 6.2 verifies the Compose order-to-notification smoke flow, and Phase 7.1-7.5 verify durable outbox persistence, lease/retry behavior, backlog operations, readiness policy, RabbitMQ outage recovery, Notification inbox idempotency, reconciliation, bounded shutdown, and lifecycle readiness transitions. Browser Playwright coverage and the Phase 8 observability/quality work remain deferred.

| Environment | Purpose | Data/integration policy |
| --- | --- | --- |
| Local native | Fast debugging | Apps native, PostgreSQL/RabbitMQ in Docker |
| Local Compose | Main learning/demo environment | Disposable or named local volumes |
| Test/CI | Automated verification | Isolated containers and synthetic data |
| Demo/VPS | Optional remote demonstration | HTTPS, generated secrets, no real customer data |
| Production | Not a baseline target | Requires authentication, privacy, HA, monitoring, legal review |

The system must not be described as production-ready merely because it runs in Docker.

## Phase 7.1 outbox status

Order confirmation writes the `orders` state and its `outbox_messages` event in one database save. `OutboxDispatcher` claims pending rows with PostgreSQL leases, publishes `OrderConfirmedV1` with the stable outbox MessageId and trace context, retries with bounded backoff, reclaims expired leases after restart, and marks exhausted messages dead-lettered. The backlog/readiness policy and RabbitMQ outage exercise are implemented in Phase 7.2; Notification inbox idempotency is implemented in Phase 7.3; reconciliation is implemented in Phase 7.4; shutdown/resilience policy and lifecycle readiness are implemented in Phase 7.5.

## Phase 7.2 outbox operations

`OutboxDispatcher` emits a structured backlog log at the configured interval with pending count, oldest pending age, and dead-letter count. Order `/health/ready` includes an outbox health check when outbox mode is enabled:

- pending count above `ORDER_OUTBOX_MAX_PENDING_MESSAGES` is unhealthy;
- oldest pending age above `ORDER_OUTBOX_MAX_PENDING_AGE_MS` is unhealthy;
- dead-lettered rows are degraded by default and unhealthy when `ORDER_OUTBOX_FAIL_READINESS_ON_DEAD_LETTERED=true`;
- a pending backlog within policy does not fail readiness merely because RabbitMQ is unavailable—the Order database is the durability boundary for confirmation.

The baseline has no metrics provider or metrics endpoint, so Phase 7.2 uses health data and structured logs. The metrics surface remains owned by Phase 8.3.

### Outbox operator query

Use the read-only wrapper from the repository root:

```powershell
./scripts/db-outbox-status.ps1 -EnvFile .env
```

```bash
./scripts/db-outbox-status.sh .env
```

The command prints a summary and at most 20 pending/dead-lettered rows. It does not update, delete, or replay messages. The equivalent SQL is kept in both scripts so an operator can review the exact query before running it.

### RabbitMQ outage recovery

1. Keep Order and PostgreSQL running; do not delete the Order database or Compose volumes.
2. During the outage, verify `/health/ready`, the confirmed Order row, and a pending row with `db-outbox-status`.
3. Restore RabbitMQ and wait for its health check. If the MassTransit connection does not reconnect, restart only `order-service` so a new dispatcher can reclaim expired leases.
4. Re-run the read-only status query until pending count is zero and inspect any dead-lettered rows before taking further action.
5. Verify the Notification consumer and read API for the recovered Order; retain dead-letter/error details for diagnosis rather than deleting them.

The automated evidence is `OrderOutboxRabbitMqTests.RabbitMqOutageLeavesConfirmedOrderDurableAndRecoveryDrainsOutbox`, which uses disposable PostgreSQL/RabbitMQ Testcontainers and proves the same sequence without touching the local Compose volumes.

## Phase 7.3 Notification inbox operations

Notification handles one broker delivery by inserting the durable `ConsumedMessage` inbox row and generated Notification in one PostgreSQL transaction. A duplicate MessageId returns without a new side effect; a concurrent unique-key loser rolls back and is acknowledged as an idempotent duplicate. The consumer retry count and delay are bounded and configurable. The baseline retains inbox and notification history for the life of the demo database; no cleanup worker is enabled because a retention policy for customer data is a deployment decision outside this learning slice.

## 2. Configuration model

Each process receives only required configuration.

### Gateway

- downstream cluster addresses;
- allowed public origins;
- proxy timeout and limits;
- logging/telemetry settings.

The current Gateway configuration reads `PRODUCT_SERVICE_URL`, `ORDER_SERVICE_URL`, and `NOTIFICATION_SERVICE_URL` first, validates all three as absolute HTTP(S) destinations, maps `/api/products/*`, `/api/orders/*`, and `/api/notifications/*` to versioned native paths, preserves the incoming W3C trace ID, and returns stable `502 DOWNSTREAM_UNAVAILABLE` responses for unavailable destinations.

### Product Service

- Product DB settings: `PRODUCT_DB_HOST`, `PRODUCT_DB_PORT`, `PRODUCT_DB_NAME`, `PRODUCT_DB_USER`, and local-only `PRODUCT_DB_PASSWORD` (or an untracked connection string override);
- service name/environment;
- health/telemetry settings.

### Order Service

- Order DB connection (`ORDER_DB_HOST`, `ORDER_DB_PORT`, `ORDER_DB_NAME`, `ORDER_DB_USER`, and local-only `ORDER_DB_PASSWORD`, or an untracked connection string);
- Product Service internal URL;
- HTTP timeout/resilience settings;
- RabbitMQ connection;
- outbox settings: `ORDER_OUTBOX_ENABLED`, `ORDER_OUTBOX_MAX_ATTEMPTS`, `ORDER_OUTBOX_POLL_INTERVAL_MS`, `ORDER_OUTBOX_LEASE_DURATION_MS`, `ORDER_OUTBOX_RETRY_BASE_DELAY_MS`, and `ORDER_OUTBOX_RETRY_MAX_DELAY_MS`.

### Notification Service

- Notification DB connection;
- RabbitMQ connection;
- bounded consumer retry/concurrency (`NOTIFICATION_CONSUMER_RETRY_COUNT`, `NOTIFICATION_CONSUMER_RETRY_DELAY_MS`);
- HTTP read API settings.

### Shared process resilience

- `MICROSHOP_SHUTDOWN_TIMEOUT_MS` bounds host shutdown between 1 and 60 seconds (default 10 seconds);
- `PRODUCT_SERVICE_TIMEOUT_MS` remains bounded to at most 5 seconds;
- `PRODUCT_SERVICE_SAFE_RETRY_COUNT` and `PRODUCT_SERVICE_SAFE_RETRY_DELAY_MS` control only the Order client's idempotent release and read-only reservation lookup retries (defaults: 1 and 100 ms);
- Product reservation creation is not automatically retried because a lost response can represent a committed stock change.

### Web

Only browser-safe configuration:

- public API base, preferably empty/same origin;
- application name;
- optional polling interval.

No database or RabbitMQ secret may use an Angular build variable.

## 3. Secrets

Secrets include:

- database passwords;
- RabbitMQ password;
- TLS private keys;
- optional telemetry backend credentials.

Rules:

- `.env.example` contains placeholders only;
- `.env` is untracked;
- CI uses secret storage;
- public demo uses generated credentials;
- logs and health details do not expose secrets;
- rotate any secret shown in screenshots or committed history.

## 4. Docker images

Expected images:

| Image | Build |
| --- | --- |
| `microshop-web` | Angular build stage -> nginx runtime |
| `microshop-gateway` | .NET publish -> ASP.NET runtime |
| `microshop-product-service` | .NET publish -> ASP.NET runtime |
| `microshop-order-service` | .NET publish -> ASP.NET runtime |
| `microshop-notification-service` | .NET publish -> ASP.NET runtime |

Image rules:

- multi-stage builds;
- pinned base image major;
- non-root user when supported;
- no SDK in runtime image;
- deterministic restore from lock/central versions;
- health checks at Compose/orchestrator level;
- labels/version metadata optional.

The current implementation maps these images to `deploy/docker/product-service.Dockerfile`, `deploy/docker/order-service.Dockerfile`, `deploy/docker/notification-service.Dockerfile`, `deploy/docker/gateway.Dockerfile`, and `deploy/docker/web.Dockerfile`. The .NET images use SDK `10.0.302` only in the build stage, ASP.NET `10.0` in the runtime stage, UID 1654, and port 8080. The Web image uses Node `24.15.0` only in the build stage and unprivileged Nginx on port 8080. Local Phase 6.1 builds and the Web `/health` smoke check pass; Compose service wiring is 6.2.

## 5. Compose topology

Implemented services:

```yaml
services:
  web:
  gateway:
  product-service:
  order-service:
  notification-service:
  migrate-product:
  migrate-order:
  migrate-notification:
  postgres:
  rabbitmq:
```

Volumes:

- PostgreSQL data;
- RabbitMQ data;
- optional development NuGet/npm caches are not production volumes.

Networks:

- one internal application network is enough for baseline;
- only Web 8080 and RabbitMQ management 15672 are published by the base file;
- Gateway and service-native ports are published only by `deploy/compose.override.yaml` for debugging;
- database ports are optional debug overrides, not default public exposure.

## 6. Startup dependencies

Compose `depends_on` controls startup order, not business readiness.

The Compose implementation satisfies the following startup rules:

- retry initial dependency connection with bounded startup policy where appropriate;
- expose readiness;
- fail clearly if configuration is invalid;
- tolerate Notification Service starting before/after queue topology;
- not assume database schema exists without explicit migration step.

`migrate-product`, `migrate-order`, and `migrate-notification` wait for PostgreSQL health and exit successfully before their owning application service starts. Product now supports `--migrate`; Order and Notification retain their explicit `--migrate` commands. Product, Order, Notification, and Gateway expose Compose health checks; Web exposes `/health`; Gateway is started only after all three downstream services are healthy.

Recommended startup sequence:

1. PostgreSQL and RabbitMQ;
2. migrations for each database;
3. Product and Notification services;
4. Order Service;
5. Gateway;
6. Web.

## 7. Migration deployment

Do not run competing migrations from every replica.

For demo/VPS:

1. stop or drain old application if migration is incompatible;
2. back up databases;
3. run Product migration command;
4. run Order migration command;
5. run Notification migration command;
6. verify migration status;
7. start new services;
8. verify readiness and smoke tests.

Prefer backward-compatible expand/contract migrations for any future rolling deployment.

For the current Product slice, apply `InitialProductSchema`, `AddInventoryReservations`, and `AddInventoryReservationConstraints` with `scripts/db-migrate-product.ps1` or `.sh`, then run the explicit seed command if demo data is needed. Apply `InitialOrderSchema` with `scripts/db-migrate-order.ps1` or `.sh`, and `InitialNotificationSchema` with `scripts/db-migrate-notification.ps1` or `.sh`. `scripts/db-migrate-all.ps1/.sh` runs the three Compose migration one-shots in order. In Compose, the same one-shots run before application startup. Normal Product, Order, and Notification startup validates database configuration and readiness but does not silently apply migrations. The local reset wrappers require explicit owner-approved volume-deletion flags and typed confirmation.

## 8. Public deployment path

Recommended simple VPS:

```text
Internet
  |
  v
Host nginx/Caddy (TLS)
  |
  +--> web static origin
  \--> /api -> gateway container
```

Alternatively the web container can reverse proxy `/api` to Gateway. Use one clear TLS termination boundary and preserve forwarding headers.

Internal services, PostgreSQL, and RabbitMQ must not be exposed publicly.

## 9. Deployment sequence

1. Pull source or immutable images.
2. Verify `.env`/secret inputs.
3. Back up current databases.
4. Build/pull images.
5. Start PostgreSQL and RabbitMQ.
6. Apply the three explicit migrations.
7. Start Product, Order, Notification, Gateway, and Web.
8. Verify service readiness.
9. Verify Gateway routes through Web.
10. Run a smoke order.
11. Verify broker event and notification.
12. Monitor logs/error queue.

## 10. Health and readiness

### Liveness

Checks process execution only.

- `/health/live` returns 200 if process host is alive;
- no dependency check.

### Readiness

| Process | Required readiness |
| --- | --- |
| Gateway | route config loaded; optionally downstream health summary |
| Product Service | Product DB reachable and expected schema present |
| Order Service | Order DB and RabbitMQ reachable; Product dependency policy documented |
| Notification Service | Notification DB and RabbitMQ reachable; consumer started |
| Web | static server responds |

Whether Order readiness fails when Product Service is down is a deliberate decision. Baseline may report not-ready because order creation cannot complete, while keeping liveness healthy.

Product readiness is implemented with an EF Core database health check. A missing Product database password/connection configuration fails startup validation rather than silently selecting another service database.

### Shutdown behavior

`MICROSHOP_SHUTDOWN_TIMEOUT_MS` is parsed and bounded to 1–60 seconds, with a 10-second default. The value configures the generic host shutdown timeout and, for Order/Notification, MassTransit startup and consumer-stop timeouts. Notification keeps its durable endpoint/prefetch policy and bounded retry; shutdown cancellation is passed through the host and consumer pipeline so the process stops without an unbounded drain.

The shared lifecycle health check observes `IHostApplicationLifetime.ApplicationStopping`: `/health/ready` returns unhealthy during shutdown, while `/health/live` remains a process-liveness signal. This prevents a terminating instance from receiving new traffic without making liveness imply dependency health. The bounded timeout and transition are covered by `ServiceReadinessTests`; Product-client tests cover caller cancellation and the single-attempt reserve policy.

No circuit breaker is enabled in this learning baseline. The Product dependency already has an explicit five-second cap, bounded retry only for idempotent release/lookup operations, explicit ambiguous outcomes, durable outbox/reconciliation paths, and readiness signals. Adding a circuit breaker would add state and tuning without a demonstrated failure mode; it can be revisited with measured traffic.

## 11. Observability

Minimum:

- structured stdout logs;
- service name;
- environment;
- trace/span IDs;
- important entity/message IDs;
- health endpoints;
- RabbitMQ management view in local/demo;
- database migration status.

Hardening extension:

- OpenTelemetry traces;
- metrics for request duration/error;
- dependency duration/error;
- order outcomes;
- outbox pending count;
- RabbitMQ queue depth;
- consumer retry/error count.

## 12. Suggested metrics

| Metric | Meaning |
| --- | --- |
| `http_server_request_duration` | Service API latency |
| `http_client_product_duration` | Order -> Product latency |
| `orders_created_total{status}` | Confirmed/rejected/unknown outcomes |
| `inventory_reservations_total{result}` | Reservation results |
| `inventory_available_stock` | Optional product gauge, careful cardinality |
| `outbox_pending` | Unpublished events |
| `notifications_consumed_total{result}` | New/duplicate/failed |
| `consumer_error_queue_total` | Poison-message operational count |

Metrics are optional for baseline but names/labels should avoid unbounded customer/order IDs.

## 13. Logging operations

Logs are written to stdout/stderr; Docker or the hosting platform collects them.

Do not store critical state only in log files.

Operational searches:

- by trace ID;
- by order ID;
- by reservation ID;
- by message ID;
- by service name;
- by error code.

When integrated with the existing Log Monitoring System, use the same field names across all .NET services.

## 14. RabbitMQ operations

Monitor:

- connection/channel count;
- queue ready/unacked;
- consumer count;
- redelivery;
- error queue;
- disk/memory alarms.

### Queue backlog

1. confirm Notification Service readiness;
2. inspect consumer count;
3. inspect unacked messages;
4. check Notification DB;
5. inspect consumer exceptions;
6. restart only after understanding repeated poison behavior;
7. do not purge a queue merely to hide backlog.

### Error queue

For a learning replay:

1. inspect payload/headers and exception;
2. fix consumer/data issue;
3. replay with the same message ID;
4. verify idempotency;
5. archive/delete error message only after evidence is recorded.

## 15. Backup

### PostgreSQL

```bash
pg_dump -Fc -d microshop_product > product.dump
pg_dump -Fc -d microshop_order > order.dump
pg_dump -Fc -d microshop_notification > notification.dump
```

Back up before migration and before destructive reset.

### RabbitMQ

For the learning baseline:

- export definitions for topology/users if customized;
- persistent messages depend on RabbitMQ volume durability;
- do not treat broker backup as the primary event source.

With an Order outbox, unpublished/republishable events remain in Order DB, improving recovery.

## 16. Restore

1. stop application writes;
2. restore databases to matching or compatible schema;
3. restore RabbitMQ definitions/volume if required;
4. apply necessary migrations;
5. start services in dependency order;
6. verify reservation/order consistency;
7. verify outbox dispatch;
8. verify consumer idempotency;
9. run smoke test.

Restoring databases from different times can create cross-service inconsistency. For a learning demo, back up all service databases in the same maintenance window.

## 17. Rollback

Application rollback is safe only when old code understands the current schema/event contracts.

Before deploy:

- preserve previous images;
- review migration backward compatibility;
- avoid removing event fields consumed by old service;
- do not roll back one service to an incompatible internal API.

If database migration is destructive, restore from backup rather than improvising reverse SQL.

## 18. Incident runbooks

### Browser receives 502

- check Gateway health;
- inspect YARP cluster destination;
- check target readiness;
- verify Docker DNS/network;
- inspect path transform.

### Orders return Product unavailable

- check Product Service liveness/readiness;
- check Product DB;
- check Order typed-client DNS/address;
- inspect Product timeout and bounded retry configuration (`PRODUCT_SERVICE_TIMEOUT_MS`, `PRODUCT_SERVICE_SAFE_RETRY_COUNT`, `PRODUCT_SERVICE_SAFE_RETRY_DELAY_MS`);
- use trace ID;
- inspect whether an inventory reservation exists for affected order.

### Order is `inventory_unknown`

- do not manually mark confirmed immediately;
- keep the Order Service and Product Service available and query Product through `GET /internal/v1/inventory/reservations/by-order/{orderId}`;
- call the Order-native `POST /internal/v1/reconciliation/orders/{orderId}` manual path;
- if an identical `reserved` response exists, the handler verifies the stored Product ID/quantity intent, Product snapshots, currency, subtotals, total, and order limit before confirming;
- if Product returns `404 RESERVATION_NOT_FOUND` or a known `released` reservation, the handler rejects the Order safely without adding an outbox event;
- if Product is unavailable, the response is `503` and the Order remains `inventory_unknown`; retry the same controlled reconciliation later;
- if the response is malformed or mismatched, stop and investigate the audit row rather than guessing;
- inspect `order_reconciliation_audits` by `order_id` and retain the trace ID and reservation ID in the incident note.

### Order is `cancellation_pending`

- do not send repeated blind release commands from a shell;
- call `POST /internal/v1/reconciliation/orders/{orderId}`;
- an absent or already `released` Product reservation is safely recorded and moves the Order to `cancelled`;
- a `reserved` response causes one idempotent Product release operation, with only its bounded transport retry; only a known successful release moves the Order to `cancelled`;
- dependency/timeout/invalid outcomes remain `cancellation_pending` and produce an audit row for the next controlled attempt;
- the reconciliation route is not routed by the public Gateway and has no browser UI in the baseline.

### Confirmed order has no notification

- check outbox row/direct publish log;
- check RabbitMQ queue;
- check Notification consumer;
- check duplicate table;
- check error queue;
- replay only with same message ID where appropriate.

### Negative stock

Critical correctness incident:

1. stop product/order writes;
2. preserve DB/log evidence;
3. inspect direct stock adjustments and reservations;
4. reconcile stock from known baseline/movements;
5. fix locking/constraint;
6. add concurrency regression test;
7. resume only after validation.

### Duplicate notifications

- inspect same/different message IDs;
- verify unique source message constraint;
- verify consumed-message transaction;
- distinguish true duplicate publish from redelivery;
- fix producer or consumer accordingly.

### RabbitMQ unavailable

Direct mode:

- inspect whether orders committed without messages;
- reconcile/publish manually from order data only through a controlled script.

Outbox mode:

- orders can remain confirmed;
- monitor pending outbox count;
- restore RabbitMQ;
- verify dispatcher drains backlog.

## 19. CI/CD

Suggested pipeline:

1. checkout;
2. setup .NET/Node;
3. restore dependencies;
4. format/lint;
5. build .NET;
6. test unit;
7. build/test Angular;
8. start PostgreSQL/RabbitMQ test infrastructure;
9. run integration/contract tests;
10. build container images;
11. run Compose smoke/E2E;
12. scan dependencies/images;
13. publish artifacts/images on main/tag;
14. deploy optional demo with approval.

Database migrations should be validated in CI against an empty database.

## 20. Production-readiness gaps

Before accepting real users:

- authentication and authorization;
- CSRF/session strategy if cookies are used;
- privacy/retention policy;
- secure product administration;
- rate limiting/WAF;
- managed database/broker or tested operations;
- backups and restore drill;
- TLS and secret manager;
- alerting/SLOs;
- outbox and reconciliation;
- external notification provider policy;
- load/security testing;
- audit log.

The baseline intentionally leaves these gaps visible.

## 21. Demo deployment checklist

- [ ] Generated secrets.
- [ ] No real personal data.
- [ ] HTTPS enabled.
- [ ] Database/RabbitMQ ports not public.
- [ ] Migrations applied.
- [ ] Health endpoints pass.
- [ ] Product seed loaded.
- [ ] Successful order smoke test.
- [ ] Insufficient-stock test.
- [ ] Notification queue/consumer verified.
- [ ] Backups configured if demo data matters.
- [ ] UI clearly labeled as demonstration.

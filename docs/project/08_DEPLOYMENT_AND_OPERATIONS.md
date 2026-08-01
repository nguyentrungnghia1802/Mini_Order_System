# Deployment and Operations

Last reviewed: 2026-08-02.

## 1. Environment model

| Environment | Purpose | Data/integration policy |
| --- | --- | --- |
| Local native | Fast debugging | Apps native, PostgreSQL/RabbitMQ in Docker |
| Local Compose | Main learning/demo environment | Disposable or named local volumes |
| Test/CI | Automated verification | Isolated containers and synthetic data |
| Demo/VPS | Optional remote demonstration | HTTPS, generated secrets, no real customer data |
| Production | Not a baseline target | Requires authentication, privacy, HA, monitoring, legal review |

The system must not be described as production-ready merely because it runs in Docker.

## 2. Configuration model

Each process receives only required configuration.

### Gateway

- downstream cluster addresses;
- allowed public origins;
- proxy timeout and limits;
- logging/telemetry settings.

### Product Service

- Product DB connection;
- service name/environment;
- health/telemetry settings.

### Order Service

- Order DB connection;
- Product Service internal URL;
- HTTP timeout/resilience settings;
- RabbitMQ connection;
- outbox settings when enabled.

### Notification Service

- Notification DB connection;
- RabbitMQ connection;
- consumer retry/concurrency;
- HTTP read API settings.

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

## 5. Compose topology

Conceptual services:

```yaml
services:
  web:
  gateway:
  product-service:
  order-service:
  notification-service:
  postgres:
  rabbitmq:
```

Volumes:

- PostgreSQL data;
- RabbitMQ data;
- optional development NuGet/npm caches are not production volumes.

Networks:

- one internal application network is enough for baseline;
- only web/gateway and development RabbitMQ management ports are published;
- database ports are optional debug overrides, not default public exposure.

## 6. Startup dependencies

Compose `depends_on` controls startup order, not business readiness.

Services must:

- retry initial dependency connection with bounded startup policy where appropriate;
- expose readiness;
- fail clearly if configuration is invalid;
- tolerate Notification Service starting before/after queue topology;
- not assume database schema exists without explicit migration step.

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
6. Apply migrations.
7. Start Product, Order, Notification.
8. Verify service readiness.
9. Start Gateway.
10. Verify Gateway routes.
11. Start Web.
12. Run smoke order.
13. Verify broker event and notification.
14. Monitor logs/error queue.

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
- inspect timeout/circuit state;
- use trace ID;
- inspect whether an inventory reservation exists for affected order.

### Order is `inventory_unknown`

- do not manually mark confirmed immediately;
- query Product reservation by order ID;
- if identical reservation exists, reconcile Order from authoritative snapshot;
- if no reservation and Product is healthy, retry same idempotent reservation;
- record reconciliation evidence.

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

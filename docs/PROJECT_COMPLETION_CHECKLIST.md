# Verified Completion Snapshot

Last verified: 2026-08-18.

Bootstrap implementation commit: `b2a924d` (`chore(repo): bootstrap Phase 0 standards`).

Current implementation slice: Phase 1 Product catalog/update API plus Angular catalog/operator UI, Phase 2 Order persistence/API, Phase 3 Product reservation plus Order typed-client/orchestration/cancellation, Phase 4 Gateway routing/safety/frontend client slices, Phase 5 contract/transport/Notification persistence/read API/Angular UI and RabbitMQ recovery slices, Phase 6.1/6.2 runtime images plus full-stack Compose, Phase 7.1-7.6 outbox operations/Notification inbox hardening/controlled reconciliation/bounded resilience, and Phase 8.1-8.6 observability, local log search, Playwright Compose E2E, and failure-injection automation, implemented in the commits recorded in `docs/agent/task.md`.

The detailed implementation checklist remains [`docs/agent/task.md`](agent/task.md). This file records the repository state and evidence verified during the current autonomous slice so that a later agent can audit the checklist against executable files and commands without treating scaffolding as business completion.

## Phase 0 — Repository Bootstrap and Standards

| Area | Status | Verified evidence |
| --- | --- | --- |
| Repository structure and solution | `[x]` | `MicroShop.sln`, `src/`, `tests/`, `web/microshop-ui/`, `deploy/`, `scripts/`, and `docs/project/` exist. |
| .NET project hosts | `[x]` | Gateway, Product, Order, Notification, Contracts, ServiceDefaults, and architecture test projects restore/build. |
| Angular workspace | `[x]` | Angular CLI 22.1.2 workspace with strict TypeScript/template settings, ESLint, Vitest, and committed `package-lock.json`. |
| Version and package pinning | `[x]` | `global.json` pins SDK 10.0.302; `.nvmrc`/`package.json` pin Node/npm; central NuGet package management and per-project `packages.lock.json` files are committed. |
| Code quality and secrets policy | `[x]` | `.editorconfig`, `.gitignore`, `.env.example`, Angular lint target, and CI credential-pattern guard exist. |
| Initial CI | `[x]` | `.github/workflows/ci.yml` covers .NET, Angular, Compose infrastructure, whitespace, secret checks, and Product migration application to an empty PostgreSQL database. CI image execution remains a later Phase 6/8 validation item. |
| PostgreSQL/RabbitMQ Compose | `[x]` | `deploy/compose.yaml` validates and starts the full Web/Gateway/Product/Order/Notification stack plus PostgreSQL/RabbitMQ; PostgreSQL creates three logical databases/users and RabbitMQ management is exposed for local learning. |
| Phase 0 validation gate | `[x]` | Local .NET, Angular, full-stack Compose, and Product empty-database migration checks pass. CI image execution remains a later validation item. |

## Commands verified

- `dotnet restore MicroShop.sln`
- `dotnet format MicroShop.sln --no-restore`
- `dotnet format MicroShop.sln --verify-no-changes --no-restore`
- `dotnet build MicroShop.sln --configuration Release --no-restore`
- `dotnet test MicroShop.sln --configuration Release`
- `npm ci`
- `npm run lint`
- `npm run test -- --watch=false`
- `npm run build`
- `npm audit --omit=dev --audit-level=high` (no production vulnerabilities)
- `docker compose --env-file .env.example -f deploy/compose.yaml config`
- `docker compose --env-file .env.example -f deploy/compose.yaml up --build -d`
- `docker compose --env-file .env.example -f deploy/compose.yaml ps --all`
- Full-stack Web/Gateway Product, Order, Notification API smoke through port 8080, including confirmed Order and eventual Notification delivery
- Compose observability smoke with W3C trace root `11111111111111111111111111111111` crossing Gateway, Order, Product, outbox/RabbitMQ, and Notification; structured logs retained bounded Order/Reservation/Message identifiers
- `scripts/e2e-compose.ps1 -EnvFile .env.example -SkipInstall`: current Compose images/startup plus 2 Playwright tests passed
- `scripts/failure-injection.ps1 -Scenario all -EnvFile .env.example`: Product/Notification/RabbitMQ stopped-service recovery plus duplicate/error-queue, concurrent-last-stock, and outbox integration filters passed; containers and volumes preserved
- `dotnet ef migrations script --project src/Services/ProductService/MicroShop.ProductService --startup-project src/Services/ProductService/MicroShop.ProductService`
- Product `InitialProductSchema` applied to a fresh PostgreSQL Testcontainer and the local Product database
- Product seed command executed twice; database remained at four deterministic seed products
- Product native smoke: `/health/live`, `/health/ready`, `/api/v1/products`, `/openapi/v1.json`

## Deferred and not yet complete

- Playwright end-to-end coverage and the legacy fake-client compatibility gate for Angular checkout.
- Phase 8.7 security/deployment review, 8.8 documentation audit, and 8.9 final completion gate.
- Native Linux/macOS execution of the documented Compose workflow and CI image execution.
- CI execution on GitHub; the workflow is committed but has not been observed remotely from this local run.

Security note: Vitest was upgraded to `4.1.10` during verification to remove a critical development-time advisory. `npm ci` currently reports one moderate and one high development-tool advisory in the Angular toolchain; `npm audit --omit=dev --audit-level=high` reports 0 production vulnerabilities. No production dependency is affected.

## Next recommended slice

Phase 8.7–8.9 — complete the security/deployment review, perform the documentation audit, and run the final CI/release gate.

## Phase 6 — Docker Compose completion (partial)

| Area | Status | Verified evidence |
| --- | --- | --- |
| Runtime images | `[x]` | Five multi-stage Dockerfiles under `deploy/docker/` build successfully as `microshop-*:phase6`; .NET runtime images are SDK-free, non-root, port 8080 only, and the Web image serves `/health`. |
| Full-stack Compose | `[x]` | `deploy/compose.yaml` starts the five application services after three successful migration one-shots; Web `/health`, Gateway API proxying, confirmed Order, and eventual Notification delivery pass. Only Web 8080 and RabbitMQ management 15672 are published by default. |

## Phase 7.1 — Transactional outbox (partial Phase 7)

| Area | Status | Verified evidence |
| --- | --- | --- |
| Order outbox schema and migration | `[x]` | `OutboxMessage`, `OutboxMessageConfiguration`, `OrderDbContext`, and `20260817203650_AddOrderOutbox` create an Order-owned table with stable message ID, aggregate identity, immutable JSON payload, attempt/lease/publish/dead-letter state, indexes, and a unique `(message_type, aggregate_id)` constraint. |
| Atomic confirmation write | `[x]` | `OrderApplicationService` adds the serialized `OrderConfirmedV1` row before the same `SaveChangesAsync` that confirms the Order; no production DI registration remains for the direct broker publisher. |
| Dispatcher and recovery state | `[x]` | `OutboxDispatcher` uses PostgreSQL `FOR UPDATE SKIP LOCKED`, leases, bounded exponential backoff, stable MessageId/correlation/traceparent publication, lease-expiry recovery, and dead-lettering at the configured maximum. |
| Phase 7.1 tests | `[x]` | 44 Order tests pass, including atomic outbox persistence, direct-publish demonstration isolation, stable-ID success, concurrent claim, retry/dead-letter, and restart/lease recovery. |

## Phase 7.2 — Outbox operations

| Area | Status | Verified evidence |
| --- | --- | --- |
| Backlog logging and readiness | `[x]` | `OutboxDispatcher` emits pending/oldest/dead-letter summaries; `OrderOutboxHealthCheck` applies configurable pending count/age and dead-letter policy to `/health/ready`. |
| Operator status and recovery | `[x]` | Read-only `scripts/db-outbox-status.ps1/.sh` report backlog and actionable rows; recovery steps are documented in `docs/project/08_DEPLOYMENT_AND_OPERATIONS.md` without deleting outbox data. |
| RabbitMQ outage/recovery | `[x]` | PostgreSQL/RabbitMQ Testcontainers test confirms confirmed Order durability while RabbitMQ is stopped, readiness within policy, lease/retry recovery, and eventual publish after broker/dispatcher recovery. |
| Metrics scope | `[x]` | Phase 7 established health/backlog logging; Phase 8.3 now adds the shared OpenTelemetry meters and bounded outcome/backlog instruments while preserving health as the local operator surface. |

## Phase 7.3 — Notification inbox/idempotency hardening

| Area | Status | Verified evidence |
| --- | --- | --- |
| Atomic consumer transaction | `[x]` | `OrderConfirmedNotificationHandler` commits `ConsumedMessage` and generated Notification in one explicit Notification DB transaction; oversized notification persistence failure rolls back both rows. |
| Concurrent duplicate redelivery | `[x]` | Eight independent PostgreSQL contexts process one MessageId concurrently; the inbox primary key and source-message unique index leave one inbox row and one Notification. |
| Restart and retry policy | `[x]` | Notification retry count/delay are bounded/configurable; RabbitMQ integration publishes, restarts the Notification host, redelivers the same MessageId, and verifies no duplicate side effect. |
| Retention decision | `[x]` | No cleanup worker is justified for the small demo database; retained inbox/notification history follows the documented demo retention policy and production personal-data retention remains deployment-specific. |

## Phase 7.4 — Controlled reconciliation

| Area | Status | Verified evidence |
| --- | --- | --- |
| Internal/manual route | `[x]` | Order maps `POST /internal/v1/reconciliation/orders/{orderId}`; Gateway has no `/internal/*` route. `OrderApiTests.InternalReconciliationRouteRejectsUnknownOrderWhenReservationIsAbsent` passes. |
| Reservation lookup and intent | `[x]` | Product exposes an order-keyed query; Order persists `order_inventory_request_items` and validates Product reservation identity, state, snapshots, quantities, subtotals, currency, and total before confirmation. |
| Safe inventory/cancellation outcomes | `[x]` | Known absent/released inventory becomes rejected; matching inventory becomes confirmed with the outbox; absent/released cancellation becomes cancelled; reserved cancellation uses one idempotent release; unknown outcomes remain pending. |
| Audit and migration | `[x]` | `OrderReconciliationAudit`, `order_reconciliation_audits`, and `20260818044623_AddOrderReconciliation` are Order-owned and use only local foreign keys. |
| Tests | `[x]` | 60 Order tests pass, including lookup mapping, matching/absent/mismatched inventory, dependency-unavailable pending state, cancellation reconciliation, safe Product-client retries, audit history, and outbox confirmation. |

## Phase 7.5 — Resilience policies

| Area | Status | Verified evidence |
| --- | --- | --- |
| Product timeout and safe retries | `[x]` | `ProductServiceOptions` validates a 1–5 second timeout. `ProductInventoryClient` retries only order-keyed idempotent release and read-only lookup operations, with bounded count/delay and one shared operation timeout; reserve remains single-attempt. |
| Circuit-breaker decision | `[x]` | No circuit breaker is justified for the learning baseline: bounded timeout/retry, explicit ambiguous states, outbox durability, reconciliation, and readiness provide the required controls without adding untuned state. |
| Graceful/consumer shutdown | `[x]` | `ServiceDefaultsExtensions` bounds `HostOptions.ShutdownTimeout`; Order and Notification bind MassTransit start/stop timeouts to it. `MICROSHOP_SHUTDOWN_TIMEOUT_MS` is documented and Compose-wired. |
| Cancellation and readiness | `[x]` | Product client preserves caller cancellation; `ApplicationLifecycleHealthCheck` marks readiness unhealthy on `ApplicationStopping` while `/health/live` remains process-only. `ServiceReadinessTests` passes. |
| Tests | `[x]` | Full .NET suite passes 108 tests: 1 Architecture, 2 Contracts, 15 Notification, 9 Gateway, 60 Order, and 21 Product. |

## Phase 7.6 — Phase 7 validation gate

| Area | Status | Verified evidence |
| --- | --- | --- |
| Outbox/broker durability | `[x]` | RabbitMQ outage/recovery and outbox lease/restart tests prove confirmed Order durability, eventual publish, and no lost event in outbox mode. |
| Notification idempotency | `[x]` | Concurrent duplicate and process-restart redelivery tests leave one consumed-message row and one Notification. |
| Reconciliation | `[x]` | Inventory-unknown and cancellation-pending reconciliation tests prove controlled known/unknown outcomes and audit history. |
| Shutdown gate | `[x]` | Bounded shutdown-option and readiness-transition tests pass; Compose configuration validates with the new environment settings. |

## Phase 8.1 — Structured logging

| Area | Status | Verified evidence |
| --- | --- | --- |
| Shared JSON logging | `[x]` | `MicroShop.ServiceDefaults` configures JSON scopes and shared request/background logging for Product, Order, Notification, and Gateway. |
| Correlation and bounded fields | `[x]` | Service/environment/trace/span fields are standard; Order, Reservation, and Message IDs are added only where relevant. Stable event codes avoid full customer payloads and secrets. |
| Validation | `[x]` | `ObservabilityTests`, full .NET suite (111 passed), Compose config/startup, and a confirmed-order smoke with trace/log evidence pass. |

## Phase 8.2 — Distributed tracing

| Area | Status | Verified evidence |
| --- | --- | --- |
| W3C and OpenTelemetry | `[x]` | Gateway, Order, Product, and Notification use W3C activity identity and centralized OpenTelemetry ASP.NET Core/HTTP/custom source registration. |
| HTTP/RabbitMQ propagation | `[x]` | Gateway preserves the request parent; Product client and outbox producer spans propagate context; Notification creates a consumer span from the message `traceparent`. |
| Optional backend and validation | `[x]` | `OTEL_EXPORTER_OTLP_ENDPOINT` is optional and documented; Compose trace root `11111111111111111111111111111111` crosses all participating services for Order `d0e58ba5-9d6a-4a4b-bd8c-aa0be6666c0c`. |

## Phase 8.3 — Metrics and health

| Area | Status | Verified evidence |
| --- | --- | --- |
| Shared instruments | `[x]` | `MicroShopTelemetry` provides HTTP/dependency/order/reservation/notification counters plus outbox pending/dead-letter gauges with bounded labels. |
| Health semantics | `[x]` | Liveness remains process-only; readiness includes dependency/backlog signals and becomes unhealthy during bounded shutdown. |
| Validation | `[x]` | `ObservabilityTests` and `ServiceReadinessTests` pass; Release build/test and Compose config/rebuild/startup/health/API smoke pass. |

## Phase 8.4 — Local log integration

| Area | Status | Verified evidence |
| --- | --- | --- |
| Optional external integration | `[x]` | No external Log Monitoring System was supplied or added; JSON stdout and optional OTLP endpoint preserve independent local operation. |
| Trace/Order search | `[x]` | Docker logs were searched by trace root `11111111111111111111111111111111` and Order `d0e58ba5-9d6a-4a4b-bd8c-aa0be6666c0c` across all participating services. |
| Leakage policy | `[x]` | Shared middleware/business logs include bounded IDs and stable codes only; payloads, credentials, tokens, and connection strings are excluded and documented in the operations guide. |

## Phase 8.5 — Compose Playwright E2E

| Area | Status | Verified evidence |
| --- | --- | --- |
| Browser setup | `[x]` | `@playwright/test` 1.62.1, Chromium project, failure artifacts, and `e2e`/`e2e:install` scripts are committed under `web/microshop-ui`. |
| Business flow coverage | `[x]` | The suite covers catalog, UI Product create/update, confirmed checkout/detail, bounded Notification polling, cancellation, stock restoration, insufficient stock, and dependency UI behavior. |
| Compose execution | `[x]` | `scripts/e2e-compose.ps1 -EnvFile .env.example -SkipInstall` rebuilt/started the stack and passed 2/2 Playwright tests without removing named volumes. |

## Phase 8.6 — Failure-injection automation

| Area | Status | Verified evidence |
| --- | --- | --- |
| Stopped-service recovery | `[x]` | The harness passed Product stopped dependency failure, Notification stopped durable Order/recovery, and RabbitMQ stopped outbox recovery scenarios. |
| Duplicate/concurrency/error queue | `[x]` | Integration filters passed 2 Notification duplicate/error-queue tests, 1 Product concurrent last-stock test, and 1 Order RabbitMQ outbox recovery test. |
| Safety | `[x]` | The harness only stops/starts named services, creates unique demo data, and preserves containers and Docker volumes. |

## Phase 1 — Product Service foundation

| Area | Status | Verified evidence |
| --- | --- | --- |
| Product domain and validation | `[x]` | Product entity contains identity, bounded text, decimal VND price, stock, active state, UTC timestamps, and explicit `version` concurrency token. Unit validation tests pass. |
| Product database and migration | `[x]` | Product DbContext/configuration, `20260801194513_InitialProductSchema`, `products` constraints, and `ix_products_active_name_id` are committed. Fresh PostgreSQL migration passes. |
| Product seed | `[x]` | Explicit PowerShell/shell seed scripts insert four deterministic products idempotently. |
| Product catalog/create API | `[x]` | Service-native list/detail/create endpoints, pagination, active filtering, Problem Details, stable codes, and development OpenAPI are tested. |
| Product update/activation | `[x]` | PATCH supports mutable fields, direct stock adjustment, activation/deactivation, ETag/If-Match, and stable stale-update conflicts. |
| Product PostgreSQL integration tests | `[x]` | 21 Product tests pass using PostgreSQL Testcontainers, including update/lifecycle, reservation/release/replay/lookup, atomic failures, and competing stock requests; no EF InMemory provider. |
| Product inventory reservation boundary (Phase 3) | `[x]` | Product-owned reservation entities, migrations, internal reserve/release endpoints, authoritative snapshots, idempotency, atomic stock updates, and stable Product-ID row locks are implemented in `d2a885a`. |
| Product Angular screens | `[x]` | Catalog route shows active Products with price/stock and loading/empty/error states; management route uses Reactive Forms for create/update/activate/deactivate and server validation mapping. |
| Phase 1 validation gate | `[x]` | Product service, migration, OpenAPI, update/concurrency, Gateway route, and 11 Angular Product UI tests pass. |

## Phase 2 — Order Service foundation (partial)

| Area | Status | Verified evidence |
| --- | --- | --- |
| Order domain | `[x]` | `Order`, `OrderItem`, `OrderStateHistory`, six documented states, normalized email, snapshot totals, failure fields, timestamps, version token, and transition guard are implemented and unit-tested. |
| Order database and migration | `[x]` | `20260801204113_InitialOrderSchema` creates only `orders`, `order_items`, and `order_state_history` with constraints and query indexes. |
| Order readiness and ownership | `[x]` | Order EF health check/startup validation, explicit `--migrate`, and fresh PostgreSQL credential-isolation test pass. |
| Order HTTP API | `[x]` | `POST /api/v1/orders`, paginated `GET`, detail `GET`, authoritative reservation snapshots, validation, stable Problem Details codes, and OpenAPI metadata are implemented; the fake path is test-only compatibility. |
| Order foundation tests | `[x]` | 45 Order tests pass: native API, typed client HTTP contract, unavailable/timeout/cancellation mapping, orchestration/cancellation state transitions, domain rules, migration persistence, state history, readiness/OpenAPI, database credential isolation, optimistic concurrency, and outbox/direct-publish behavior. |
| Angular Order UI | `[x]` | Checkout, quantity selection, confirmed/rejected/dependency outcomes, Order list/detail, cancellation, loading/empty/error states, and duplicate-submit suppression are implemented through Gateway; the combined Angular suite has 21 tests. |
| Phase 2 validation gate | `[~]` | Order service, migration, native API, real Product-client paths, Order concurrency guard, and Angular Order UI pass. The legacy wording requiring an Angular checkout run with the opt-in fake Product client remains explicitly partial because runtime now uses the real Product HTTP boundary. |

## Phase 3 — Product reservation and Order communication (partial)

| Area | Status | Verified evidence |
| --- | --- | --- |
| Reservation domain and schema | `[x]` | Product owns `inventory_reservations` and `inventory_reservation_items`, request hashes, reserved/released states, snapshots, constraints, and migrations `20260817164457_AddInventoryReservations` plus `20260817164536_AddInventoryReservationConstraints`. |
| Internal reservation API | `[x]` | Native Product endpoints reserve/replay/mismatch and release idempotently; Gateway rejects `/internal/*` without forwarding. |
| Reservation concurrency and failure tests | `[x]` | 21 Product tests pass, including no partial decrement, reservation lookup, and concurrent last-stock behavior on PostgreSQL Testcontainers. |
| Order typed client/orchestration | `[x]` | `ProductInventoryClient` uses the internal URL, explicit <=5s timeout, traceparent/cancellation propagation, stable Product error mapping, no blind retry, stable `orderId`, authoritative snapshot verification, and `inventory_unknown` persistence in `e3c2b7c`. |
| Order cancellation/release | `[x]` | `POST /api/v1/orders/{id}/cancel` guards state, calls idempotent Product release, confirms only after known release, and persists `cancellation_pending` for ambiguous outcomes in `27d57ef`. |
| Phase 3 validation gate | `[x]` | Product reservation, Order-to-Product HTTP/orchestration, cancellation, and Gateway internal-route safety tests pass. |

## Phase 4 — YARP API Gateway (partial)

| Area | Status | Verified evidence |
| --- | --- | --- |
| Gateway foundation | `[x]` | YARP Product/Order clusters, public path transforms, Notification placeholder cluster, CORS, request limit, health, structured logging defaults, and trace forwarding are configured in `src/Gateway/MicroShop.Gateway/`. |
| Gateway safety | `[x]` | Internal routes are rejected, destinations are validated, downstream failures map to stable `502`, and Angular API clients use only same-origin Gateway paths. |
| Gateway integration tests | `[x]` | 7 `MicroShop.Gateway.Tests` pass for Product/Order/Notification transforms, trace headers, health/CORS, downstream failure, internal rejection, and destination validation. |
| Angular Gateway client migration | `[x]` | `ProductApiService`, `OrderApiService`, and `NotificationApiService` use same-origin Gateway paths; the interceptor maps connectivity failures to `GatewayApiError`; Gateway client coverage is included in the current 21-test Angular suite. |
| Phase 4 validation gate | `[~]` | Gateway routing, Product/Order Angular feature screens, and source-level Gateway-only usage pass; application-container port isolation remains in the Phase 6 Compose slice. |

Gateway evidence:

- Commit: `569af30` (`feat(gateway): add public yarp routes`).
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Result: 91 .NET tests pass (1 Architecture, 2 Contracts, 15 Notification, 7 Gateway, 45 Order, 21 Product); only the pre-existing NU1903 SSH.NET warning remains.

## Phase 5 — RabbitMQ and Notification foundation (partial)

| Area | Status | Verified evidence |
| --- | --- | --- |
| Shared `OrderConfirmedV1` contract | `[x]` | Versioned passive records and two JSON compatibility tests are implemented in `MicroShop.Contracts`. |
| RabbitMQ/MassTransit transport foundation | `[x]` | RabbitMQ management Compose service, environment-bound credentials/options, Order bus registration, durable Notification endpoint, bounded retry, framework error queue, bus readiness health, and real RabbitMQ Testcontainers integration coverage are implemented. |
| Direct `OrderConfirmedV1` publish milestone | `[x]` | Order publishes after the confirmed DB commit with explicit message/correlation IDs and traceparent propagation; the direct-publish failure window is tested and documented. |
| Notification persistence and consumer | `[x]` | Notification owns `consumed_messages` and `notifications`, applies `20260817185808_InitialNotificationSchema`, persists both records in one explicit transaction, suppresses concurrent duplicate message IDs, and supports bounded configurable retry; 15 Notification tests pass, including real RabbitMQ publish/consume, rollback, concurrent duplicate, and process-restart redelivery coverage. |
| Notification read API and Gateway route | `[x]` | Filtered/paginated `GET /api/v1/notifications`, idempotent mark-as-read, OpenAPI, and tested `/api/notifications/*` Gateway transform are implemented. |
| Notification Angular UI | `[x]` | `/notifications` route, same-origin client, list/empty/error/loading states, bounded polling, manual refresh, eventual-consistency guidance, and mark-as-read are implemented; 21 Angular tests pass overall. |
| Phase 5 validation gate | `[~]` | Contract/transport/persistence/consumer/read API/UI, duplicate suppression, retry/error queue, publisher independence, restart, and queued recovery are verified; full Compose eventual-flow validation remains. |

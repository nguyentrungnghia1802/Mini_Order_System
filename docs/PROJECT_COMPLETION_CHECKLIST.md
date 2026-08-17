# Verified Completion Snapshot

Last verified: 2026-08-18.

Bootstrap implementation commit: `b2a924d` (`chore(repo): bootstrap Phase 0 standards`).

Current implementation slice: Phase 1 Product catalog/update API plus Angular catalog/operator UI, Phase 2 Order persistence/API, Phase 3 Product reservation plus Order typed-client/orchestration/cancellation, and Phase 4 Gateway routing/safety/frontend client slices, implemented in `abc9a7a`, `0654c31`, `7bf1692`, `d694a5b`, `d2a885a`, `8b021f5`, `e3c2b7c`, `27d57ef`, `569af30`, `f750963`, and `185a8cc` (`feat(ui): add product catalog management`).

The detailed implementation checklist remains [`docs/agent/task.md`](agent/task.md). This file records the repository state and evidence verified during the current autonomous slice so that a later agent can audit the checklist against executable files and commands without treating scaffolding as business completion.

## Phase 0 — Repository Bootstrap and Standards

| Area | Status | Verified evidence |
| --- | --- | --- |
| Repository structure and solution | `[x]` | `MicroShop.sln`, `src/`, `tests/`, `web/microshop-ui/`, `deploy/`, `scripts/`, and `docs/project/` exist. |
| .NET project hosts | `[x]` | Gateway, Product, Order, Notification, Contracts, ServiceDefaults, and architecture test projects restore/build. |
| Angular workspace | `[x]` | Angular CLI 22.1.2 workspace with strict TypeScript/template settings, ESLint, Vitest, and committed `package-lock.json`. |
| Version and package pinning | `[x]` | `global.json` pins SDK 10.0.302; `.nvmrc`/`package.json` pin Node/npm; central NuGet package management and per-project `packages.lock.json` files are committed. |
| Code quality and secrets policy | `[x]` | `.editorconfig`, `.gitignore`, `.env.example`, Angular lint target, and CI credential-pattern guard exist. |
| Initial CI | `[x]` | `.github/workflows/ci.yml` covers .NET, Angular, Compose infrastructure, whitespace, secret checks, and Product migration application to an empty PostgreSQL database. Image validation remains deferred. |
| PostgreSQL/RabbitMQ Compose | `[x]` | `deploy/compose.yaml` validates and starts; PostgreSQL creates three logical databases/users; RabbitMQ management is exposed for local learning. |
| Phase 0 validation gate | `[~]` | Local .NET, Angular, Compose, and Product empty-database migration checks pass. Application image build remains deferred to Phase 6. |

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
- `docker compose --env-file .env.example -f deploy/compose.yaml up -d`
- `docker compose --env-file .env.example -f deploy/compose.yaml ps`
- `dotnet ef migrations script --project src/Services/ProductService/MicroShop.ProductService --startup-project src/Services/ProductService/MicroShop.ProductService`
- Product `InitialProductSchema` applied to a fresh PostgreSQL Testcontainer and the local Product database
- Product seed command executed twice; database remained at four deterministic seed products
- Product native smoke: `/health/live`, `/health/ready`, `/api/v1/products`, `/openapi/v1.json`

## Deferred and not yet complete

- Notification UI, Playwright end-to-end coverage, and the legacy fake-client compatibility gate for Angular checkout.
- Notification business behavior, migration, and integration tests.
- MassTransit producer/consumer and transactional outbox.
- Application Dockerfiles and full-stack Compose services.
- Docker image build validation.
- CI execution on GitHub; the workflow is committed but has not been observed remotely from this local run.

Security note: Vitest was upgraded to `4.1.10` during verification to remove a critical development-time advisory. `npm ci` currently reports one moderate and one high development-tool advisory in the Angular toolchain; `npm audit --omit=dev --audit-level=high` reports 0 production vulnerabilities. No production dependency is affected.

## Next recommended slice

Phase 5 — implement versioned RabbitMQ contracts, Notification persistence/consumer, and the remaining legacy fake-client compatibility decision.

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
| Order foundation tests | `[x]` | 39 Order tests pass: native API, typed client HTTP contract, unavailable/timeout/cancellation mapping, orchestration/cancellation state transitions, domain rules, migration persistence, state history, readiness/OpenAPI, database credential isolation, and the optimistic-concurrency guard. |
| Angular Order UI | `[x]` | Checkout, quantity selection, confirmed/rejected/dependency outcomes, Order list/detail, cancellation, loading/empty/error states, and duplicate-submit suppression are implemented through Gateway; 16 Angular tests pass. |
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
| Gateway integration tests | `[x]` | 6 `MicroShop.Gateway.Tests` pass for Product/Order transforms, trace headers, health/CORS, downstream failure, internal rejection, and destination validation. |
| Angular Gateway client migration | `[x]` | `ProductApiService` and `OrderApiService` use `/api/products` and `/api/orders`; the interceptor maps connectivity failures to `GatewayApiError`; Gateway client coverage is included in the current 16-test Angular suite. |
| Phase 4 validation gate | `[~]` | Gateway routing, Product/Order Angular feature screens, and source-level Gateway-only usage pass; application-container port isolation remains in the Phase 6 Compose slice. |

Gateway evidence:

- Commit: `569af30` (`feat(gateway): add public yarp routes`).
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Result: 70 .NET tests pass (1 Architecture, 2 Contracts, 1 Notification bootstrap, 6 Gateway, 39 Order, 21 Product); only the pre-existing NU1903 SSH.NET warning remains.

## Phase 5 — RabbitMQ and Notification foundation (partial)

| Area | Status | Verified evidence |
| --- | --- | --- |
| Shared `OrderConfirmedV1` contract | `[x]` | Versioned passive records and two JSON compatibility tests are implemented in `MicroShop.Contracts`. |
| RabbitMQ/MassTransit transport foundation | `[x]` | RabbitMQ management Compose service, environment-bound credentials/options, Order bus registration, durable Notification endpoint, bounded retry, framework error queue, and bus readiness health are configured. |
| Notification persistence/consumer/API/UI | `[ ]` | Deferred to the remaining Phase 5 vertical slice. |
| Phase 5 validation gate | `[ ]` | Publishing, consuming, duplicate suppression, read API/UI, restart behavior, and eventual Notification flow remain unimplemented. |

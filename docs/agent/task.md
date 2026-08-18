# Mini Order System — Project Completion Tasks

Last reviewed: 2026-08-18.

This file is the canonical execution checklist for completing **Mini Order System / MicroShop**.

The project is complete only when every required item in Phases 0–8 is marked `[x]`, all release gates pass, the repository is documented, and the full system runs end to end.

Status symbols:

- `[ ]` Not started
- `[~]` Partially completed
- `[x]` Completed and validated
- `[!]` Blocked

Evidence format:

```text
Evidence:
- Files:
- Tests:
- Commands:
- Commit:
- Notes:
```

Do not mark any task `[x]` without implementation and successful validation.

---

# Phase 0 — Repository Bootstrap and Standards

## 0.1 Repository structure

- [x] Create the root repository structure:
  - `src/Gateway/`
  - `src/BuildingBlocks/`
  - `src/Services/ProductService/`
  - `src/Services/OrderService/`
  - `src/Services/NotificationService/`
  - `tests/`
  - `web/microshop-ui/`
  - `deploy/`
  - `scripts/`
  - `docs/project/`
- [x] Create `MicroShop.sln`.
- [x] Create one deployable .NET project for each service.
- [x] Create the Angular workspace.
- [x] Add all specification files under `docs/project/`.
- [x] Add `AGENT.md`.
- [x] Add this `task.md`.
- [x] Add a root `README.md` linking architecture, setup, and task tracking.

Evidence for 0.1:

- Files: `MicroShop.sln`, `src/`, `tests/`, `web/microshop-ui/`, `deploy/`, `scripts/`, `docs/project/`, `README.md`, `docs/agent/AGENT.md`, `docs/agent/task.md`.
- Tests: `MicroShop.Architecture.Tests` and Angular workspace test exist.
- Commands: `dotnet restore MicroShop.sln`; `git status --short`; `rg --files`.
- Commit: `b2a924d` (`chore(repo): bootstrap Phase 0 standards`).
- Notes: Business features remain deferred to Phases 1–5.

## 0.2 Version pinning

- [x] Add `global.json` for the selected .NET SDK.
- [x] Add `Directory.Build.props`.
- [x] Add `Directory.Packages.props`.
- [x] Enable nullable reference types.
- [x] Enable warnings as errors in CI.
- [x] Pin Angular and Node dependencies.
- [x] Commit lockfiles.

Evidence for 0.2:

- Files: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.nvmrc`, `web/microshop-ui/package.json`, `web/microshop-ui/package-lock.json`, and seven `packages.lock.json` files.
- Tests: restore completed with central package management and lock files.
- Commands: `dotnet restore MicroShop.sln`; `npm ci`.
- Commit: `b2a924d` (`chore(repo): bootstrap Phase 0 standards`).
- Notes: SDK `10.0.302`, Node `24.15.0`, npm `11.12.1`; CI sets `TreatWarningsAsErrors`.

## 0.3 Code quality

- [x] Add `.editorconfig`.
- [x] Configure `dotnet format`.
- [x] Configure Angular linting.
- [x] Configure strict TypeScript.
- [x] Configure strict Angular template checking.
- [x] Add `.gitignore`.
- [x] Ensure `.env` and secrets are ignored.
- [x] Add `.env.example` with placeholders only.

Evidence for 0.3:

- Files: `.editorconfig`, `.gitignore`, `.env.example`, `web/microshop-ui/eslint.config.js`, `web/microshop-ui/angular.json`.
- Tests: `dotnet format --verify-no-changes` and Angular lint pass.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `npm run lint`.
- Commit: `b2a924d` (`chore(repo): bootstrap Phase 0 standards`).
- Notes: No `.env`, token, dump, log, or credential-bearing file is tracked.

## 0.4 Initial CI

- [x] Add CI for .NET restore/build/test.
- [x] Add CI for Angular install/lint/test/build.
- [x] Add migration validation against empty PostgreSQL databases.
- [~] Add Docker image build validation.
- [x] Add secret scanning or equivalent repository protection.

Evidence for 0.4:

- Files: `.github/workflows/ci.yml`.
- Tests: workflow syntax reviewed; local constituent commands pass where configuration exists.
- Commands: `dotnet restore`; `dotnet build`; `dotnet test`; `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`; Compose config/up/ps.
- Commit: `b2a924d` for the original CI foundation; Product migration validation is added in `abc9a7a` (`feat(product): add catalog persistence slice`).
- Notes: The CI workflow now applies Product migrations to an empty PostgreSQL service database. Phase 6.1 application images now build locally; adding image builds to CI and full-stack Compose validation remain later gates.

## 0.5 Phase 0 validation gate

- [x] `dotnet restore` passes.
- [x] `dotnet build --configuration Release` passes.
- [x] `dotnet test --configuration Release` passes.
- [x] Angular install/lint/test/build passes.
- [x] Docker Compose infrastructure starts.
- [x] Repository contains no committed secret.

Evidence for 0.5:

- Files: `docs/PROJECT_COMPLETION_CHECKLIST.md`, `.github/workflows/ci.yml`, `deploy/compose.yaml`.
- Tests: .NET build/test, Angular lint/test/build, PostgreSQL health, RabbitMQ health, and Compose status pass locally.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release`; `dotnet test MicroShop.sln --configuration Release`; `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`; `docker compose ... config/up/ps`.
- Commit: `b2a924d` (`chore(repo): bootstrap Phase 0 standards`).
- Notes: Full-stack Compose, migration ordering, business integration, and E2E gates remain incomplete by design; the five runtime images are now covered by Phase 6.1.

---

# Phase 1 — Product Service

## 1.1 Product domain

- [x] Define Product entity.
- [x] Add fields:
  - ID
  - name
  - description
  - unit price
  - currency
  - available stock
  - active state
  - timestamps
  - concurrency token
- [x] Enforce nonnegative price.
- [x] Enforce nonnegative stock.
- [x] Use `decimal` for money.
- [x] Use UTC timestamps.

Evidence for 1.1:

- Files: `src/Services/ProductService/MicroShop.ProductService/Persistence/Entities/Product.cs`, `Features/Products/ProductContracts.cs`, `Features/Products/ProductEndpoints.cs`.
- Tests: `ProductValidationTests` covers required/bounded text, currency, nonnegative price/stock, and pagination bounds.
- Commands: `dotnet build MicroShop.sln --configuration Release`; `dotnet test MicroShop.sln --configuration Release`.
- Commit: `abc9a7a` (`feat(product): add catalog persistence slice`).
- Notes: `version` is an explicit EF concurrency token used by the Product PATCH contract; Product reservation concurrency is implemented in the Phase 3 slice `d2a885a`.

## 1.2 Product database

- [x] Create Product DbContext.
- [x] Create EF Core entity configuration.
- [x] Configure PostgreSQL.
- [x] Create Product database migration.
- [x] Add required indexes.
- [x] Add database constraints.
- [x] Add Product database health check.
- [x] Add Product database seed script.

Evidence for 1.2:

- Files: `Persistence/ProductDbContext.cs`, `Persistence/Configurations/ProductConfiguration.cs`, `Persistence/Migrations/20260801194513_InitialProductSchema.cs`, `Infrastructure/Database/ProductDatabaseOptions.cs`, `scripts/db-migrate-product.ps1`, `scripts/db-seed-products.ps1` and shell equivalents.
- Tests: Product Testcontainers fixture applies the migration to a fresh PostgreSQL database; constraint test rejects negative stock.
- Commands: `dotnet ef migrations script`; `dotnet ef database update`; `./scripts/db-seed-products.ps1` twice; `docker compose ... config/up/ps`.
- Commit: `abc9a7a` (`feat(product): add catalog persistence slice`).
- Notes: Schema contains only Product-owned `products`; seed inserts four deterministic products and is idempotent. Existing developer volumes were preserved.

## 1.3 Product API

- [x] Implement `GET /api/v1/products`.
- [x] Implement pagination.
- [x] Implement active-only shopper listing.
- [x] Implement optional inactive Product listing.
- [x] Implement `GET /api/v1/products/{id}`.
- [x] Implement `POST /api/v1/products`.
- [x] Implement `PATCH /api/v1/products/{id}`.
- [x] Implement activate/deactivate behavior.
- [x] Do not implement hard delete.
- [x] Add validation.
- [x] Add RFC 7807 Problem Details.
- [x] Add stable error codes.
- [x] Add OpenAPI documentation.

Evidence for 1.3:

- Files: `Features/Products/ProductContracts.cs`, `Features/Products/ProductEndpoints.cs`, `Program.cs`.
- Tests: `ProductApiTests.CreateGetAndListProductRoundTrip`, `InactiveProductIsHiddenByDefaultAndVisibleForOperatorListing`, `PatchUpdatesMutableFieldsAndReturnsNewVersion`, `PatchCanDeactivateAndReactivateProduct`, and `ConcurrentPatchesWithSameVersionProduceOneSuccessAndOneConflict`.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`; Development smoke remains verified for `/api/v1/products` and `/openapi/v1.json`.
- Commit: `0654c31` (`feat(product): add optimistic catalog updates`).
- Notes: PATCH uses `If-Match`/`ETag`, increments the explicit version, and returns `409 PRODUCT_CONCURRENCY_CONFLICT` for stale or racing updates. The browser-facing `/api/products` mapping is implemented in Gateway; there is still no hard-delete endpoint.

## 1.4 Product tests

- [x] Unit-test Product validation.
- [x] Integration-test Product creation.
- [x] Integration-test Product listing.
- [x] Integration-test Product update.
- [x] Test inactive Product visibility.
- [x] Test database constraints.
- [x] Test Product concurrency conflict.
- [x] Use PostgreSQL Testcontainers.

Evidence for 1.4:

- Files: `tests/MicroShop.ProductService.Tests/ProductValidationTests.cs`, `ProductApiTests.cs`, `ProductApiFixture.cs`.
- Tests: 11 tests pass, including real migration, create/list/detail, update/versioning, activation filtering, Problem Details, competing PATCH conflict, and PostgreSQL check constraint behavior.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Commit: `0654c31` (`feat(product): add optimistic catalog updates`).
- Notes: EF Core InMemory is not used. The concurrency test uses two simultaneous HTTP PATCH requests against PostgreSQL; exactly one succeeds for the same initial version.

## 1.5 Angular Product screens

- [x] Create Product catalog route.
- [x] Display active Product list.
- [x] Display price and available stock.
- [x] Handle loading state.
- [x] Handle empty state.
- [x] Handle API error state.
- [x] Create Product management route.
- [x] Create Product form using Reactive Forms.
- [x] Add create Product UI.
- [x] Add update Product UI.
- [x] Add activate/deactivate UI.
- [x] Map server validation errors to controls.
- [x] Ensure responsive layout and keyboard use.

Evidence for 1.5:

- Files: `web/microshop-ui/src/app/app.routes.ts`, `app.ts`, `app.html`, `features/products/product-catalog.component.*`, `product-management.component.*`, and `core/api/product-api.service.ts`.
- Tests: 11 Angular tests pass, covering Gateway Product listing, catalog ready/empty/error states, Product create request mapping, `If-Match` update, activation/deactivation, and server validation mapping.
- Commands: `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`; `git diff --check`.
- Commit: `185a8cc` (`feat(ui): add product catalog management`).
- Notes: The catalog uses active-only `/api/products`; the operator view explicitly requests `includeInactive=true`. Product updates send the current body version as the quoted `If-Match` header, and the UI disables active mutations/submission while saving.

## 1.6 Phase 1 validation gate

- [x] Product Service starts independently.
- [x] Product migration applies to an empty database.
- [x] Product API OpenAPI is reachable.
- [x] Product tests pass.
- [x] Angular Product UI works through the intended public route.
- [x] Product Service uses only its own database.

Evidence for 1.6:

- Files: Product host, Product migration, `.github/workflows/ci.yml`, and `tests/MicroShop.ProductService.Tests/`.
- Tests: Product PostgreSQL Testcontainers suite, native smoke (`/health/live=200`, `/health/ready=200`, catalog `200`, OpenAPI `200`), and 11 Angular Product UI tests pass.
- Commands: `dotnet restore MicroShop.sln`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release`; `dotnet test MicroShop.sln --configuration Release`; `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`; `docker compose ... config/up/ps`.
- Commits: `abc9a7a` (`feat(product): add catalog persistence slice`), `0654c31` (`feat(product): add optimistic catalog updates`), and `185a8cc` (`feat(ui): add product catalog management`).
- Notes: Product native/API/database and Angular catalog/operator UI are complete through the Gateway route. The Product reservation boundary is implemented separately in Phase 3; Angular checkout/Order UI is tracked and implemented under Phase 2.5.

---

# Phase 2 — Order Service Foundation

## 2.1 Order domain

- [x] Define Order entity.
- [x] Define OrderItem entity.
- [x] Define OrderStateHistory entity.
- [x] Add Order states:
  - `pending_inventory`
  - `confirmed`
  - `rejected`
  - `inventory_unknown`
  - `cancellation_pending`
  - `cancelled`
- [x] Add customer name and normalized email.
- [x] Add total and currency.
- [x] Add failure code/detail.
- [x] Add timestamps.
- [x] Add concurrency token.
- [x] Define valid state transitions.

Evidence for 2.1:

- Files: `src/Services/OrderService/MicroShop.OrderService/Domain/OrderStatuses.cs`, `Persistence/Entities/Order.cs`, `OrderItem.cs`, `OrderStateHistory.cs`.
- Tests: `OrderDomainTests.CreateNormalizesCustomerEmailAndRecordsPendingState`, `AddItemsCalculatesTotalFromProductSnapshots`, `DuplicateProductIdsAreRejected`, `TransitionToConfirmedIncrementsVersionAndRecordsHistory`, `InvalidTransitionIsRejected`, and `RepeatedCancelledTransitionIsIdempotent`.
- Commands: `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release`.
- Commit: `7bf1692` (`feat(order): add persistence foundation`).
- Notes: Order status transitions remain local domain behavior; no Product database or shared entity is referenced.

## 2.2 Order database

- [x] Create Order DbContext.
- [x] Create EF entity configurations.
- [x] Configure PostgreSQL.
- [x] Create initial Order migration.
- [x] Add state constraints.
- [x] Add list/query indexes.
- [x] Add Order database health check.
- [x] Verify Order database credentials cannot access Product database.

Evidence for 2.2:

- Files: `Infrastructure/Database/OrderDatabaseOptions.cs`, `Persistence/OrderDbContext.cs`, `Persistence/OrderDbContextFactory.cs`, `Persistence/Configurations/`, `Persistence/Migrations/20260801204113_InitialOrderSchema.cs`, `deploy/postgres-init/001-create-service-databases.sh`, and `scripts/db-migrate-order.ps1/.sh`.
- Tests: `OrderPersistenceTests.PersistsOrderItemsAndStateHistoryWithAuthoritativeSnapshots`, `DatabaseRejectsUnknownOrderStatus`, `OrderDatabaseCredentialsCannotConnectToProductDatabase`, and `OrderServiceStartsWithOwnedDatabaseAndReadiness`.
- Commands: `dotnet-ef migrations script --project src/Services/OrderService/MicroShop.OrderService --startup-project src/Services/OrderService/MicroShop.OrderService`; fresh PostgreSQL Testcontainers migration; `dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release`.
- Commit: `7bf1692` (`feat(order): add persistence foundation`).
- Notes: The migration owns only Order tables; no outbox table or cross-service foreign key is introduced. Fresh bootstrap revokes database CONNECT from PUBLIC and grants it only to the owning role.

## 2.3 Order API foundation

- [x] Implement request DTO containing customer and Product IDs/quantities only.
- [x] Reject browser-supplied price/name/total/status.
- [x] Reject duplicate Product IDs.
- [x] Enforce item-count limits.
- [x] Enforce quantity limits.
- [x] Implement `POST /api/v1/orders`.
- [x] Initially use a fake Product client behind an interface.
- [x] Persist `pending_inventory`.
- [x] Persist known fake Product snapshots.
- [x] Transition to `confirmed`.
- [x] Implement `GET /api/v1/orders`.
- [x] Implement `GET /api/v1/orders/{id}`.
- [x] Add pagination.
- [x] Add Problem Details and stable error codes.
- [x] Add OpenAPI.

Evidence for 2.3:

- Files: `Features/Orders/OrderContracts.cs`, `OrderApplicationService.cs`, `OrderEndpoints.cs`, `Infrastructure/Products/IProductCatalogClient.cs`, `FakeProductCatalogClient.cs`, `FakeProductCatalog.cs`, and `Program.cs`.
- Tests: `OrderApiTests` covers authoritative request validation, duplicate/quantity rejection, pending-to-confirmed persistence, rejected fake Product outcomes, pagination, detail, and stable validation codes.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`; `git diff --check`.
- Commit: `d694a5b` (`feat(order): add fake order API foundation`).
- Notes: The deterministic fake catalog remains available only for explicit Phase 2 compatibility tests. Runtime Order creation now uses the typed Product reservation client; cancellation is implemented in the Phase 3 slice.

## 2.4 Order tests

- [x] Unit-test Order transition rules.
- [x] Unit-test duplicate Product ID rejection.
- [x] Unit-test total calculation from snapshots.
- [x] Integration-test Order creation.
- [x] Integration-test Order listing.
- [x] Integration-test Order detail.
- [x] Integration-test state-history persistence.
- [x] Test Order concurrency guard.

Evidence for 2.4:

- Files: `tests/MicroShop.OrderService.Tests/OrderDomainTests.cs`, `OrderApiTests.cs`, `OrderDatabaseFixture.cs`, and `OrderPersistenceTests.cs`.
- Tests: 40 Order tests pass, including HTTP creation, known Product rejection, listing/detail pagination, domain transitions, migration persistence, state history, status constraints, readiness/OpenAPI, database credential isolation, typed Product HTTP mapping, timeout/unavailable handling, real orchestration state mapping, cancellation release/idempotency, the database optimistic-concurrency guard, and direct `OrderConfirmedV1` publication behavior.
- Commands: `dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~RejectsConcurrentOrderStateTransition`; `dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-restore`.
- Commits: `7bf1692` (`feat(order): add persistence foundation`), `d694a5b` (`feat(order): add fake order API foundation`), and `33d7c70` (`test(order): cover concurrent transitions`).
- Notes: Two independent DbContexts transition the same persisted order; the first save wins and the second is rejected by the `version` concurrency token without adding a second state-history record.

## 2.5 Angular Order foundation

- [x] Create checkout route.
- [x] Add customer form.
- [x] Add Product quantity selection.
- [x] Submit through Order API.
- [x] Display confirmed result.
- [x] Display rejected result.
- [x] Display dependency error.
- [x] Create Order list route.
- [x] Create Order detail route.
- [x] Handle loading/empty/error states.
- [x] Prevent duplicate UI submission while active.

Evidence for 2.5:

- Files: `web/microshop-ui/src/app/app.routes.ts`, `features/orders/order-checkout.component.*`, `order-list.component.*`, `order-detail.component.*`, and `core/api/order-api.service.ts`.
- Tests: 21 Angular tests pass, including checkout request/quantity mapping, confirmed/rejected/dependency outcomes, duplicate-submit suppression, Order list rendering, detail loading, cancellation, Notification API paths, and Notification UI states.
- Commands: `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`; `git diff --check`.
- Commit: `20bf3d8` (`feat(ui): add order checkout and history`).
- Notes: All browser calls use same-origin `/api/orders` and `/api/products` Gateway paths. Known Product failures are shown as rejected; unavailable/ambiguous outcomes are shown as unknown dependency outcomes rather than false confirmation.

## 2.6 Phase 2 validation gate

- [x] Order Service starts independently.
- [x] Order migration applies cleanly.
- [x] Order tests pass.
- [~] Angular checkout works through the real Product HTTP path.
- [x] Order Service does not access Product database.

Evidence for 2.6 (partial Phase 2 gate):

- Files: `src/Services/OrderService/MicroShop.OrderService/Program.cs`, Order migration, `tests/MicroShop.OrderService.Tests/`, and `.github/workflows/ci.yml`.
- Tests: 40 Order tests and 21 Angular tests pass; readiness, native Order API, OpenAPI, Gateway checkout contract, Product HTTP orchestration, optimistic concurrency, direct event publication, and Notification UI coverage are covered.
- Commands: `dotnet restore MicroShop.sln`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release`; `dotnet test MicroShop.sln --configuration Release`; `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`.
- Commits: `7bf1692` (`feat(order): add persistence foundation`), `d694a5b` (`feat(order): add fake order API foundation`), and `20bf3d8` (`feat(ui): add order checkout and history`).
- Notes: The legacy fake Product client remains opt-in compatibility coverage; the default runtime and Angular flow use real Product HTTP communication and explicit unknown outcomes. The exact fake-client Angular gate is retained as partial rather than being claimed by a frontend HTTP stub.

---

# Phase 3 — Synchronous Product–Order Communication

## 3.1 Inventory reservation domain

- [x] Define InventoryReservation entity.
- [x] Define InventoryReservationItem entity.
- [x] Add reservation states:
  - `reserved`
  - `released`
- [x] Add `orderId` unique constraint.
- [x] Add canonical request hash.
- [x] Add Product snapshot fields.
- [x] Add reservation timestamps.

Evidence for 3.1:

- Files: `Domain/InventoryReservationStatuses.cs`, `Persistence/Entities/InventoryReservation.cs`, and `InventoryReservationItem.cs`.
- Tests: `InventoryApiTests` verifies reserved/released lifecycle, immutable Product snapshots, canonical replay, and release timestamps.
- Commands: `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.ProductService.Tests/MicroShop.ProductService.Tests.csproj --configuration Release --no-restore`.
- Commit: `d2a885a` (`feat(product): add atomic inventory reservations`).
- Notes: Reservation identity is Product-owned; `orderId` is a scalar idempotency key and is not a foreign key to the Order database.

## 3.2 Inventory reservation database

- [x] Create migration for reservation tables.
- [x] Add unique `(reservationId, productId)` constraint.
- [x] Add unique `orderId`.
- [x] Add status constraints.
- [x] Add indexes for order lookup.
- [x] Add concurrency-safe stock update strategy.
- [x] Lock rows in stable Product-ID order.

Evidence for 3.2:

- Files: `Persistence/Configurations/InventoryReservationConfiguration.cs`, `InventoryReservationItemConfiguration.cs`, `Persistence/Migrations/20260817164457_AddInventoryReservations.cs`, and `20260817164536_AddInventoryReservationConstraints.cs`.
- Tests: Product PostgreSQL Testcontainers apply both reservation migrations to a fresh database; concurrent last-stock and release tests verify transactional stock behavior.
- Commands: `dotnet-ef migrations script --project src/Services/ProductService/MicroShop.ProductService --startup-project src/Services/ProductService/MicroShop.ProductService`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Commit: `d2a885a` (`feat(product): add atomic inventory reservations`).
- Notes: PostgreSQL advisory locks serialize an order ID, while `SELECT ... ORDER BY id FOR UPDATE` locks Product rows in stable ID order. No Order database table or cross-service foreign key is created.

## 3.3 Internal Product API

- [x] Implement `POST /internal/v1/inventory/reservations`.
- [x] Implement atomic multi-item reservation.
- [x] Reject full reservation on any invalid item.
- [x] Return authoritative Product snapshots.
- [x] Return total amount.
- [x] Return `201` for new reservation.
- [x] Return `200` for idempotent replay.
- [x] Return `RESERVATION_REQUEST_MISMATCH` for same order with different items.
- [x] Implement `POST /internal/v1/inventory/reservations/{orderId}/release`.
- [x] Make release idempotent.
- [x] Implement internal reservation query for reconciliation.
- [x] Ensure Gateway does not expose internal endpoints.

Evidence for 3.3:

- Files: `Features/Inventory/InventoryContracts.cs`, `InventoryReservationService.cs`, `InventoryEndpoints.cs`, Product `Program.cs`, `src/Gateway/MicroShop.Gateway/appsettings.json`, and `tests/MicroShop.ProductService.Tests/InventoryApiTests.cs` plus `tests/MicroShop.Gateway.Tests/GatewayApiTests.cs`.
- Tests: `InventoryApiTests` covers `201` creation, authoritative snapshots/totals, `404 PRODUCT_NOT_FOUND`, `409 PRODUCT_INACTIVE`, `409 INSUFFICIENT_STOCK`, replay, mismatch, release, idempotent release, reservation lookup by order ID, and `404 RESERVATION_NOT_FOUND`.
- Tests: Gateway integration coverage rejects `/internal/v1/inventory/reservations` without forwarding and rejects invalid HTTP destination configuration; Product reservation behavior remains covered by `InventoryApiTests`.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.ProductService.Tests/MicroShop.ProductService.Tests.csproj --configuration Release --no-restore`; `git diff --check`.
- Commits: `d2a885a` (`feat(product): add atomic inventory reservations`), `042d8fa` (`feat(product): add reservation lookup`), and `569af30` (`feat(gateway): add public yarp routes`).
- Notes: `GET /internal/v1/inventory/reservations/by-order/{orderId}` returns current reservation state, snapshots, and release timestamp for controlled reconciliation. The API remains service-native and Gateway-excluded.

## 3.4 Order Product client

- [x] Create typed `HttpClient`.
- [x] Configure Product Service internal URL.
- [x] Configure explicit timeout.
- [x] Propagate `traceparent`.
- [x] Propagate cancellation token.
- [x] Map Product business errors.
- [x] Map dependency unavailable.
- [x] Map ambiguous timeout to `inventory_unknown`.
- [x] Avoid blind retries.
- [x] Use stable `orderId` for safe replay.

Evidence for 3.4:

- Files: `Infrastructure/Products/ProductInventoryClient.cs`, `ProductInventoryContracts.cs`, `ProductServiceOptions.cs`, `Program.cs`, and `Features/Orders/OrderEndpoints.cs`.
- Tests: `ProductInventoryClientTests` verifies internal reserve/release routes, authoritative response parsing, `traceparent`, business Problem Details, unavailable dependency, timeout ambiguity, malformed response, and caller cancellation.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`; `git diff --check`.
- Commit: `e3c2b7c` (`feat(order): integrate product inventory client`).
- Notes: The client sets `HttpClient.Timeout` to infinite and applies the configured linked timeout so the ambiguity boundary is explicit; no automatic retry is configured.

## 3.5 Real Order orchestration

- [x] Generate order ID before remote call.
- [x] Persist `pending_inventory`.
- [x] Call Product reservation.
- [x] Store authoritative Product snapshots.
- [x] Calculate Order total from snapshots.
- [x] Transition to `confirmed`.
- [x] Transition known Product failures to `rejected`.
- [x] Transition ambiguous failures to `inventory_unknown`.
- [x] Add state-history entries.
- [x] Return stable public errors with order ID where appropriate.

Evidence for 3.5:

- Files: `Features/Orders/OrderApplicationService.cs`, `OrderEndpoints.cs`, `Infrastructure/Products/FakeProductCatalogClient.cs`, and `tests/MicroShop.OrderService.Tests/OrderOrchestrationTests.cs` plus `OrderProductHttpIntegrationTests.cs`.
- Tests: Order orchestration verifies pending-before-call, stable order identity, Product-authoritative snapshots/totals, known rejection, unavailable dependency, timeout ambiguity, malformed/mismatched response, trace forwarding, and persisted state history.
- Commands: `dotnet test MicroShop.sln --configuration Release --no-restore`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`.
- Commit: `e3c2b7c` (`feat(order): integrate product inventory client`).
- Notes: A reservation response with inconsistent snapshots is treated as `INVENTORY_OUTCOME_UNKNOWN`; an over-limit reservation is released before a normal total-limit rejection, with release ambiguity also becoming unknown.

## 3.6 Cancellation

- [x] Implement `POST /api/v1/orders/{id}/cancel`.
- [x] Allow only from `confirmed`.
- [x] Call Product release.
- [x] Mark `cancelled` only after known release.
- [x] Mark `cancellation_pending` for ambiguous release.
- [x] Ensure repeated cancellation never restores stock twice.
- [x] Return `canCancel` from Order responses.

Evidence for 3.6:

- Files: `Features/Orders/OrderApplicationService.cs`, `OrderEndpoints.cs`, `Infrastructure/Products/ProductInventoryClient.cs`, and `tests/MicroShop.OrderService.Tests/OrderApiTests.cs` plus `OrderProductHttpIntegrationTests.cs`.
- Tests: cancellation succeeds only after Product release, repeated cancellation returns the cancelled order without another release, unavailable release persists `cancellation_pending`, and a second attempt does not blindly call release again.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-restore`; `git diff --check`.
- Commit: `27d57ef` (`feat(order): add reservation cancellation`).
- Notes: `confirmed -> cancellation_pending` is committed before the remote call, so concurrent/repeated requests cannot issue duplicate release commands. A known release moves to `cancelled`; dependency/ambiguous outcomes remain pending for reconciliation.

## 3.7 Synchronous communication tests

- [x] Test successful reservation.
- [x] Test insufficient stock.
- [x] Test Product not found.
- [x] Test inactive Product.
- [x] Test reservation replay.
- [x] Test reservation mismatch.
- [x] Test idempotent release.
- [x] Test concurrent last-stock purchase.
- [x] Test Product Service unavailable.
- [x] Test timeout with ambiguous outcome.
- [x] Test cancellation.
- [x] Test repeated cancellation.
- [x] Test `cancellation_pending`.
- [x] Verify no partial stock decrement.
- [x] Verify stock never becomes negative.

Evidence for 3.7:

- Files: `tests/MicroShop.ProductService.Tests/InventoryApiTests.cs`, `tests/MicroShop.OrderService.Tests/ProductInventoryClientTests.cs`, `OrderOrchestrationTests.cs`, `OrderProductHttpIntegrationTests.cs`, and `tests/MicroShop.Gateway.Tests/GatewayApiTests.cs`.
- Tests: 21 Product tests, 40 Order tests, and 6 Gateway tests pass, including Product atomic reservation/release, known failures, replay/mismatch, reservation lookup, concurrent last-stock, typed HTTP response/error mapping, unavailable dependency, timeout ambiguity, cancellation propagation, cancellation release/idempotency, `cancellation_pending`, Order unknown-state persistence/concurrency, direct event publication, Gateway internal-route exclusion, and startup destination validation.
- Commands: `dotnet test MicroShop.sln --configuration Release --no-restore`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`.
- Commits: `d2a885a` (`feat(product): add atomic inventory reservations`), `8b021f5` (`docs(project): record inventory reservation boundary`), `e3c2b7c` (`feat(order): integrate product inventory client`), `27d57ef` (`feat(order): add reservation cancellation`), and `569af30` (`feat(gateway): add public yarp routes`).
- Notes: Product reservation, Order synchronous communication, cancellation, and internal-route exclusion are green.

## 3.8 Phase 3 validation gate

- [x] Order Service uses real Product HTTP communication.
- [x] Product and Order databases remain isolated.
- [x] All concurrency tests pass using PostgreSQL.
- [x] All failure states match documentation.
- [x] Cancellation restores stock exactly once.
- [x] Internal Product API is not public.

Evidence for 3.8:

- Files: Product/Order database configuration and migrations, the typed Product client, cancellation orchestration, and Gateway route/test files.
+ Tests: Full solution validation passes with 83 .NET tests: 1 Architecture, 2 Contracts, 7 Gateway, 12 Notification, 40 Order, and 21 Product.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Commit: `569af30` (`feat(gateway): add public yarp routes`) completes the final Gateway safety item for this gate.

---

# Phase 4 — YARP API Gateway

## 4.1 Gateway foundation

- [x] Configure YARP.
- [x] Add Product cluster.
- [x] Add Order cluster.
- [x] Add Notification cluster placeholder or deferred route.
- [x] Add Product public routes.
- [x] Add Order public routes.
- [x] Configure path transforms.
- [x] Configure CORS.
- [x] Configure body/request limits.
- [x] Add liveness.
- [x] Add readiness.
- [x] Add structured logging.
- [x] Propagate trace headers.

Evidence for 4.1:

- Files: `src/Gateway/MicroShop.Gateway/BootstrapConfiguration.cs`, `Program.cs`, `appsettings.json`, and `tests/MicroShop.Gateway.Tests/GatewayApiTests.cs`.
- Tests: Gateway route, path transform, trace propagation, health, CORS, downstream failure, and internal-route rejection tests pass.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Commit: `569af30` (`feat(gateway): add public yarp routes`).
- Notes: Product and Order destinations are environment-overridable and validated as absolute HTTP(S) addresses. Notification has a configuration-only cluster placeholder; no Notification route is exposed before its API exists.

## 4.2 Gateway safety

- [x] Ensure `/internal/*` is never routed.
- [x] Ensure browser receives no internal hostname.
- [x] Ensure service-native ports are not required by Angular.
- [x] Handle downstream unavailable as gateway error.
- [x] Validate configured clusters at startup.

Evidence for 4.2 (partial):

- Files: `BootstrapConfiguration.cs`, `Program.cs`, `appsettings.json`, and `GatewayApiTests.cs`.
- Tests: Internal Product paths return `404 GATEWAY_ROUTE_NOT_FOUND` without forwarding; unavailable destinations return `502 DOWNSTREAM_UNAVAILABLE`; invalid Product/Order destination schemes fail startup configuration.
+ Commands: `dotnet test MicroShop.sln --configuration Release --no-restore` (83 tests pass).
- Commit: `569af30` (`feat(gateway): add public yarp routes`).
- Notes: Angular feature clients and screens use same-origin Gateway paths; service-native ports are not referenced by browser code. Full application-container port isolation remains a Phase 6 Compose validation item.

## 4.3 Angular migration to Gateway

- [x] Replace direct Product API URL with `/api/products`.
- [x] Replace direct Order API URL with `/api/orders`.
- [x] Use same-origin API requests.
- [x] Remove internal service URLs from Angular configuration.
- [x] Add Gateway connectivity error handling.

Evidence for 4.3:

- Files: `web/microshop-ui/src/app/core/api/api.paths.ts`, `product-api.service.ts`, `order-api.service.ts`, `gateway-error.ts`, `gateway-error.interceptor.ts`, `app.config.ts`, and `gateway-api.spec.ts`.
- Tests: Product listing, Order create/cancel, Notification listing/mark-as-read, same-origin paths, and `502 DOWNSTREAM_UNAVAILABLE` mapping are covered by the Gateway API tests; the full Angular suite has 21 passing tests.
- Commands: `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`; `rg -n -i "product-service|order-service|notification-service|internal/v1|api/v1" web/microshop-ui/src` (no service/internal API matches).
- Commits: `f750963` (`feat(ui): route api clients through gateway`), `185a8cc` (`feat(ui): add product catalog management`), and `20bf3d8` (`feat(ui): add order checkout and history`).
- Notes: The workspace had no previous feature API clients or service URL configuration, so the migration creates the canonical relative client boundary for the upcoming Product and Order screens. External Angular documentation links in the generated placeholder are unrelated to service routing.

## 4.4 Gateway tests

- [x] Test Product route.
- [x] Test Order route.
- [x] Test path transforms.
- [x] Test trace-header propagation.
- [x] Test downstream unavailable behavior.
- [x] Test internal route rejection.
- [x] Test Gateway health.

Evidence for 4.4:

- Files: `tests/MicroShop.Gateway.Tests/GatewayApiTests.cs` and `tests/MicroShop.Gateway.Tests/MicroShop.Gateway.Tests.csproj`.
- Tests: 6 Gateway integration tests pass using a real in-process Kestrel downstream and `WebApplicationFactory`.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet test tests/MicroShop.Gateway.Tests/MicroShop.Gateway.Tests.csproj --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Commit: `569af30` (`feat(gateway): add public yarp routes`).

## 4.5 Phase 4 validation gate

- [x] Angular works using only Gateway.
- [x] Product and Order services are hidden from normal browser use.
- [x] Internal inventory API cannot be reached through Gateway.
- [x] Gateway tests pass.

Evidence for 4.5:

- Files: Gateway route configuration and `tests/MicroShop.Gateway.Tests/GatewayApiTests.cs`.
- Tests: 7 Gateway tests, 21 Angular tests, and the full 83-test .NET solution pass. Angular source scans contain no service-native, internal, or versioned service API URLs.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Commit: `569af30` (`feat(gateway): add public yarp routes`).
- Notes: Public routes, internal-route exclusion, Angular same-origin API clients, Product/Order feature screens, and full-stack private application-port isolation are complete.

---

# Phase 5 — RabbitMQ and Notification Service

## 5.1 Shared event contracts

- [x] Create `MicroShop.Contracts`.
- [x] Add `OrderConfirmedV1`.
- [x] Add message ID.
- [x] Add order ID.
- [x] Add customer destination fields.
- [x] Add total and currency.
- [x] Add item snapshots.
- [x] Add occurred-at UTC.
- [x] Add schema version.
- [x] Keep contracts free from EF entities and business logic.
- [x] Add serialization compatibility test.

Evidence for 5.1:

- Files: `src/BuildingBlocks/MicroShop.Contracts/Orders/OrderConfirmedV1.cs`, `src/Services/OrderService/MicroShop.OrderService/MicroShop.OrderService.csproj`, `src/Services/NotificationService/MicroShop.NotificationService/MicroShop.NotificationService.csproj`, and `tests/MicroShop.Contracts.Tests/OrderConfirmedV1SerializationTests.cs`.
+ Tests: 2 contract tests pass for stable camelCase JSON serialization and documented Version 1 fixture deserialization; the full .NET solution has 83 passing tests.
- Commands: `dotnet restore MicroShop.sln`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.Contracts.Tests/MicroShop.Contracts.Tests.csproj --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Commit: `2c99721` (`feat(contracts): add order confirmed event`).
- Notes: The assembly contains passive immutable records only; it references no EF Core project, database entity, or service business logic. Order and Notification reference the contract assembly directly.

## 5.2 RabbitMQ and MassTransit

- [x] Add RabbitMQ container.
- [x] Add development management UI.
- [x] Configure MassTransit producer.
- [x] Configure durable Notification receive endpoint.
- [x] Configure bounded retry.
- [x] Configure error queue behavior.
- [x] Add RabbitMQ readiness checks.
- [x] Ensure credentials come from environment.

Evidence for 5.2:

- Files: `deploy/compose.yaml`, `.env.example`, `Directory.Packages.props`, `src/BuildingBlocks/MicroShop.ServiceDefaults/Messaging/RabbitMqOptions.cs`, both service `Program.cs` files, and `tests/MicroShop.NotificationService.Tests/NotificationBootstrapTests.cs`.
+ Tests: Notification bootstrap health and API tests pass with owned PostgreSQL Testcontainers; the existing Order readiness test passes with the same isolated test transport; the full solution has 83 passing .NET tests. Compose reports healthy PostgreSQL and RabbitMQ containers with the management UI on port 15672.
- Commands: `dotnet restore MicroShop.sln`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.NotificationService.Tests/MicroShop.NotificationService.Tests.csproj --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`; `docker compose --env-file .env.example -f deploy/compose.yaml config --quiet`; `docker compose --env-file .env.example -f deploy/compose.yaml up -d`; `docker compose --env-file .env.example -f deploy/compose.yaml ps`.
- Commit: `6fabfd5` (`feat(messaging): configure RabbitMQ transport`).
+ Notes: Runtime uses MassTransit RabbitMQ 8.5.10, a durable `microshop-notification-order-confirmed-v1` endpoint, three 250ms retry attempts, and the framework error-queue convention. PostgreSQL-backed Order tests continue to use isolated in-memory transport, while the Notification messaging integration suite uses a disposable RabbitMQ Testcontainer. A native full-stack smoke is deferred until application containers exist because AMQP port 5672 is intentionally private in the current infrastructure Compose.

## 5.3 Direct publish learning milestone

- [x] Publish `OrderConfirmedV1` after Order confirmation.
- [x] Preserve event message ID.
- [x] Propagate trace context.
- [x] Document database/broker dual-write gap.
- [x] Add test demonstrating direct-publish failure window.

Evidence for 5.3:

- Files: `src/Services/OrderService/MicroShop.OrderService/Infrastructure/Messaging/OrderConfirmedPublisher.cs`, `Features/Orders/OrderApplicationService.cs`, `Program.cs`, and `tests/MicroShop.OrderService.Tests/OrderOrchestrationTests.cs`.
- Tests: 40 Order tests pass, including confirmed-event construction with stable order/message IDs and trace parent propagation, plus a direct-publish failure test proving the confirmed Order remains persisted after the broker publish step throws.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Commit: `96d5183` (`feat(order): publish confirmed events`).
- Notes: This historical milestone committed `confirmed` before calling MassTransit `IPublishEndpoint`. The envelope `MessageId` was explicitly set to the event body's `MessageId`, `OrderId` was the correlation ID, and a W3C `traceparent` header was copied when present. It remains as the direct-publish learning demonstration; Phase 7.1 now closes the post-commit publish gap in the runtime path.

## 5.4 Notification database

- [x] Create Notification DbContext.
- [x] Define ConsumedMessage entity.
- [x] Define Notification entity.
- [x] Add unique consumed message constraint.
- [x] Add unique source message constraint.
- [x] Add query indexes.
- [x] Create migration.
- [x] Add Notification database health check.

Evidence for 5.4:

- Files: `src/Services/NotificationService/MicroShop.NotificationService/Persistence/NotificationDbContext.cs`, `Persistence/Entities/ConsumedMessage.cs`, `Persistence/Entities/Notification.cs`, `Persistence/Configurations/`, `Persistence/Migrations/20260817185808_InitialNotificationSchema.cs`, `Infrastructure/Database/NotificationDatabaseOptions.cs`, and `Program.cs`.
+ Tests: 12 Notification tests pass against real PostgreSQL 17 and RabbitMQ Testcontainers; the bootstrap test verifies `/health/ready` against the migrated owned database, API tests verify filters/pagination/OpenAPI/mark-as-read, and the messaging tests verify publish/consume and recovery. The full solution has 83 passing .NET tests.
- Commands: `dotnet restore MicroShop.sln`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.NotificationService.Tests/MicroShop.NotificationService.Tests.csproj --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-build --no-restore`.
- Commit: `e6f19a6` (`feat(notification): persist confirmed order events`).
- Notes: Notification owns `consumed_messages` and `notifications`; unique `message_id`/`source_message_id` constraints and customer/order indexes are local to that database. No Product or Order EF project, DbContext, connection string, table, or migration is referenced.

## 5.5 Notification consumer

- [x] Implement `OrderConfirmedV1` consumer.
- [x] Persist consumed message ID and Notification in one transaction.
- [x] Suppress duplicate delivery.
- [x] Generate readable simulated notification.
- [x] Preserve order ID and customer email.
- [x] Log duplicate suppression.
- [x] Let failures throw for retry/error handling.
- [x] Ensure Notification Service does not query Product/Order databases.

Evidence for 5.5:

- Files: `src/Services/NotificationService/MicroShop.NotificationService/Features/Messaging/OrderConfirmedConsumer.cs`, `Persistence/Entities/Notification.cs`, `Persistence/Entities/ConsumedMessage.cs`, and `Program.cs`.
- Tests: PostgreSQL-backed tests cover one-transaction persistence/readback, readable body and normalized customer email, sequential duplicate suppression, unsupported schema rejection without side effects, trace ID storage, read filters/pagination, mark-as-read idempotency, OpenAPI, and validation/not-found responses. Non-duplicate failures are not swallowed; only a unique-key race is treated as an idempotent duplicate.
- Commands: `dotnet test tests/MicroShop.NotificationService.Tests/MicroShop.NotificationService.Tests.csproj --configuration Release --no-build --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-build --no-restore`.
- Commit: `e6f19a6` (`feat(notification): persist confirmed order events`).
- Notes: MassTransit invokes the consumer; the handler consumes only the self-contained `OrderConfirmedV1` snapshot and writes Notification-owned data. It does not call Product Service, Order Service, or another database.

## 5.6 Notification read API

- [x] Implement `GET /api/v1/notifications`.
- [x] Support customer email filter.
- [x] Support order ID filter.
- [x] Support pagination.
- [x] Optionally implement mark-as-read.
- [x] Add OpenAPI.
- [x] Add Gateway Notification route.

Evidence for 5.6:

- Files: `src/Services/NotificationService/MicroShop.NotificationService/Features/Notifications/NotificationContracts.cs`, `NotificationEndpoints.cs`, `Program.cs`, `src/Gateway/MicroShop.Gateway/appsettings.json`, `BootstrapConfiguration.cs`, and the Notification/Gateway test files.
+ Tests: 12 Notification tests pass, including email/order filters, stable descending pagination, OpenAPI discovery, validation, not-found handling, idempotent mark-as-read, real RabbitMQ publish/consume, retry/error queue, duplicate delivery, restart, and queued recovery; 7 Gateway tests pass, including `/api/notifications` to `/api/v1/notifications` path/query transformation. The full solution has 83 passing .NET tests.
- Commands: `dotnet restore MicroShop.sln`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.NotificationService.Tests/MicroShop.NotificationService.Tests.csproj --configuration Release --no-build --no-restore`; `dotnet test tests/MicroShop.Gateway.Tests/MicroShop.Gateway.Tests.csproj --configuration Release --no-build --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-build --no-restore`.
- Commit: `569027d` (`feat(notification): add read api and gateway route`).
- Notes: The Gateway proxies only the public Notification HTTP path; it does not reference Notification EF types or database configuration. The Angular Notification screen consumes the public path through the same-origin Gateway client.

## 5.7 Angular Notification UI

- [x] Create Notification route.
- [x] List notifications.
- [x] Add manual refresh.
- [x] Add bounded polling.
- [x] Handle eventual consistency.
- [x] Handle empty/error/loading states.
- [x] Optionally mark notification read.

Evidence for 5.7:

- Files: `web/microshop-ui/src/app/core/api/notification-api.service.ts`, `api.models.ts`, `api.paths.ts`, `features/notifications/notification-list.component.ts`, `.html`, `.scss`, `.spec.ts`, `app.routes.ts`, and `app.html`.
- Tests: Angular lint passes; 21 Angular tests pass, including same-origin Notification client paths, list rendering, empty/error/loading states, mark-as-read interaction, and bounded polling that stops after five checks. Production Angular build passes.
- Commands: `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`.
- Commit: `e36b1f5` (`feat(ui): add notification screen`).
- Notes: The page explains eventual consistency, performs one immediate read plus four bounded automatic checks at 15-second intervals, supports manual refresh after the bound, and never exposes service-native URLs to the browser.

## 5.8 Messaging tests

- [x] Publish/consume integration test.
- [x] Duplicate event test.
- [x] Consumer retry test.
- [x] Error queue test.
- [x] Notification Service restart test.
- [x] Durable queued-message recovery test.
- [x] Verify Order confirmation does not wait for Notification consumer.
- [x] Verify Notification Service uses only its own database.

Evidence for 5.8:

- Files: `tests/MicroShop.NotificationService.Tests/RabbitMqMessagingFixture.cs`, `NotificationMessagingIntegrationTests.cs`, `NotificationConsumerTests.cs`, and `MicroShop.NotificationService.Tests.csproj`; central package/lock files pin `Testcontainers.RabbitMq` 4.13.0 and `RabbitMQ.Client` 7.2.1.
- Tests: 5 RabbitMQ-backed integration tests pass: publish/consume, duplicate delivery, bounded retry/error queue, queued recovery after Notification host restart, and publisher completion while the consumer is stopped. The existing PostgreSQL consumer tests still verify ownership and transactional/idempotent persistence; the full solution has 83 passing .NET tests.
- Commands: `dotnet restore MicroShop.sln --locked-mode`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test tests/MicroShop.NotificationService.Tests/MicroShop.NotificationService.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~NotificationMessagingIntegrationTests`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Notes: The test host runs the actual Notification `Program` against the durable `microshop-notification-order-confirmed-v1` endpoint. The RabbitMQ container is disposable because infrastructure Compose keeps AMQP 5672 private; no shared Docker volume is removed.

## 5.9 Phase 5 validation gate

- [x] Confirmed Order emits event.
- [x] Notification Service consumes event.
- [x] Duplicate event creates one Notification.
- [x] Stopping Notification Service does not block Order confirmation.
- [x] Restarting Notification Service drains queued messages.
- [~] Angular displays eventual Notification.

Evidence for 5.9:

- Files: `src/Services/OrderService/MicroShop.OrderService/Infrastructure/Messaging/OrderConfirmedPublisher.cs`, `src/Services/NotificationService/MicroShop.NotificationService/Program.cs`, and `tests/MicroShop.NotificationService.Tests/NotificationMessagingIntegrationTests.cs`.
- Tests: direct Order publication and failure-window tests, plus the 5 RabbitMQ-backed Notification integration tests, pass. Angular Notification list behavior is unit-tested with bounded polling; full browser-to-Compose eventual delivery remains deferred to Phase 6/8.
---

# Phase 6 — Docker Compose Completion

## 6.1 Container images

- [x] Add multi-stage Product Service Dockerfile.
- [x] Add multi-stage Order Service Dockerfile.
- [x] Add multi-stage Notification Service Dockerfile.
- [x] Add multi-stage Gateway Dockerfile.
- [x] Add Angular build/nginx Dockerfile.
- [x] Use runtime-only images.
- [x] Use non-root runtime users where practical.
- [x] Add image metadata/version labels if useful.

Evidence for 6.1:

- Files: `.dockerignore`, `deploy/docker/product-service.Dockerfile`, `deploy/docker/order-service.Dockerfile`, `deploy/docker/notification-service.Dockerfile`, `deploy/docker/gateway.Dockerfile`, `deploy/docker/web.Dockerfile`, and `deploy/docker/nginx.conf`.
- Commands: five `docker build --file deploy/docker/... --tag microshop-*:phase6 .` commands; `docker image inspect`; a temporary `microshop-web:phase6` container health check at `/health`.
- Result: all five images build successfully from locked .NET restores or `npm ci`; .NET images expose only port 8080 and run as UID 1654, while the Nginx image runs as its unprivileged image user and serves the Angular build.
- Notes: image build output reports the known Angular development-tool advisories; no production dependency vulnerability was introduced. Full-stack Compose wiring is verified in 6.2.

## 6.2 Compose stack

- [x] Add `deploy/compose.yaml`.
- [x] Add Web.
- [x] Add Gateway.
- [x] Add Product Service.
- [x] Add Order Service.
- [x] Add Notification Service.
- [x] Add PostgreSQL.
- [x] Create separate Product database and user.
- [x] Create separate Order database and user.
- [x] Create separate Notification database and user.
- [x] Add RabbitMQ.
- [x] Add persistent PostgreSQL volume.
- [x] Add persistent RabbitMQ volume.
- [x] Add internal application network.
- [x] Publish only required ports.
- [x] Keep database and RabbitMQ application ports private by default.
- [x] Add development override for debugging ports.

Evidence for 6.2:

- Files: `deploy/compose.yaml`, `deploy/compose.override.yaml`, `.env.example`, and `deploy/docker/nginx.conf`.
- Commands: `docker compose --env-file .env.example --file deploy/compose.yaml config`; `docker compose --env-file .env.example --file deploy/compose.yaml up --build -d`; `docker compose --env-file .env.example --file deploy/compose.yaml ps --all`.
- Tests: all five application services, three migration one-shots, PostgreSQL, RabbitMQ, Gateway, and Web reached the expected state; only Web port 8080 and RabbitMQ management port 15672 are public in the base file. Gateway `/health` and Web `/health` are healthy, while application API ports remain private.
- Full-stack smoke: `GET /api/products`, `GET /api/notifications`, confirmed `POST /api/orders`, and eventual Notification read all passed through Web/Nginx and Gateway. Existing Compose volumes were preserved.

## 6.3 Migration and startup

- [x] Add explicit Product migration command.
- [x] Add explicit Order migration command.
- [x] Add explicit Notification migration command.
- [x] Add migrate-all script.
- [x] Add safe local reset script requiring explicit confirmation/permission.
- [x] Ensure services do not race migrations.
- [x] Add health-based startup dependencies where supported.
- [x] Validate configuration at startup.

Evidence for the implemented 6.3 items:

- Files: Product `Program.cs` now supports `--migrate`; Order and Notification already expose `--migrate`; Compose contains `migrate-product`, `migrate-order`, and `migrate-notification` one-shot services with `service_completed_successfully` dependencies.
- Tests: all three migration containers exited with code 0 before their application services started; each service reached a healthy readiness endpoint against its owned database.
- Evidence for the remaining wrappers: `scripts/db-migrate-all.ps1/.sh` ran all three Compose migration services successfully; `scripts/db-reset-local.ps1/.sh` require explicit reset and volume-deletion flags plus the exact confirmation string. The PowerShell refusal path was executed without flags and ran no Docker command; POSIX scripts pass `bash -n` inside the Linux ASP.NET container, while native WSL bash is unavailable in this Windows environment.

## 6.4 One-command local run

- [x] Document full-stack start command.
- [x] Document stop command.
- [x] Document logs command.
- [x] Document status command.
- [x] Document seed command.
- [x] Document migration command.
- [x] Verify Windows PowerShell workflow.
- [~] Verify Linux/macOS workflow where practical.

Evidence for 6.4:

- Files: `README.md`, `docs/project/07_DEVELOPMENT_AND_TESTING.md`, and `docs/project/08_DEPLOYMENT_AND_OPERATIONS.md`.
- Commands: `docker compose --env-file .env.example --file deploy/compose.yaml up --build -d`; `... ps --all`; `... logs`; `... run --rm --no-deps product-service dotnet MicroShop.ProductService.dll --seed`; and `... down` without volume removal.
- Result: the Windows PowerShell workflow starts the full stack, seeds Product idempotently, exposes Web on 8080, and leaves PostgreSQL/RabbitMQ volumes intact. POSIX commands are documented with the same Compose file and service names but were not executed on Linux/macOS in this Windows run.

## 6.5 Phase 6 validation gate

- [x] Full stack starts with one documented Compose command.
- [x] Angular is reachable.
- [x] Gateway routes all public APIs.
- [x] Product/Order/Notification databases are separate.
- [x] RabbitMQ management shows expected topology.
- [x] End-to-end Order flow works in Compose.
- [x] Internal service ports are not unnecessarily public.

Evidence for 6.5:

- Files: `deploy/compose.yaml`, `deploy/postgres-init/001-create-service-databases.sh`, and `deploy/docker/nginx.conf`.
- Tests: Compose `ps --all` shows three successful migration one-shots and healthy application services; Web `/api/products`, `/api/orders`, and `/api/notifications` routes pass; a seeded Order reaches `confirmed` and its Notification is readable through Web; only Web 8080 and RabbitMQ management 15672 are published.
- Notes: the Angular UI behavior is covered by its unit suite; browser Playwright coverage remains a Phase 8 task.

---

# Phase 7 — Reliability Hardening

## 7.1 Transactional outbox

- [x] Add OutboxMessage entity.
- [x] Add Outbox migration.
- [x] Insert `OrderConfirmedV1` outbox record in Order confirmation transaction.
- [x] Stop using direct broker publish as final path.
- [x] Preserve the direct-publish demonstration in documentation/tests only.
- [x] Implement outbox dispatcher.
- [x] Claim pending records safely.
- [x] Add attempt count.
- [x] Add next-attempt time.
- [x] Add last-error field.
- [x] Add lock/lease field.
- [x] Mark successful publish.
- [x] Make dispatcher restart-safe.
- [x] Prevent infinite retry loop.

Evidence for 7.1:

- Files: `src/Services/OrderService/MicroShop.OrderService/Persistence/Entities/OutboxMessage.cs`, `Persistence/Configurations/OutboxMessageConfiguration.cs`, `Persistence/Migrations/20260817203650_AddOrderOutbox.cs`, `Infrastructure/Messaging/OrderConfirmedMessageFactory.cs`, `OrderOutboxWriter.cs`, `OrderConfirmedTransport.cs`, `OutboxOptions.cs`, `OutboxDispatcher.cs`, `Features/Orders/OrderApplicationService.cs`, `Program.cs`, and `tests/MicroShop.OrderService.Tests/OutboxDispatcherTests.cs`.
- Database: `outbox_messages` is Order-owned, stores the stable event MessageId and serialized `OrderConfirmedV1`, has unique `(message_type, aggregate_id)`, pending indexes, attempt/error fields, lease fields, published/dead-letter timestamps, and no cross-service foreign key.
- Tests: 44 Order tests pass against PostgreSQL Testcontainers. Coverage includes atomic confirmed Order plus outbox persistence, direct-publish failure isolation, stable MessageId/traceparent payload, `FOR UPDATE SKIP LOCKED` concurrent claim, bounded retry/dead-letter, and lease-expiry recovery after dispatcher interruption.
- Commands: `dotnet ef migrations add AddOrderOutbox --project src/Services/OrderService/MicroShop.OrderService --startup-project src/Services/OrderService/MicroShop.OrderService --output-dir Persistence/Migrations`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-restore`; `dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-build --no-restore`.
- Notes: production DI no longer registers `IOrderEventPublisher`; `MassTransitOrderEventPublisher` remains only as the explicit direct-publish learning/demo path. The dispatcher uses a lease and bounded attempts, marks exhausted rows dead-lettered, and can reclaim an expired lease after process restart. Outbox operations/readiness/metrics, RabbitMQ outage Compose evidence, reconciliation, and the Phase 7 gate remain in later 7.2-7.6 tasks.

## 7.2 Outbox operations

- [x] Add outbox backlog logs.
- [x] Add outbox readiness/health policy.
- [x] Add outbox metrics if metrics exist.
- [x] Add operator query or documented SQL for pending outbox.
- [x] Add recovery procedure.
- [x] Add RabbitMQ outage test.
- [x] Verify Order confirmation remains durable when RabbitMQ is unavailable.
- [x] Verify backlog drains after RabbitMQ recovers.

Evidence for 7.2:

- Files: `Infrastructure/Messaging/OutboxBacklog.cs`, `OutboxDispatcher.cs`, `OutboxOptions.cs`, `Program.cs`, `scripts/db-outbox-status.ps1`, `scripts/db-outbox-status.sh`, `scripts/README.md`, `deploy/compose.yaml`, and `tests/MicroShop.OrderService.Tests/OrderOutboxRabbitMqTests.cs`.
- Operations: the dispatcher emits structured backlog summaries with pending count, oldest age, and dead-letter count; `/health/ready` includes an Order outbox policy check. Configurable thresholds are `ORDER_OUTBOX_MAX_PENDING_MESSAGES`, `ORDER_OUTBOX_MAX_PENDING_AGE_MS`, `ORDER_OUTBOX_BACKLOG_LOG_INTERVAL_MS`, and `ORDER_OUTBOX_FAIL_READINESS_ON_DEAD_LETTERED`. A pending backlog within policy remains ready during a RabbitMQ outage because confirmation durability is provided by the Order database outbox; excessive backlog or configured dead-letter policy returns unhealthy.
- Metrics: no metrics provider or endpoint exists in the current baseline, so no separate metric was added outside the roadmap. Structured logs and health data are the operational signals for this slice; Phase 8.3 owns the future metrics surface.
- Operator query/recovery: the read-only status wrappers show aggregate backlog/dead-letter state and the first 20 actionable rows. The runbook is documented in `docs/project/08_DEPLOYMENT_AND_OPERATIONS.md`; it never deletes or edits outbox records.
- Test: `OrderOutboxRabbitMqTests.RabbitMqOutageLeavesConfirmedOrderDurableAndRecoveryDrainsOutbox` uses RabbitMQ and PostgreSQL Testcontainers, stops RabbitMQ before confirmation, verifies `confirmed` plus an un-published outbox row and HTTP readiness, restarts the dispatcher after lease expiry, and verifies the row is published with a retry attempt.
- Commands: `dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~OrderOutboxRabbitMqTests`; `./scripts/db-outbox-status.ps1 -EnvFile .env.example`; `docker compose --env-file .env.example -f deploy/compose.yaml config`.

## 7.3 Inbox/idempotency hardening

- [x] Ensure consumed-message insert and Notification creation share one transaction.
- [x] Test duplicate redelivery under concurrency.
- [x] Test consumer process restart between attempts.
- [x] Verify unique constraints prevent duplicate side effects.
- [x] Add cleanup/retention policy only if justified.

Evidence for 7.3:

- Files: `src/Services/NotificationService/MicroShop.NotificationService/Features/Messaging/OrderConfirmedConsumer.cs`, `Infrastructure/Messaging/NotificationMessagingOptions.cs`, `Program.cs`, `tests/MicroShop.NotificationService.Tests/NotificationConsumerTests.cs`, and `tests/MicroShop.NotificationService.Tests/NotificationMessagingIntegrationTests.cs`.
- Transaction: `OrderConfirmedNotificationHandler` starts one Notification database transaction, adds `ConsumedMessage` plus its generated `Notification`, commits together, and rolls back a concurrent unique-key loser. A deliberately oversized customer email test proves a failed notification write leaves no consumed-message row.
- Idempotency: eight independent PostgreSQL contexts deliver the same MessageId concurrently; the `consumed_messages` primary key and `notifications.source_message_id` unique index leave exactly one inbox row and one notification. The RabbitMQ integration test publishes, restarts the Notification host, redelivers the same MessageId, and verifies one durable side effect.
- Restart/retry configuration: Notification retry count and delay are bounded and configurable with `NOTIFICATION_CONSUMER_RETRY_COUNT` and `NOTIFICATION_CONSUMER_RETRY_DELAY_MS`; default is three attempts with 250 ms intervals.
- Retention: no cleanup job is justified for the small learning/demo database. Notification and consumed-message history remain retained according to the existing demo database policy; any production personal-data retention remains a future deployment concern.
- Tests: `dotnet test tests/MicroShop.NotificationService.Tests/MicroShop.NotificationService.Tests.csproj --configuration Release --no-restore` passes with 15 tests, including concurrent duplicate, transaction rollback, RabbitMQ duplicate delivery, error-queue retry, queued restart, and process restart between redelivery attempts.

## 7.4 Reconciliation

- [x] Add internal/manual reconciliation path for `inventory_unknown`.
- [x] Query Product reservation by order ID.
- [x] Reconcile known existing reservation to confirmed Order.
- [x] Reconcile known absent reservation safely.
- [x] Add reconciliation audit/history.
- [x] Add reconciliation tests.
- [x] Add `cancellation_pending` reconciliation.
- [x] Document runbook.

Evidence for 7.4:

- Files: `Features/Reconciliation/OrderReconciliationService.cs`, `Features/Reconciliation/ReconciliationEndpoints.cs`, `Infrastructure/Products/ProductInventoryClient.cs`, `Persistence/Entities/OrderInventoryRequestItem.cs`, `Persistence/Entities/OrderReconciliationAudit.cs`, `Persistence/Migrations/20260818044623_AddOrderReconciliation.cs`, and `docs/project/08_DEPLOYMENT_AND_OPERATIONS.md`.
- Behavior: `POST /internal/v1/reconciliation/orders/{orderId}` is an Order-native manual path and is not mapped by the Gateway. It queries Product's order-keyed reservation state, matches the persisted inventory intent against Product-authoritative snapshots before confirming, rejects a known absent/released reservation safely, records every outcome in `order_reconciliation_audits`, and handles both `inventory_unknown` and `cancellation_pending` without blind repeated compensation.
- Tests: `ProductInventoryClientTests` covers lookup request/response/error mapping; `OrderReconciliationTests` covers matching reservation confirmation plus outbox, absent reservation rejection, cancellation with absent reservation, cancellation after release, dependency-unavailable pending state, and mismatch conflict; `OrderApiTests.InternalReconciliationRouteRejectsUnknownOrderWhenReservationIsAbsent` covers the internal HTTP route. The Order suite has 60 passing tests after adding the resilience cases.
- Commands: `dotnet test tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-restore`; `dotnet build tests/MicroShop.OrderService.Tests/MicroShop.OrderService.Tests.csproj --configuration Release --no-restore`; `git diff --check`.
- Notes: The migration adds only Order-owned request-intent and audit tables with local foreign keys. Confirming through reconciliation writes the Order, audit, and transactional outbox in the same save; dependency or snapshot conflicts leave the ambiguous state intact for a later controlled retry.

## 7.5 Resilience policies

- [x] Review Product timeout.
- [x] Add bounded retry only where idempotency makes it safe.
- [x] Add circuit breaker only if justified.
- [x] Add graceful shutdown for services.
- [x] Add bounded consumer shutdown.
- [x] Add cancellation token propagation.
- [x] Add readiness transitions during shutdown.

Evidence for 7.5:

- Files: `src/BuildingBlocks/MicroShop.ServiceDefaults/ServiceDefaultsExtensions.cs`, Order `Infrastructure/Products/ProductInventoryClient.cs`, `ProductServiceOptions.cs`, Order/Notification `Program.cs`, `.env.example`, `deploy/compose.yaml`, `tests/MicroShop.OrderService.Tests/ProductInventoryClientTests.cs`, and `tests/MicroShop.Gateway.Tests/ServiceReadinessTests.cs`.
- Timeout/retry policy: Product timeout remains validated at 1–5000 ms. Reserve is never automatically retried because a lost response can represent a committed stock change; only the order-keyed idempotent release and read-only reservation lookup retry transient transport/408/429/5xx failures within the same linked timeout. Retry count is bounded to 0–3 and delay to 10–2000 ms (defaults: 1 and 100 ms). No circuit breaker is added because the baseline already has bounded dependency calls, explicit ambiguous states, outbox durability, and reconciliation.
- Shutdown/consumer policy: `MICROSHOP_SHUTDOWN_TIMEOUT_MS` is bounded to 1–60 seconds (default 10 seconds), configures `HostOptions`, and ties Order/Notification MassTransit start/stop timeouts to the same bound. Application stopping makes readiness unhealthy while liveness remains process-only.
- Tests: `ProductInventoryClientTests` covers safe release/lookup retry, reserve no-retry, malformed/business no-retry behavior, timeout and caller cancellation; `ServiceReadinessTests` covers stopping readiness and bounded host options. The full solution has 108 passing .NET tests.
- Commands: `dotnet restore MicroShop.sln`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`; `docker compose --env-file .env.example -f deploy/compose.yaml config`.
- Notes: Phase 7.5 changes process configuration and HTTP behavior only; no database ownership or cross-service schema is added. Existing outbox, Notification inbox, and reconciliation evidence remains the durability/recovery boundary.

## 7.6 Phase 7 validation gate

- [x] No confirmed Order event is lost during RabbitMQ outage in outbox mode.
- [x] Outbox survives service restart.
- [x] Duplicate publish/redelivery creates one Notification.
- [x] Unknown inventory outcomes can be reconciled.
- [x] Cancellation pending can be reconciled.
- [x] Graceful shutdown is bounded and tested.

Evidence for 7.6:

- `OrderOutboxRabbitMqTests.RabbitMqOutageLeavesConfirmedOrderDurableAndRecoveryDrainsOutbox` proves confirmed Order plus pending outbox durability and recovery after RabbitMQ returns.
- `OutboxDispatcherTests` proves lease-expiry recovery/restart safety; Notification RabbitMQ integration proves duplicate delivery and process restart leave one Notification; `OrderReconciliationTests` proves `inventory_unknown` and `cancellation_pending` controlled outcomes.
- `ServiceReadinessTests` plus Product client timeout/cancellation tests prove bounded shutdown/readiness transition and cancellation propagation. Full .NET gate: 108 tests passed; Compose config validation passed.

---

# Phase 8 — Observability, Quality, and Final Completion

## 8.1 Structured logging

- [x] Add consistent structured logging to all .NET processes.
- [x] Add `service.name`.
- [x] Add environment.
- [x] Add trace/span IDs.
- [x] Add Order ID where relevant.
- [x] Add reservation ID where relevant.
- [x] Add message ID where relevant.
- [x] Add stable error/event codes.
- [x] Ensure logs contain no secrets.
- [x] Ensure logs avoid full customer payloads.

Evidence for 8.1:

- `MicroShop.ServiceDefaults` configures JSON console scopes and shared `MicroShopRequestLog`, `MicroShopLogging`, and service identity helpers. Product, Order, Notification, and Gateway all register the shared defaults and request middleware.
- Domain/background logs use bounded identifiers and stable codes such as `ORDER_CONFIRMED`, `INVENTORY_RESERVATION_RESULT`, `ORDER_OUTBOX_PUBLISH_FAILED`, and `NOTIFICATION_CONSUMED`; customer request bodies and credentials are not logged. Compose logs show `service.name`, `deployment.environment`, `trace.id`, `span.id`, `order.id`, `reservation.id`, and `message.id` on the relevant operations.
- Validation: `tests/MicroShop.Gateway.Tests/ObservabilityTests.cs`, full .NET suite (111 tests passed), `docker compose ... config`, and the confirmed-order Compose smoke with trace root `11111111111111111111111111111111` and Order `d0e58ba5-9d6a-4a4b-bd8c-aa0be6666c0c`.
- Implementation commit: `78a112c67c119274c16a050a69089332235b2b01` (`feat(observability): add telemetry foundation`).

## 8.2 Distributed tracing

- [x] Propagate W3C `traceparent` through Gateway.
- [x] Propagate trace context Order -> Product.
- [x] Propagate trace context through RabbitMQ.
- [x] Connect consumer spans.
- [x] Add OpenTelemetry registration.
- [x] Add optional collector/trace backend.
- [x] Verify one Order trace crosses all participating services.

Evidence for 8.2:

- Gateway preserves the incoming W3C parent; Order creates a Product client span and forwards its W3C context; the outbox dispatcher creates a producer span and stores the propagated context in the RabbitMQ message; Notification creates a linked consumer span.
- `OpenTelemetry.Extensions.Hosting`, ASP.NET Core/HTTP instrumentation, and OTLP exporter registration are centralized in `MicroShop.ServiceDefaults`. `OTEL_EXPORTER_OTLP_ENDPOINT` is optional and empty by default, so local operation remains independent of an external collector.
- `ObservabilityTests` verifies W3C identity and consumer-span parentage. Compose smoke evidence: trace root `11111111111111111111111111111111` is present in Gateway `/api/orders` request logs, Order confirmation/Product reservation logs, Order outbox producer scopes, and Notification consumer logs for Message `29dabbca-e878-4646-bce9-98511b37ea45`.
- Implementation commit: `78a112c67c119274c16a050a69089332235b2b01` (`feat(observability): add telemetry foundation`).

## 8.3 Metrics and health

- [x] Add HTTP request metrics.
- [x] Add Product client dependency metrics.
- [x] Add Order outcome counters.
- [x] Add reservation result counters.
- [x] Add outbox pending/failure metrics.
- [x] Add Notification consume result counters.
- [x] Avoid high-cardinality labels.
- [x] Review all liveness checks.
- [x] Review all readiness checks.
- [x] Ensure dependency failure does not incorrectly fail liveness.

Evidence for 8.3:

- `MicroShopTelemetry` registers bounded counters for HTTP/dependency/order/reservation/notification/outbox outcomes and observable outbox pending/dead-letter gauges. Labels are limited to operation/result values; identifiers are kept in logs, not metric dimensions.
- `ServiceDefaultsExtensions` registers ASP.NET Core and custom meters. `ApplicationLifecycleHealthCheck` keeps liveness process-only and makes readiness unhealthy during shutdown; dependency checks remain readiness signals.
- `ObservabilityTests` verifies the custom instrument names and bounded tags. `ServiceReadinessTests` covers lifecycle behavior; Release build and full .NET suite passed 111 tests; Compose `config`, image rebuild, startup, health checks, and API smoke passed.
- Implementation commit: `78a112c67c119274c16a050a69089332235b2b01` (`feat(observability): add telemetry foundation`).

## 8.4 Centralized log integration

- [x] Optionally connect all services to the existing Log Monitoring System.
- [x] Preserve independent local operation.
- [x] Verify search by trace ID.
- [x] Verify search by Order ID.
- [x] Document integration configuration.
- [x] Ensure no secret/customer payload leakage.

Evidence for 8.4:

- No external Log Monitoring System was supplied or added to the baseline scope. All .NET services emit collector-neutral JSON to stdout, and `OTEL_EXPORTER_OTLP_ENDPOINT` can connect an existing OTLP-capable collector without changing service code or Compose topology.
- Local Docker log search was verified by trace root `11111111111111111111111111111111` and Order `d0e58ba5-9d6a-4a4b-bd8c-aa0be6666c0c` across Gateway, Order, Product, outbox, and Notification logs. `docs/project/08_DEPLOYMENT_AND_OPERATIONS.md` documents fields, query strategy, optional endpoint configuration, and leakage rules.
- `scripts/e2e-compose.ps1`, `scripts/failure-injection.ps1`, and the full Compose smoke preserve independent local operation; logs contain bounded identifiers and stable codes, not credentials, tokens, or full customer payloads.

## 8.5 End-to-end automation

- [x] Add Playwright setup.
- [x] Test Product catalog.
- [x] Test Product creation/update.
- [x] Test successful checkout.
- [x] Test confirmed Order detail.
- [x] Test eventual Notification using bounded polling.
- [x] Test cancellation.
- [x] Verify stock restoration.
- [x] Test insufficient stock.
- [x] Test dependency failure UI.
- [x] Add Compose-based E2E execution.

Evidence for 8.5:

- `web/microshop-ui/playwright.config.ts` pins a Chromium project, Compose base URL override, serial execution for stateful demo data, bounded polling-compatible timeouts, and failure artifacts. `package.json` adds `e2e` and `e2e:install` scripts; `package-lock.json` pins `@playwright/test` 1.62.1.
- `web/microshop-ui/e2e/microshop.spec.ts` covers catalog display, UI create/update, confirmed checkout, Order detail, bounded Notification polling, cancellation, Product stock restoration, insufficient stock, and a dependency-failure UI response without creating an Order.
- `scripts/e2e-compose.ps1/.sh` validates/builds/starts the full stack and runs the browser suite without removing containers or volumes. Validation: `npm ci`, `npm run lint`, `npm run test -- --watch=false`, `npm run build`, and Compose Playwright run passed; 2 Playwright tests passed.

## 8.6 Failure-injection automation

- [x] Automate Product Service stopped scenario.
- [x] Automate Notification Service stopped scenario.
- [x] Automate RabbitMQ stopped scenario.
- [x] Automate duplicate message scenario.
- [x] Automate concurrent last-stock scenario.
- [x] Validate outbox recovery.
- [x] Validate error queue behavior.

Evidence for 8.6:

- `scripts/failure-injection.ps1` provides `all`, per-service, and integration-test scenarios. It stops/starts only named services, expects bounded downstream failure when Product is stopped, verifies durable confirmed Orders while Notification/RabbitMQ are stopped, and polls for recovery after restart. `scripts/failure-injection.sh` delegates to the same harness when `pwsh` is available.
- The integration scenario runs `PublishesDuplicateEventWithOneDurableNotification` and `MovesUnsupportedMessageToErrorQueueAfterBoundedRetry` (2 passed), `ConcurrentLastStockReservationsAllowOnlyOneSuccess` (1 passed), and `RabbitMqOutageLeavesConfirmedOrderDurableAndRecoveryDrainsOutbox` (1 passed).
- Local validation: Product stopped, Notification stopped, RabbitMQ stopped, and integration-test scenarios each passed; all named containers and volumes were preserved.

## 8.7 Security and deployment review

- [ ] Confirm no secrets committed.
- [ ] Confirm `.env.example` contains placeholders.
- [ ] Confirm internal ports are private.
- [ ] Confirm public deployment requires HTTPS.
- [ ] Confirm write APIs are labeled unsecured before optional authentication.
- [ ] Confirm no production-readiness claim is made.
- [ ] Add dependency/image vulnerability scanning.
- [ ] Add backup and restore instructions.
- [ ] Add rollback instructions.
- [ ] Run a restore drill for demo databases where practical.

## 8.8 Documentation completion

- [ ] Update `00_PROJECT_CONTEXT.md` status to actual implemented state.
- [ ] Verify every Product requirement against code/tests.
- [ ] Verify architecture diagrams match runtime.
- [ ] Verify domain state machines match implementation.
- [ ] Verify database documentation matches migrations.
- [ ] Verify API documentation matches OpenAPI/events.
- [ ] Verify codebase guide matches repository.
- [ ] Verify development commands work.
- [ ] Verify deployment/runbooks work.
- [ ] Update ADR statuses and technical debt.
- [ ] Update root README.
- [ ] Ensure AGENT workflow matches repository.
- [ ] Ensure this task file reflects actual completion.

## 8.9 Final CI and release gate

- [ ] `git diff --check` passes.
- [ ] `dotnet format --verify-no-changes` passes.
- [ ] `dotnet build --configuration Release` passes.
- [ ] `dotnet test --configuration Release` passes.
- [ ] Angular `npm ci` passes.
- [ ] Angular lint passes.
- [ ] Angular tests pass.
- [ ] Angular build passes.
- [ ] All migrations apply to empty databases.
- [ ] Docker images build.
- [ ] Full Compose stack starts.
- [ ] E2E suite passes.
- [ ] Failure-injection suite passes.
- [ ] No secrets detected.
- [ ] Working tree is clean after final commit.
- [ ] Final branch is pushed.
- [ ] Default branch contains the completed project.
- [ ] Final release tag is created only if repository workflow requests it.

---

# Optional Phase 9 — Authentication and Authorization

This phase is not required for the learning baseline.

- [ ] Record/supersede ADR before implementation.
- [ ] Select Identity architecture.
- [ ] Implement authentication.
- [ ] Protect Product management writes.
- [ ] Associate Orders with authenticated users.
- [ ] Add Gateway/JWT validation.
- [ ] Add service authorization.
- [ ] Add Angular login/session behavior.
- [ ] Add security tests.
- [ ] Update production-readiness documentation.

Do not begin this phase while required Phase 0–8 tasks remain unless explicitly requested.

---

# Final 100% Completion Definition

The project is 100% complete when:

- [ ] Every required Phase 0–8 task is `[x]`.
- [ ] No required task is `[ ]`, `[~]`, or `[!]`.
- [ ] All final CI and release gates pass.
- [ ] The complete Compose system runs end to end.
- [ ] Product, Order, and Notification databases remain isolated.
- [ ] Inventory concurrency is correct.
- [ ] HTTP failure ambiguity is modeled honestly.
- [ ] RabbitMQ redelivery is idempotent.
- [ ] Transactional outbox prevents lost confirmation events.
- [ ] Angular uses only Gateway public routes.
- [ ] Documentation matches actual runtime behavior.
- [ ] `AGENT.md` can independently guide the next agent.
- [ ] The default branch is committed and pushed.
- [ ] Final handoff documents remaining optional work only.

---

# Agent Update Protocol

After completing any task or coherent task group, the agent must update this file.

Required update behavior:

1. Change task status accurately.
2. Add evidence directly beneath the completed/partial/blocking item or phase.
3. Include files, tests, commands, and commit hash where available.
4. Do not mark a task complete before validation.
5. Re-check dependent tasks when architecture/contracts change.
6. Identify the next highest-priority incomplete task.
7. Commit this file with the implementation.
8. Merge and push according to `AGENT.md`.

Example:

```markdown
- [x] Implement atomic multi-item inventory reservation.

  Evidence:
  - Files: `src/Services/ProductService/...`
  - Tests: `ReserveInventoryTests`, `ConcurrentLastStockTests`
  - Commands: `dotnet test --configuration Release`
  - Commit: `abc1234`
  - Notes: Uses PostgreSQL row locking in stable Product-ID order.
```

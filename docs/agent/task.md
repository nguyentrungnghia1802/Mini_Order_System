# Mini Order System — Project Completion Tasks

Last reviewed: 2026-08-17.

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
- Notes: The CI workflow now applies Product migrations to an empty PostgreSQL service database. Docker image validation remains deferred until application Dockerfiles exist.

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
- Notes: Full-stack image, Order/Notification migrations, business integration, and E2E gates remain incomplete by design.

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
- Notes: Service-native API is intentionally not routed through YARP yet. PATCH uses `If-Match`/`ETag`, increments the explicit version, and returns `409 PRODUCT_CONCURRENCY_CONFLICT` for stale or racing updates. There is still no hard-delete endpoint.

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

- [ ] Create Product catalog route.
- [ ] Display active Product list.
- [ ] Display price and available stock.
- [ ] Handle loading state.
- [ ] Handle empty state.
- [ ] Handle API error state.
- [ ] Create Product management route.
- [ ] Create Product form using Reactive Forms.
- [ ] Add create Product UI.
- [ ] Add update Product UI.
- [ ] Add activate/deactivate UI.
- [ ] Map server validation errors to controls.
- [ ] Ensure responsive layout and keyboard use.

## 1.6 Phase 1 validation gate

- [x] Product Service starts independently.
- [x] Product migration applies to an empty database.
- [x] Product API OpenAPI is reachable.
- [x] Product tests pass.
- [ ] Angular Product UI works through the intended public route.
- [x] Product Service uses only its own database.

Evidence for 1.6 (partial Phase 1 gate):

- Files: Product host, Product migration, `.github/workflows/ci.yml`, and `tests/MicroShop.ProductService.Tests/`.
- Tests: Product PostgreSQL Testcontainers suite and native smoke (`/health/live=200`, `/health/ready=200`, catalog `200`, OpenAPI `200`).
- Commands: `dotnet restore MicroShop.sln`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release`; `dotnet test MicroShop.sln --configuration Release`; `docker compose ... config/up/ps`.
- Commits: `abc9a7a` (`feat(product): add catalog persistence slice`), `0654c31` (`feat(product): add optimistic catalog updates`).
- Notes: Angular Product UI and Gateway routing are not complete, so Phase 1 remains partial. Product PATCH/activation and update/concurrency behavior are implemented and tested in `0654c31`; the Product reservation boundary is implemented separately in Phase 3.

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
- [ ] Test Order concurrency guard.

Evidence for 2.4 (partial):

- Files: `tests/MicroShop.OrderService.Tests/OrderDomainTests.cs`, `OrderApiTests.cs`, `OrderDatabaseFixture.cs`, and `OrderPersistenceTests.cs`.
- Tests: 38 Order tests pass, including HTTP creation, known Product rejection, listing/detail pagination, domain transitions, migration persistence, state history, status constraints, readiness/OpenAPI, database credential isolation, typed Product HTTP mapping, timeout/unavailable handling, real orchestration state mapping, and cancellation release/idempotency. Order transition concurrency remains unimplemented.
- Commands: `dotnet test MicroShop.sln --configuration Release --no-restore`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`.
- Commits: `7bf1692` (`feat(order): add persistence foundation`), `d694a5b` (`feat(order): add fake order API foundation`).
- Notes: The HTTP creation, listing, and detail items are complete. The real Product reservation/orchestration slice is implemented; the separate Order transition concurrency guard remains deferred.

## 2.5 Angular Order foundation

- [ ] Create checkout route.
- [ ] Add customer form.
- [ ] Add Product quantity selection.
- [ ] Submit through Order API.
- [ ] Display confirmed result.
- [ ] Display rejected result.
- [ ] Display dependency error.
- [ ] Create Order list route.
- [ ] Create Order detail route.
- [ ] Handle loading/empty/error states.
- [ ] Prevent duplicate UI submission while active.

## 2.6 Phase 2 validation gate

- [x] Order Service starts independently.
- [x] Order migration applies cleanly.
- [x] Order tests pass.
- [ ] Angular checkout works with fake Product client.
- [x] Order Service does not access Product database.

Evidence for 2.6 (partial Phase 2 gate):

- Files: `src/Services/OrderService/MicroShop.OrderService/Program.cs`, Order migration, `tests/MicroShop.OrderService.Tests/`, and `.github/workflows/ci.yml`.
- Tests: 18 Order tests pass; readiness, native Order API, and OpenAPI are reachable with a fresh owned PostgreSQL database.
- Commands: `dotnet restore MicroShop.sln`; `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release`; `dotnet test MicroShop.sln --configuration Release`.
- Commits: `7bf1692` (`feat(order): add persistence foundation`), `d694a5b` (`feat(order): add fake order API foundation`).
- Notes: Angular checkout remains incomplete. The fake client is opt-in test compatibility; the default Order runtime uses real Product HTTP communication and persists explicit unknown outcomes.

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
- [ ] Optionally implement internal reservation query for reconciliation.
- [x] Ensure Gateway does not expose internal endpoints.

Evidence for 3.3 (partial):

- Files: `Features/Inventory/InventoryContracts.cs`, `InventoryReservationService.cs`, `InventoryEndpoints.cs`, Product `Program.cs`, `src/Gateway/MicroShop.Gateway/appsettings.json`, and `tests/MicroShop.Gateway.Tests/GatewayApiTests.cs`.
- Tests: `InventoryApiTests` covers `201` creation, authoritative snapshots/totals, `404 PRODUCT_NOT_FOUND`, `409 PRODUCT_INACTIVE`, `409 INSUFFICIENT_STOCK`, replay, mismatch, release, and idempotent release.
- Tests: Gateway integration coverage rejects `/internal/v1/inventory/reservations` without forwarding and rejects invalid HTTP destination configuration; Product reservation behavior remains covered by `InventoryApiTests`.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`; `git diff --check`.
- Commits: `d2a885a` (`feat(product): add atomic inventory reservations`) and `569af30` (`feat(gateway): add public yarp routes`).
- Notes: The API is intentionally service-native and internal. Gateway exclusion is now verified; the optional reconciliation query remains deferred.

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
- Tests: 19 Product tests, 38 Order tests, and 6 Gateway tests pass, including Product atomic reservation/release, known failures, replay/mismatch, concurrent last-stock, typed HTTP response/error mapping, unavailable dependency, timeout ambiguity, cancellation propagation, cancellation release/idempotency, `cancellation_pending`, Order unknown-state persistence, Gateway internal-route exclusion, and startup destination validation.
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
- Tests: Full solution validation passes with 64 .NET tests: 1 Architecture, 6 Gateway, 38 Order, and 19 Product.
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
- [ ] Ensure service-native ports are not required by Angular.
- [x] Handle downstream unavailable as gateway error.
- [x] Validate configured clusters at startup.

Evidence for 4.2 (partial):

- Files: `BootstrapConfiguration.cs`, `Program.cs`, `appsettings.json`, and `GatewayApiTests.cs`.
- Tests: Internal Product paths return `404 GATEWAY_ROUTE_NOT_FOUND` without forwarding; unavailable destinations return `502 DOWNSTREAM_UNAVAILABLE`; invalid Product/Order destination schemes fail startup configuration.
- Commands: `dotnet test MicroShop.sln --configuration Release --no-restore` (64 tests pass).
- Commit: `569af30` (`feat(gateway): add public yarp routes`).
- Notes: Angular still has no feature API clients, so removing all service-native browser requirements remains in 4.3.

## 4.3 Angular migration to Gateway

- [x] Replace direct Product API URL with `/api/products`.
- [x] Replace direct Order API URL with `/api/orders`.
- [x] Use same-origin API requests.
- [x] Remove internal service URLs from Angular configuration.
- [x] Add Gateway connectivity error handling.

Evidence for 4.3:

- Files: `web/microshop-ui/src/app/core/api/api.paths.ts`, `product-api.service.ts`, `order-api.service.ts`, `gateway-error.ts`, `gateway-error.interceptor.ts`, `app.config.ts`, and `gateway-api.spec.ts`.
- Tests: Product listing, Order create/cancel, same-origin paths, and `502 DOWNSTREAM_UNAVAILABLE` mapping are covered by 3 new Angular tests; the full Angular suite has 5 passing tests.
- Commands: `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`; `rg -n -i "product-service|order-service|notification-service|internal/v1|api/v1" web/microshop-ui/src` (no service/internal API matches).
- Commit: `f750963` (`feat(ui): route api clients through gateway`).
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

- [ ] Angular works using only Gateway.
- [x] Product and Order services are hidden from normal browser use.
- [x] Internal inventory API cannot be reached through Gateway.
- [x] Gateway tests pass.

Evidence for 4.5 (partial):

- Files: Gateway route configuration and `tests/MicroShop.Gateway.Tests/GatewayApiTests.cs`.
- Tests: 6 Gateway tests and the full 64-test .NET solution pass.
- Commands: `dotnet format MicroShop.sln --verify-no-changes --no-restore`; `dotnet build MicroShop.sln --configuration Release --no-restore`; `dotnet test MicroShop.sln --configuration Release --no-restore`.
- Commit: `569af30` (`feat(gateway): add public yarp routes`).
- Notes: Public routes, internal-route exclusion, and Angular same-origin API clients are complete. Feature screens and application-container port isolation remain before this gate can be complete.

---

# Phase 5 — RabbitMQ and Notification Service

## 5.1 Shared event contracts

- [ ] Create `MicroShop.Contracts`.
- [ ] Add `OrderConfirmedV1`.
- [ ] Add message ID.
- [ ] Add order ID.
- [ ] Add customer destination fields.
- [ ] Add total and currency.
- [ ] Add item snapshots.
- [ ] Add occurred-at UTC.
- [ ] Add schema version.
- [ ] Keep contracts free from EF entities and business logic.
- [ ] Add serialization compatibility test.

## 5.2 RabbitMQ and MassTransit

- [ ] Add RabbitMQ container.
- [ ] Add development management UI.
- [ ] Configure MassTransit producer.
- [ ] Configure durable Notification receive endpoint.
- [ ] Configure bounded retry.
- [ ] Configure error queue behavior.
- [ ] Add RabbitMQ readiness checks.
- [ ] Ensure credentials come from environment.

## 5.3 Direct publish learning milestone

- [ ] Publish `OrderConfirmedV1` after Order confirmation.
- [ ] Preserve event message ID.
- [ ] Propagate trace context.
- [ ] Document database/broker dual-write gap.
- [ ] Add test demonstrating direct-publish failure window.

## 5.4 Notification database

- [ ] Create Notification DbContext.
- [ ] Define ConsumedMessage entity.
- [ ] Define Notification entity.
- [ ] Add unique consumed message constraint.
- [ ] Add unique source message constraint.
- [ ] Add query indexes.
- [ ] Create migration.
- [ ] Add Notification database health check.

## 5.5 Notification consumer

- [ ] Implement `OrderConfirmedV1` consumer.
- [ ] Persist consumed message ID and Notification in one transaction.
- [ ] Suppress duplicate delivery.
- [ ] Generate readable simulated notification.
- [ ] Preserve order ID and customer email.
- [ ] Log duplicate suppression.
- [ ] Let failures throw for retry/error handling.
- [ ] Ensure Notification Service does not query Product/Order databases.

## 5.6 Notification read API

- [ ] Implement `GET /api/v1/notifications`.
- [ ] Support customer email filter.
- [ ] Support order ID filter.
- [ ] Support pagination.
- [ ] Optionally implement mark-as-read.
- [ ] Add OpenAPI.
- [ ] Add Gateway Notification route.

## 5.7 Angular Notification UI

- [ ] Create Notification route.
- [ ] List notifications.
- [ ] Add manual refresh.
- [ ] Add bounded polling.
- [ ] Handle eventual consistency.
- [ ] Handle empty/error/loading states.
- [ ] Optionally mark notification read.

## 5.8 Messaging tests

- [ ] Publish/consume integration test.
- [ ] Duplicate event test.
- [ ] Consumer retry test.
- [ ] Error queue test.
- [ ] Notification Service restart test.
- [ ] Durable queued-message recovery test.
- [ ] Verify Order confirmation does not wait for Notification consumer.
- [ ] Verify Notification Service uses only its own database.

## 5.9 Phase 5 validation gate

- [ ] Confirmed Order emits event.
- [ ] Notification Service consumes event.
- [ ] Duplicate event creates one Notification.
- [ ] Stopping Notification Service does not block Order confirmation.
- [ ] Restarting Notification Service drains queued messages.
- [ ] Angular displays eventual Notification.

---

# Phase 6 — Docker Compose Completion

## 6.1 Container images

- [ ] Add multi-stage Product Service Dockerfile.
- [ ] Add multi-stage Order Service Dockerfile.
- [ ] Add multi-stage Notification Service Dockerfile.
- [ ] Add multi-stage Gateway Dockerfile.
- [ ] Add Angular build/nginx Dockerfile.
- [ ] Use runtime-only images.
- [ ] Use non-root runtime users where practical.
- [ ] Add image metadata/version labels if useful.

## 6.2 Compose stack

- [ ] Add `deploy/compose.yaml`.
- [ ] Add Web.
- [ ] Add Gateway.
- [ ] Add Product Service.
- [ ] Add Order Service.
- [ ] Add Notification Service.
- [ ] Add PostgreSQL.
- [ ] Create separate Product database and user.
- [ ] Create separate Order database and user.
- [ ] Create separate Notification database and user.
- [ ] Add RabbitMQ.
- [ ] Add persistent PostgreSQL volume.
- [ ] Add persistent RabbitMQ volume.
- [ ] Add internal application network.
- [ ] Publish only required ports.
- [ ] Keep database and RabbitMQ application ports private by default.
- [ ] Add development override for debugging ports.

## 6.3 Migration and startup

- [ ] Add explicit Product migration command.
- [ ] Add explicit Order migration command.
- [ ] Add explicit Notification migration command.
- [ ] Add migrate-all script.
- [ ] Add safe local reset script requiring explicit confirmation/permission.
- [ ] Ensure services do not race migrations.
- [ ] Add health-based startup dependencies where supported.
- [ ] Validate configuration at startup.

## 6.4 One-command local run

- [ ] Document full-stack start command.
- [ ] Document stop command.
- [ ] Document logs command.
- [ ] Document status command.
- [ ] Document seed command.
- [ ] Document migration command.
- [ ] Verify Windows PowerShell workflow.
- [ ] Verify Linux/macOS workflow where practical.

## 6.5 Phase 6 validation gate

- [ ] Full stack starts with one documented Compose command.
- [ ] Angular is reachable.
- [ ] Gateway routes all public APIs.
- [ ] Product/Order/Notification databases are separate.
- [ ] RabbitMQ management shows expected topology.
- [ ] End-to-end Order flow works in Compose.
- [ ] Internal service ports are not unnecessarily public.

---

# Phase 7 — Reliability Hardening

## 7.1 Transactional outbox

- [ ] Add OutboxMessage entity.
- [ ] Add Outbox migration.
- [ ] Insert `OrderConfirmedV1` outbox record in Order confirmation transaction.
- [ ] Stop using direct broker publish as final path.
- [ ] Preserve the direct-publish demonstration in documentation/tests only.
- [ ] Implement outbox dispatcher.
- [ ] Claim pending records safely.
- [ ] Add attempt count.
- [ ] Add next-attempt time.
- [ ] Add last-error field.
- [ ] Add lock/lease field.
- [ ] Mark successful publish.
- [ ] Make dispatcher restart-safe.
- [ ] Prevent infinite retry loop.

## 7.2 Outbox operations

- [ ] Add outbox backlog logs.
- [ ] Add outbox readiness/health policy.
- [ ] Add outbox metrics if metrics exist.
- [ ] Add operator query or documented SQL for pending outbox.
- [ ] Add recovery procedure.
- [ ] Add RabbitMQ outage test.
- [ ] Verify Order confirmation remains durable when RabbitMQ is unavailable.
- [ ] Verify backlog drains after RabbitMQ recovers.

## 7.3 Inbox/idempotency hardening

- [ ] Ensure consumed-message insert and Notification creation share one transaction.
- [ ] Test duplicate redelivery under concurrency.
- [ ] Test consumer process restart between attempts.
- [ ] Verify unique constraints prevent duplicate side effects.
- [ ] Add cleanup/retention policy only if justified.

## 7.4 Reconciliation

- [ ] Add internal/manual reconciliation path for `inventory_unknown`.
- [ ] Query Product reservation by order ID.
- [ ] Reconcile known existing reservation to confirmed Order.
- [ ] Reconcile known absent reservation safely.
- [ ] Add reconciliation audit/history.
- [ ] Add reconciliation tests.
- [ ] Add `cancellation_pending` reconciliation.
- [ ] Document runbook.

## 7.5 Resilience policies

- [ ] Review Product timeout.
- [ ] Add bounded retry only where idempotency makes it safe.
- [ ] Add circuit breaker only if justified.
- [ ] Add graceful shutdown for services.
- [ ] Add bounded consumer shutdown.
- [ ] Add cancellation token propagation.
- [ ] Add readiness transitions during shutdown.

## 7.6 Phase 7 validation gate

- [ ] No confirmed Order event is lost during RabbitMQ outage in outbox mode.
- [ ] Outbox survives service restart.
- [ ] Duplicate publish/redelivery creates one Notification.
- [ ] Unknown inventory outcomes can be reconciled.
- [ ] Cancellation pending can be reconciled.
- [ ] Graceful shutdown is bounded and tested.

---

# Phase 8 — Observability, Quality, and Final Completion

## 8.1 Structured logging

- [ ] Add consistent structured logging to all .NET processes.
- [ ] Add `service.name`.
- [ ] Add environment.
- [ ] Add trace/span IDs.
- [ ] Add Order ID where relevant.
- [ ] Add reservation ID where relevant.
- [ ] Add message ID where relevant.
- [ ] Add stable error/event codes.
- [ ] Ensure logs contain no secrets.
- [ ] Ensure logs avoid full customer payloads.

## 8.2 Distributed tracing

- [ ] Propagate W3C `traceparent` through Gateway.
- [ ] Propagate trace context Order -> Product.
- [ ] Propagate trace context through RabbitMQ.
- [ ] Connect consumer spans.
- [ ] Add OpenTelemetry registration.
- [ ] Add optional collector/trace backend.
- [ ] Verify one Order trace crosses all participating services.

## 8.3 Metrics and health

- [ ] Add HTTP request metrics.
- [ ] Add Product client dependency metrics.
- [ ] Add Order outcome counters.
- [ ] Add reservation result counters.
- [ ] Add outbox pending/failure metrics.
- [ ] Add Notification consume result counters.
- [ ] Avoid high-cardinality labels.
- [ ] Review all liveness checks.
- [ ] Review all readiness checks.
- [ ] Ensure dependency failure does not incorrectly fail liveness.

## 8.4 Centralized log integration

- [ ] Optionally connect all services to the existing Log Monitoring System.
- [ ] Preserve independent local operation.
- [ ] Verify search by trace ID.
- [ ] Verify search by Order ID.
- [ ] Document integration configuration.
- [ ] Ensure no secret/customer payload leakage.

## 8.5 End-to-end automation

- [ ] Add Playwright setup.
- [ ] Test Product catalog.
- [ ] Test Product creation/update.
- [ ] Test successful checkout.
- [ ] Test confirmed Order detail.
- [ ] Test eventual Notification using bounded polling.
- [ ] Test cancellation.
- [ ] Verify stock restoration.
- [ ] Test insufficient stock.
- [ ] Test dependency failure UI.
- [ ] Add Compose-based E2E execution.

## 8.6 Failure-injection automation

- [ ] Automate Product Service stopped scenario.
- [ ] Automate Notification Service stopped scenario.
- [ ] Automate RabbitMQ stopped scenario.
- [ ] Automate duplicate message scenario.
- [ ] Automate concurrent last-stock scenario.
- [ ] Validate outbox recovery.
- [ ] Validate error queue behavior.

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

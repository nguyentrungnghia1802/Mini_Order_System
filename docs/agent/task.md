# Mini Order System — Project Completion Tasks

Last reviewed: 2026-08-02.

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
- [~] Add migration validation against empty PostgreSQL databases.
- [~] Add Docker image build validation.
- [x] Add secret scanning or equivalent repository protection.

Evidence for 0.4:

- Files: `.github/workflows/ci.yml`.
- Tests: workflow syntax reviewed; local constituent commands pass where configuration exists.
- Commands: `dotnet restore`; `dotnet build`; `dotnet test`; `npm ci`; `npm run lint`; `npm run test -- --watch=false`; `npm run build`; Compose config/up/ps.
- Commit: `b2a924d` (`chore(repo): bootstrap Phase 0 standards`).
- Notes: Migration and image jobs are explicit deferred guards because Phase 0 has no migrations or application Dockerfiles; they must be upgraded before those artifacts are introduced.

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
- Notes: Full-stack image, migration, business integration, and E2E gates remain incomplete by design.

---

# Phase 1 — Product Service

## 1.1 Product domain

- [ ] Define Product entity.
- [ ] Add fields:
  - ID
  - name
  - description
  - unit price
  - currency
  - available stock
  - active state
  - timestamps
  - concurrency token
- [ ] Enforce nonnegative price.
- [ ] Enforce nonnegative stock.
- [ ] Use `decimal` for money.
- [ ] Use UTC timestamps.

## 1.2 Product database

- [ ] Create Product DbContext.
- [ ] Create EF Core entity configuration.
- [ ] Configure PostgreSQL.
- [ ] Create Product database migration.
- [ ] Add required indexes.
- [ ] Add database constraints.
- [ ] Add Product database health check.
- [ ] Add Product database seed script.

## 1.3 Product API

- [ ] Implement `GET /api/v1/products`.
- [ ] Implement pagination.
- [ ] Implement active-only shopper listing.
- [ ] Implement optional inactive Product listing.
- [ ] Implement `GET /api/v1/products/{id}`.
- [ ] Implement `POST /api/v1/products`.
- [ ] Implement `PATCH /api/v1/products/{id}`.
- [ ] Implement activate/deactivate behavior.
- [ ] Do not implement hard delete.
- [ ] Add validation.
- [ ] Add RFC 7807 Problem Details.
- [ ] Add stable error codes.
- [ ] Add OpenAPI documentation.

## 1.4 Product tests

- [ ] Unit-test Product validation.
- [ ] Integration-test Product creation.
- [ ] Integration-test Product listing.
- [ ] Integration-test Product update.
- [ ] Test inactive Product visibility.
- [ ] Test database constraints.
- [ ] Test Product concurrency conflict.
- [ ] Use PostgreSQL Testcontainers.

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

- [ ] Product Service starts independently.
- [ ] Product migration applies to an empty database.
- [ ] Product API OpenAPI is reachable.
- [ ] Product tests pass.
- [ ] Angular Product UI works through the intended public route.
- [ ] Product Service uses only its own database.

---

# Phase 2 — Order Service Foundation

## 2.1 Order domain

- [ ] Define Order entity.
- [ ] Define OrderItem entity.
- [ ] Define OrderStateHistory entity.
- [ ] Add Order states:
  - `pending_inventory`
  - `confirmed`
  - `rejected`
  - `inventory_unknown`
  - `cancellation_pending`
  - `cancelled`
- [ ] Add customer name and normalized email.
- [ ] Add total and currency.
- [ ] Add failure code/detail.
- [ ] Add timestamps.
- [ ] Add concurrency token.
- [ ] Define valid state transitions.

## 2.2 Order database

- [ ] Create Order DbContext.
- [ ] Create EF entity configurations.
- [ ] Configure PostgreSQL.
- [ ] Create initial Order migration.
- [ ] Add state constraints.
- [ ] Add list/query indexes.
- [ ] Add Order database health check.
- [ ] Verify Order database credentials cannot access Product database.

## 2.3 Order API foundation

- [ ] Implement request DTO containing customer and Product IDs/quantities only.
- [ ] Reject browser-supplied price/name/total/status.
- [ ] Reject duplicate Product IDs.
- [ ] Enforce item-count limits.
- [ ] Enforce quantity limits.
- [ ] Implement `POST /api/v1/orders`.
- [ ] Initially use a fake Product client behind an interface.
- [ ] Persist `pending_inventory`.
- [ ] Persist known fake Product snapshots.
- [ ] Transition to `confirmed`.
- [ ] Implement `GET /api/v1/orders`.
- [ ] Implement `GET /api/v1/orders/{id}`.
- [ ] Add pagination.
- [ ] Add Problem Details and stable error codes.
- [ ] Add OpenAPI.

## 2.4 Order tests

- [ ] Unit-test Order transition rules.
- [ ] Unit-test duplicate Product ID rejection.
- [ ] Unit-test total calculation from snapshots.
- [ ] Integration-test Order creation.
- [ ] Integration-test Order listing.
- [ ] Integration-test Order detail.
- [ ] Integration-test state-history persistence.
- [ ] Test Order concurrency guard.

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

- [ ] Order Service starts independently.
- [ ] Order migration applies cleanly.
- [ ] Order tests pass.
- [ ] Angular checkout works with fake Product client.
- [ ] Order Service does not access Product database.

---

# Phase 3 — Synchronous Product–Order Communication

## 3.1 Inventory reservation domain

- [ ] Define InventoryReservation entity.
- [ ] Define InventoryReservationItem entity.
- [ ] Add reservation states:
  - `reserved`
  - `released`
- [ ] Add `orderId` unique constraint.
- [ ] Add canonical request hash.
- [ ] Add Product snapshot fields.
- [ ] Add reservation timestamps.

## 3.2 Inventory reservation database

- [ ] Create migration for reservation tables.
- [ ] Add unique `(reservationId, productId)` constraint.
- [ ] Add unique `orderId`.
- [ ] Add status constraints.
- [ ] Add indexes for order lookup.
- [ ] Add concurrency-safe stock update strategy.
- [ ] Lock rows in stable Product-ID order.

## 3.3 Internal Product API

- [ ] Implement `POST /internal/v1/inventory/reservations`.
- [ ] Implement atomic multi-item reservation.
- [ ] Reject full reservation on any invalid item.
- [ ] Return authoritative Product snapshots.
- [ ] Return total amount.
- [ ] Return `201` for new reservation.
- [ ] Return `200` for idempotent replay.
- [ ] Return `RESERVATION_REQUEST_MISMATCH` for same order with different items.
- [ ] Implement `POST /internal/v1/inventory/reservations/{orderId}/release`.
- [ ] Make release idempotent.
- [ ] Optionally implement internal reservation query for reconciliation.
- [ ] Ensure Gateway does not expose internal endpoints.

## 3.4 Order Product client

- [ ] Create typed `HttpClient`.
- [ ] Configure Product Service internal URL.
- [ ] Configure explicit timeout.
- [ ] Propagate `traceparent`.
- [ ] Propagate cancellation token.
- [ ] Map Product business errors.
- [ ] Map dependency unavailable.
- [ ] Map ambiguous timeout to `inventory_unknown`.
- [ ] Avoid blind retries.
- [ ] Use stable `orderId` for safe replay.

## 3.5 Real Order orchestration

- [ ] Generate order ID before remote call.
- [ ] Persist `pending_inventory`.
- [ ] Call Product reservation.
- [ ] Store authoritative Product snapshots.
- [ ] Calculate Order total from snapshots.
- [ ] Transition to `confirmed`.
- [ ] Transition known Product failures to `rejected`.
- [ ] Transition ambiguous failures to `inventory_unknown`.
- [ ] Add state-history entries.
- [ ] Return stable public errors with order ID where appropriate.

## 3.6 Cancellation

- [ ] Implement `POST /api/v1/orders/{id}/cancel`.
- [ ] Allow only from `confirmed`.
- [ ] Call Product release.
- [ ] Mark `cancelled` only after known release.
- [ ] Mark `cancellation_pending` for ambiguous release.
- [ ] Ensure repeated cancellation never restores stock twice.
- [ ] Return `canCancel` from Order responses.

## 3.7 Synchronous communication tests

- [ ] Test successful reservation.
- [ ] Test insufficient stock.
- [ ] Test Product not found.
- [ ] Test inactive Product.
- [ ] Test reservation replay.
- [ ] Test reservation mismatch.
- [ ] Test idempotent release.
- [ ] Test concurrent last-stock purchase.
- [ ] Test Product Service unavailable.
- [ ] Test timeout with ambiguous outcome.
- [ ] Test cancellation.
- [ ] Test repeated cancellation.
- [ ] Test `cancellation_pending`.
- [ ] Verify no partial stock decrement.
- [ ] Verify stock never becomes negative.

## 3.8 Phase 3 validation gate

- [ ] Order Service uses real Product HTTP communication.
- [ ] Product and Order databases remain isolated.
- [ ] All concurrency tests pass using PostgreSQL.
- [ ] All failure states match documentation.
- [ ] Cancellation restores stock exactly once.
- [ ] Internal Product API is not public.

---

# Phase 4 — YARP API Gateway

## 4.1 Gateway foundation

- [ ] Configure YARP.
- [ ] Add Product cluster.
- [ ] Add Order cluster.
- [ ] Add Notification cluster placeholder or deferred route.
- [ ] Add Product public routes.
- [ ] Add Order public routes.
- [ ] Configure path transforms.
- [ ] Configure CORS.
- [ ] Configure body/request limits.
- [ ] Add liveness.
- [ ] Add readiness.
- [ ] Add structured logging.
- [ ] Propagate trace headers.

## 4.2 Gateway safety

- [ ] Ensure `/internal/*` is never routed.
- [ ] Ensure browser receives no internal hostname.
- [ ] Ensure service-native ports are not required by Angular.
- [ ] Handle downstream unavailable as gateway error.
- [ ] Validate configured clusters at startup.

## 4.3 Angular migration to Gateway

- [ ] Replace direct Product API URL with `/api/products`.
- [ ] Replace direct Order API URL with `/api/orders`.
- [ ] Use same-origin API requests.
- [ ] Remove internal service URLs from Angular configuration.
- [ ] Add Gateway connectivity error handling.

## 4.4 Gateway tests

- [ ] Test Product route.
- [ ] Test Order route.
- [ ] Test path transforms.
- [ ] Test trace-header propagation.
- [ ] Test downstream unavailable behavior.
- [ ] Test internal route rejection.
- [ ] Test Gateway health.

## 4.5 Phase 4 validation gate

- [ ] Angular works using only Gateway.
- [ ] Product and Order services are hidden from normal browser use.
- [ ] Internal inventory API cannot be reached through Gateway.
- [ ] Gateway tests pass.

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

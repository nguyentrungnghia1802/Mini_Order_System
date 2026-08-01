# Codebase Guide

Last reviewed: 2026-08-02.

## 1. Repository layout

The Phase 0 repository now follows this layout. Empty future feature folders are intentionally omitted until their owning phase needs them.

Recommended monorepo:

```text
.
|-- MicroShop.sln
|-- src/
|   |-- Gateway/
|   |   \-- MicroShop.Gateway/
|   |-- BuildingBlocks/
|   |   |-- MicroShop.Contracts/
|   |   \-- MicroShop.ServiceDefaults/
|   \-- Services/
|       |-- ProductService/
|       |   \-- MicroShop.ProductService/
|       |-- OrderService/
|       |   \-- MicroShop.OrderService/
|       \-- NotificationService/
|           \-- MicroShop.NotificationService/
|-- tests/
|   |-- MicroShop.ProductService.Tests/
|   |-- MicroShop.OrderService.Tests/
|   |-- MicroShop.NotificationService.Tests/
|   |-- MicroShop.ContractTests/
|   \-- MicroShop.EndToEndTests/
|-- web/
|   \-- microshop-ui/
|-- deploy/
|   |-- compose.yaml
|   |-- compose.override.yaml
|   |-- postgres-init/
|   \-- nginx/
|-- scripts/
|-- docs/
|-- .github/workflows/
|-- .editorconfig
|-- Directory.Build.props
|-- Directory.Packages.props
|-- global.json
|-- .env.example
|-- README.md
\-- AGENTS.md
```

The exact implementation may separate test or persistence projects later. Start with a small number of projects so service boundaries, not Clean Architecture ceremony, remain visible.

## 2. Project responsibilities

| Project | Responsibility |
| --- | --- |
| `MicroShop.Gateway` | YARP routes, public cross-cutting policy, health |
| `MicroShop.Contracts` | Versioned integration-message records only |
| `MicroShop.ServiceDefaults` | Small shared observability/health/config helpers |
| `MicroShop.ProductService` | Product and inventory HTTP API plus Product DB |
| `MicroShop.OrderService` | Order HTTP API, Product client, message producer/outbox |
| `MicroShop.NotificationService` | Consumer, Notification DB, read API |
| `microshop-ui` | Angular application |
| service test projects | Unit/integration tests owned by each service |
| contract tests | HTTP/event compatibility |
| end-to-end tests | Browser/stack-level flows |

## 3. Sharing rules

### Allowed in `MicroShop.Contracts`

- immutable event records;
- event item records;
- schema/version constants required by producers/consumers;
- no framework-specific behavior unless required for serialization compatibility.

### Allowed in `MicroShop.ServiceDefaults`

- structured logging setup;
- OpenTelemetry registration;
- health endpoint mapping;
- Problem Details helpers;
- common JSON configuration;
- configuration validation helpers.

### Forbidden shared content

- EF Core entities or DbContexts;
- repositories;
- product/order domain services;
- service-specific request/response DTOs;
- database migrations;
- business rules;
- a shared "common database";
- direct references from one service to another service implementation.

Order Service must reference Product HTTP contracts defined locally or in a narrowly named client-contract package, not Product Service implementation.

## 4. Service internal layout

Recommended:

```text
MicroShop.ProductService/
|-- Features/
|   |-- Products/
|   |   |-- CreateProduct.cs
|   |   |-- GetProduct.cs
|   |   |-- ListProducts.cs
|   |   \-- UpdateProduct.cs
|   \-- Inventory/
|       |-- ReserveInventory.cs
|       |-- ReleaseInventory.cs
|       \-- GetReservation.cs
|-- Persistence/
|   |-- ProductDbContext.cs
|   |-- Entities/
|   |-- Configurations/
|   \-- Migrations/
|-- Infrastructure/
|   |-- Errors/
|   |-- Observability/
|   \-- Time/
|-- Program.cs
\-- appsettings.json
```

A feature file may contain endpoint mapping, DTO, validator, and handler for a small vertical slice. Split only when a file becomes difficult to understand/test.

## 5. Dependency rules

| Layer/area | May depend on | Must not depend on |
| --- | --- | --- |
| Endpoint | DTO, handler/service, authorization later | another service DbContext |
| Handler/application service | own persistence, remote client abstractions | Angular, Gateway |
| Persistence | own entities/configuration, EF Core | HTTP response formatting |
| Product HTTP client | typed contracts/resilience | Product database |
| Message producer | integration contracts, bus/outbox | Notification implementation |
| Consumer | integration contracts, Notification persistence | Order database |
| Gateway | YARP/config/health | product/order business policy |
| Angular feature | frontend API service/models | internal service URL/database |

## 6. .NET conventions

- Nullable reference types enabled.
- Treat warnings as errors in CI, with explicit justified exceptions.
- Use `async` for I/O and pass `CancellationToken`.
- Use `decimal` for money.
- Use `DateTimeOffset` or UTC `DateTime`; standardize serialization.
- Prefer records for immutable contracts.
- Prefer `Guid` generated before remote orchestration.
- Use options classes with startup validation.
- Avoid service locator and static mutable state.
- Avoid generic repository wrappers over EF Core unless they add a real boundary.
- Use explicit transaction boundaries for inventory/order transitions.
- Map expected failures to stable Problem Details codes.
- Do not catch `Exception` merely to return success or hide failure.

## 7. Angular conventions

- Use standalone components/routes.
- Use strict TypeScript and strict template checking.
- Keep API base path relative (`/api`).
- Use Reactive Forms for checkout/product forms.
- Keep HTTP calls in feature API services.
- Use RxJS/signals for view state; avoid nested subscriptions.
- Display server field errors beside exact controls.
- Disable duplicate submit while request is active, but do not rely on UI for server idempotency.
- Components expose loading, empty, error, and success states.
- Do not put service URLs or secrets in browser environment files.
- Keep terms aligned with API: product, reservation, order, notification.

## 8. Naming conventions

### .NET

- Namespaces/classes: PascalCase.
- Local variables/parameters: camelCase.
- Private fields: `_camelCase` when repository standard uses it.
- Async methods: `Async` suffix unless endpoint/handler convention omits it.
- Event contracts: `OrderConfirmedV1`.
- Database columns: snake_case through EF naming convention.
- Migration names: descriptive PascalCase, e.g. `InitialProductSchema`.

### Angular

- Files: kebab-case.
- Components/classes: PascalCase.
- Selectors: `app-*`.
- Feature routes: `catalog`, `checkout`, `orders`, `notifications`, `products/manage`.
- API models: suffix `Request`, `Response`, or domain-specific interface name.

### Errors

Stable uppercase snake case:

```text
INSUFFICIENT_STOCK
ORDER_STATE_CONFLICT
PRODUCT_SERVICE_UNAVAILABLE
```

## 9. Adding a Product endpoint

1. Confirm requirement and rules in docs `01`, `03`, and `05`.
2. Add request/response DTO.
3. Add validation.
4. Add handler and persistence logic.
5. Map expected errors.
6. Add OpenAPI metadata.
7. Add unit tests for rules.
8. Add integration tests against PostgreSQL.
9. Route through Gateway only if browser-facing.
10. Add/update Angular client if used.
11. Update docs and changelog.

## 10. Adding an Order behavior

1. Draw the state transition first.
2. Decide local vs remote side effects.
3. Persist an explicit pre-remote state when outcome can be ambiguous.
4. Define Product client contract and timeout.
5. Define compensation/reconciliation behavior.
6. Add state guard/concurrency token.
7. Decide whether an integration event is emitted.
8. Use outbox if the target phase requires durable publication.
9. Test known business failure and infrastructure ambiguity separately.
10. Update sequence/failure documentation.

## 11. Adding an integration event

1. Name a fact in past tense.
2. Put versioned immutable record in `MicroShop.Contracts`.
3. Include all data required by the consumer.
4. Add message ID and occurred-at UTC.
5. Update producer.
6. Update consumer.
7. Add serialization compatibility test.
8. Add duplicate-delivery test.
9. Add error-queue/failure test.
10. Update `05_API.md` event catalog and ADR if semantics change.

## 12. Adding a database capability

1. Confirm service ownership.
2. Add entity/configuration.
3. Add constraints and indexes.
4. Generate migration in owning service.
5. Review SQL.
6. Apply to empty test database.
7. Apply from previous schema in integration test/CI.
8. Test transaction/concurrency.
9. Update `04_DATABASE.md`.
10. Never solve a query by reading another service database.

## 13. Gateway changes

Gateway route changes must include:

- route/cluster config;
- transform behavior;
- timeout/body limits if relevant;
- route contract test;
- no accidental exposure of `/internal`;
- compatibility with Angular relative paths;
- health/readiness impact.

Gateway must stay thin. If logic depends on stock/order state, it belongs in a service.

## 14. Error handling

Use a central exception-to-Problem-Details boundary per service.

Expected errors include:

- validation result;
- not found;
- conflict;
- dependency unavailable;
- unknown distributed outcome.

Unexpected exceptions:

- logged with trace ID;
- mapped to `500 INTERNAL_ERROR`;
- do not expose stack trace outside development;
- are not logged repeatedly in every layer.

## 15. Logging

Good log:

```text
Inventory reservation confirmed
service=product-service
trace_id=...
order_id=...
reservation_id=...
item_count=2
```

Avoid:

- full customer payload;
- connection strings;
- broker passwords;
- repetitive "entered method" logs;
- duplicate exception logging.

Use event IDs or stable message templates for important operational events.

## 16. Configuration

Configuration hierarchy:

1. `appsettings.json` safe defaults;
2. environment-specific non-secret config;
3. environment variables/secrets;
4. test overrides.

Each process validates required options at startup. Missing Product Service address, database connection, or RabbitMQ host must fail fast with a clear message.

## 17. Testing location

- pure rule tests in service test project;
- API/database integration tests in service test project;
- event compatibility in contract tests;
- browser tests in end-to-end project;
- do not test EF concurrency with mocked DbSet/InMemory provider.

## 18. Files requiring extra care

| File/area | Why |
| --- | --- |
| Gateway route config | Small change can expose/internal-break all APIs |
| Product reservation handler | Stock correctness and idempotency |
| Order orchestration | Distributed failure states |
| Outbox dispatcher | Duplicate/lost-message risk |
| Notification consumer | At-least-once side effects |
| EF migrations | Persistent data |
| Compose networking | Service discovery and public port exposure |
| Shared contract assembly | Producer/consumer compatibility |

## 19. Pull request checklist

- [ ] Requirement/ADR identified.
- [ ] Service boundary remains correct.
- [ ] No cross-database access.
- [ ] Failure path tested.
- [ ] Cancellation token propagated.
- [ ] Idempotency considered.
- [ ] Logs contain trace/entity context without secrets.
- [ ] OpenAPI/event contract updated.
- [ ] Migration reviewed.
- [ ] Angular states handled.
- [ ] Docker/CI still pass.
- [ ] Documentation updated.

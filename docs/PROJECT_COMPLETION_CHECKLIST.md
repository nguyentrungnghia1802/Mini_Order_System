# Verified Completion Snapshot

Last verified: 2026-08-02.

Bootstrap implementation commit: `b2a924d` (`chore(repo): bootstrap Phase 0 standards`).

Current implementation slice: Phase 1 Product Service foundation and catalog API, implemented in `abc9a7a` (`feat(product): add catalog persistence slice`).

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

- Product PATCH/activation/concurrency behavior and Angular Product screens.
- Order and Notification business behavior, entities, migrations, and integration tests.
- Public API contracts and YARP business routes.
- MassTransit producer/consumer and transactional outbox.
- Application Dockerfiles and full-stack Compose services.
- Docker image build validation.
- CI execution on GitHub; the workflow is committed but has not been observed remotely from this local run.

Security note: Vitest was upgraded to `4.1.10` during verification to remove a critical development-time advisory. The remaining full-audit results are three moderate development-tool advisories in the Angular CLI dependency tree; no production dependency is affected, and no safe fix is available within the pinned Angular 22.1.2 line.

## Next recommended slice

Phase 1 — Product PATCH/activate-deactivate plus update/concurrency tests, then Angular catalog/operator screens.

## Phase 1 — Product Service foundation (partial)

| Area | Status | Verified evidence |
| --- | --- | --- |
| Product domain and validation | `[x]` | Product entity contains identity, bounded text, decimal VND price, stock, active state, UTC timestamps, and explicit `version` concurrency token. Unit validation tests pass. |
| Product database and migration | `[x]` | Product DbContext/configuration, `20260801194513_InitialProductSchema`, `products` constraints, and `ix_products_active_name_id` are committed. Fresh PostgreSQL migration passes. |
| Product seed | `[x]` | Explicit PowerShell/shell seed scripts insert four deterministic products idempotently. |
| Product catalog/create API | `[x]` | Service-native list/detail/create endpoints, pagination, active filtering, Problem Details, stable codes, and development OpenAPI are tested. |
| Product update/activation | `[ ]` | PATCH and activate/deactivate remain the next slice. |
| Product PostgreSQL integration tests | `[x]` | 7 Product tests pass using PostgreSQL Testcontainers; no EF InMemory provider. |
| Product Angular screens | `[ ]` | Deferred until the service contract is stable and the intended Gateway route exists. |
| Phase 1 validation gate | `[~]` | Product service/migration/OpenAPI/tests pass; Gateway/Angular and remaining Product behavior are incomplete. |

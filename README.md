# MicroShop — Mini Order System

MicroShop is a deliberately small learning project for Angular, ASP.NET Core, YARP, PostgreSQL, RabbitMQ, synchronous HTTP, asynchronous events, database-per-service ownership, idempotency, and distributed failure handling. It is not a production e-commerce platform.

## Current status

Phase 0 bootstrap is implemented as a runnable repository foundation. The .NET hosts currently expose only bootstrap and health endpoints; Product, Order, Notification business behavior and full-stack application containers are added in later roadmap phases.

## Target architecture

```text
Angular -> YARP Gateway -> Product Service -> Product DB
                       \-> Order Service -> Order DB
                                      \-> HTTP Product Service
                                      \-> RabbitMQ -> Notification Service -> Notification DB
```

Each business service owns its own database. Services do not share EF entities, business logic, connection strings, tables, joins, or cross-service transactions.

## Prerequisites

- .NET SDK `10.0.302` (pinned in `global.json`)
- Node.js `24.15.0` and npm `11.12.1` (pinned in `.nvmrc` and `package.json`)
- Docker Desktop with Docker Compose

The local validation environment used for this bootstrap has .NET SDK 10.0.302, Node 24.15.0, Docker Compose 2.40.3, and Docker 28.5.2.

## Run the bootstrap

From the repository root:

```powershell
Copy-Item .env.example .env

dotnet restore
dotnet format MicroShop.sln --verify-no-changes
dotnet build --configuration Release
dotnet test --configuration Release

Push-Location web/microshop-ui
npm ci
npm run lint
npm run test -- --watch=false
npm run build
Pop-Location

docker compose --env-file .env -f deploy/compose.yaml config
docker compose --env-file .env -f deploy/compose.yaml up -d
docker compose --env-file .env -f deploy/compose.yaml ps
```

The Phase 0 Compose file starts PostgreSQL and RabbitMQ only. PostgreSQL initialization creates separate logical databases and users for Product, Order, and Notification; service migrations are deliberately deferred to their owning phases. The RabbitMQ management UI is available at `http://localhost:15672` for local learning.

For native application debugging, use the non-default override to publish PostgreSQL and RabbitMQ application ports:

```powershell
docker compose --env-file .env -f deploy/compose.yaml -f deploy/compose.override.yaml up -d
```

Do not commit `.env`. The committed `.env.example` contains placeholders only. Do not use volume-removal/reset commands without explicit permission.

## Repository layout

```text
MicroShop.sln
src/
  Gateway/MicroShop.Gateway/
  BuildingBlocks/MicroShop.Contracts/
  BuildingBlocks/MicroShop.ServiceDefaults/
  Services/ProductService/MicroShop.ProductService/
  Services/OrderService/MicroShop.OrderService/
  Services/NotificationService/MicroShop.NotificationService/
tests/MicroShop.Architecture.Tests/
web/microshop-ui/
deploy/
scripts/
docs/
.github/workflows/ci.yml
```

## Canonical documentation

- [Agent instructions](docs/agent/AGENT.md)
- [Implementation task checklist](docs/agent/task.md)
- [Verified completion snapshot](docs/PROJECT_COMPLETION_CHECKLIST.md)
- [Project context](docs/project/00_PROJECT_CONTEXT.md)
- [Product requirements](docs/project/01_PRODUCT_REQUIREMENTS.md)
- [System architecture](docs/project/02_SYSTEM_ARCHITECTURE.md)
- [Domain and flows](docs/project/03_DOMAIN_AND_FLOWS.md)
- [Database rules](docs/project/04_DATABASE.md)
- [API contracts](docs/project/05_API.md)
- [Codebase guide](docs/project/06_CODEBASE_GUIDE.md)
- [Development and testing](docs/project/07_DEVELOPMENT_AND_TESTING.md)
- [Deployment and operations](docs/project/08_DEPLOYMENT_AND_OPERATIONS.md)
- [Roadmap and ADRs](docs/project/09_ROADMAP_AND_DECISIONS.md)

The roadmap order is Phase 0 repository bootstrap, Phase 1 Product Service, Phase 2 Order foundation, Phase 3 synchronous inventory communication, Phase 4 Gateway, Phase 5 messaging/Notification, Phase 6 full Compose, Phase 7 reliability, and Phase 8 observability/quality. Authentication remains optional Phase 9.

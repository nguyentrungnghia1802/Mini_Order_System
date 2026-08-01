# Project Context

Last reviewed: 2026-08-02.

## 1. Purpose

Mini Order System, repository name **MicroShop**, is a deliberately small learning project for understanding how an Angular frontend communicates with a distributed .NET backend.

The project is not intended to become a production e-commerce platform. Its value is educational: one user action should cross an API Gateway, multiple independently deployed services, separate databases, and a message broker so that the learner can observe both synchronous and asynchronous service communication.

The central learning flow is:

```text
Angular
  |
  v
YARP API Gateway
  |
  v
Order Service --HTTP--> Product Service
  |
  +--RabbitMQ event--> Notification Service
```

## 2. Problem

Microservices concepts are often learned as isolated definitions:

- API Gateway
- service-to-service HTTP
- message broker
- event producer and consumer
- database per service
- eventual consistency
- retries, timeouts, idempotency, and tracing

Without a complete but bounded system, these concepts are difficult to connect. Large business projects introduce too much domain complexity, while a single API cannot demonstrate distributed-system behavior.

Mini Order System provides one small ordering domain that is large enough to demonstrate the architecture but small enough to understand end to end.

## 3. Target users

| Actor | Need |
| --- | --- |
| Learner/developer | Build, run, inspect, break, and repair a complete .NET microservices flow |
| Demo shopper | Browse products, submit an order, view order status, and see generated notifications |
| Demo operator | Create/update products, inspect stock, inspect orders, and observe service health |
| Automated test runner | Start isolated infrastructure and verify service contracts and failure behavior |
| System operator | Run the stack locally or on one server, inspect logs, RabbitMQ, databases, and health endpoints |

The "demo operator" is not a secured production administrator. Authentication and authorization are intentionally excluded from the first learning baseline.

## 4. Product goals

1. Teach ASP.NET Core Web API through independently running services.
2. Teach Angular through a small but complete client application.
3. Demonstrate synchronous HTTP from Order Service to Product Service.
4. Demonstrate asynchronous messaging from Order Service to Notification Service.
5. Demonstrate database ownership: no service reads or writes another service's database.
6. Demonstrate API Gateway routing through YARP.
7. Demonstrate Docker Compose service discovery and local orchestration.
8. Make failure modes visible and reproducible.
9. Keep the domain small enough that architecture remains the main subject.
10. Provide documentation that explains the system from requirements through operations.

## 5. Learning outcomes

After completing the baseline, the learner should be able to explain and demonstrate:

- why Product, Order, and Notification are separate service boundaries;
- how Angular reaches services through one public gateway;
- how Docker DNS resolves service names;
- why Order Service calls Product Service synchronously during order placement;
- why notifications are generated asynchronously;
- what happens when Product Service is unavailable;
- what happens when Notification Service is unavailable;
- why each service owns its own schema/database;
- how idempotent consumers prevent duplicate side effects;
- why distributed transactions are avoided;
- where eventual consistency appears;
- how a correlation/trace identifier follows one request;
- how health checks differ from business success;
- why microservices add operational complexity.

## 6. System scope

### Included in the final learning baseline

| Area | Target behavior |
| --- | --- |
| Product catalog | List, view, create, update, activate/deactivate products |
| Inventory | Store finite stock, reserve stock atomically, release reservation on cancellation |
| Orders | Create order, list orders, view detail, cancel confirmed order |
| Gateway | Route public client traffic to Product and Order services |
| Synchronous communication | Order Service calls Product Service using typed `HttpClient` |
| Asynchronous communication | Order Service publishes `OrderConfirmed`; Notification Service consumes it |
| Notifications | Persist and display simulated notifications; no real email/SMS provider |
| Databases | Product, Order, and Notification data are independently owned |
| Frontend | Product catalog, checkout form, order list/detail, notification list |
| Containers | Dockerfiles and one Docker Compose stack |
| Reliability basics | Timeout, limited retry, idempotency, readiness checks |
| Observability basics | Structured logs, trace/correlation propagation, OpenTelemetry-ready design |
| Tests | Unit, integration, contract, and one end-to-end happy path |

### Optional hardening extension

- transactional outbox in Order Service;
- consumer inbox/deduplication table;
- dead-letter inspection;
- OpenTelemetry collector and trace viewer;
- fault injection tests;
- JWT authentication;
- deployment to a VPS.

These extensions must not be required before the core communication flow works.

## 7. Out of scope

The following are explicitly out of scope for the baseline:

- real payment;
- shopping cart persistence;
- discounts, coupons, taxes, shipping, returns, and refunds;
- customer accounts and role-based authorization;
- real email, SMS, LINE, or push notification providers;
- product images and object storage;
- search engine and recommendations;
- multiple warehouses;
- reservation expiry workers;
- Kubernetes, service mesh, autoscaling, and cloud-managed infrastructure;
- Kafka;
- event sourcing;
- full CQRS;
- distributed saga orchestration;
- micro-frontends;
- high-availability production guarantees.

A later phase may add selected items only when each addition has a clear learning objective.

## 8. Technology baseline

| Concern | Choice |
| --- | --- |
| Backend runtime | .NET 10 LTS |
| API framework | ASP.NET Core |
| Frontend | Angular 22 |
| API Gateway | YARP |
| Persistence | PostgreSQL with Entity Framework Core and Npgsql |
| Messaging | RabbitMQ with MassTransit |
| Container orchestration | Docker Compose |
| API documentation | OpenAPI/Swagger generated by each HTTP service |
| Frontend UI | Angular Material or a minimal accessible component set |
| Testing | xUnit, ASP.NET Core integration testing, Testcontainers, Angular test runner, Playwright |
| Logging | Microsoft.Extensions.Logging with structured console output |
| Tracing | W3C `traceparent`; optional OpenTelemetry exporter |

Package versions should be pinned by the repository and upgraded deliberately. Documentation names architectural dependencies; the project files remain the executable version source of truth.

## 9. Current project status

This documentation describes the intended system before implementation.

| Area | Status |
| --- | --- |
| Requirements and boundaries | Specified |
| Repository | Phase 0 bootstrap implemented; business slices remain planned |
| Gateway | Bootstrap ASP.NET Core/YARP host implemented; public routes planned for Phase 4 |
| Product Service | Independent bootstrap host and health endpoints implemented; Product domain planned for Phase 1 |
| Order Service | Independent bootstrap host and health endpoints implemented; Order domain planned for Phase 2 |
| Notification Service | Independent bootstrap host and health endpoints implemented; consumer/API planned for Phase 5 |
| Angular frontend | Angular 22 strict workspace implemented; feature screens planned |
| PostgreSQL databases | Compose creates three logical databases and users; business migrations planned |
| RabbitMQ integration | Compose management broker implemented; MassTransit integration planned for Phase 5 |
| Docker Compose | PostgreSQL/RabbitMQ infrastructure Compose implemented; full stack planned for Phase 6 |
| Tests | Bootstrap .NET and Angular tests implemented; service/integration/E2E tests planned |
| Deployment | Optional after local completion |

When implementation begins, this table must be updated only from verified runtime/repository behavior. Documentation must not describe planned features as implemented.

## 10. Main technical constraints

1. Each service must be independently startable.
2. Product Service is the only component allowed to mutate product and inventory data.
3. Order Service must not query the Product database.
4. Notification Service must not query the Order database to reconstruct an event.
5. Public browser traffic enters through the Gateway.
6. Internal service endpoints are not exposed as browser-facing contracts.
7. Order placement must never trust browser-supplied price or product name.
8. RabbitMQ delivery is at least once; consumers must tolerate duplicates.
9. No cross-service database transaction is available.
10. Local development must work on Windows with Docker Desktop and on Linux/macOS with Docker.
11. The baseline must remain understandable without Kubernetes or cloud services.
12. Features that hide distributed behavior behind excessive framework abstraction should be avoided.

## 11. Known architectural risks

| Risk | Why it matters | Planned control |
| --- | --- | --- |
| Order saved but inventory reservation fails | Order and Product databases cannot share one transaction | Use explicit order states and record failure |
| Inventory reserved but Order database update fails | Reservation may become orphaned | Use compensating release; later add reconciliation/outbox |
| Event publish fails after order confirmation | Notification may never be created | Add transactional outbox in hardening phase |
| Duplicate message delivery | RabbitMQ/MassTransit may redeliver | Unique message ID and consumer inbox |
| Product Service unavailable | Order creation depends on a synchronous call | Timeout, no blind retry for unsafe calls, clear 503 response |
| Notification Service unavailable | Messages accumulate | Durable queue and readiness/consumer monitoring |
| Shared contracts become shared business logic | Services become tightly coupled | Contracts package contains immutable message DTOs only |
| Too many abstractions | Learning objective becomes hidden | Prefer direct, explicit implementation and small project count |

## 12. Success criteria

The learning baseline is complete when a developer can:

1. run the entire system with one documented Compose command;
2. open Angular through the Gateway;
3. create products and stock;
4. create an order;
5. observe Order Service calling Product Service;
6. observe stock decrease only in Product Service;
7. observe `OrderConfirmed` in RabbitMQ;
8. observe Notification Service consume and persist a notification;
9. view the order and notification in Angular;
10. stop Product Service and explain why order placement fails;
11. stop Notification Service and show that order placement still succeeds;
12. restart Notification Service and show queued message processing;
13. demonstrate that duplicate event handling does not create duplicate notifications;
14. run automated tests;
15. trace a request using the same trace/correlation context across logs.

## 13. Documentation map

| File | Purpose |
| --- | --- |
| `00_PROJECT_CONTEXT.md` | Scope, goals, constraints, status, and success criteria |
| `01_PRODUCT_REQUIREMENTS.md` | Actors, functional requirements, rules, acceptance criteria, and NFRs |
| `02_SYSTEM_ARCHITECTURE.md` | Processes, service boundaries, communication, reliability, and security |
| `03_DOMAIN_AND_FLOWS.md` | Domain model, states, happy paths, and failure paths |
| `04_DATABASE.md` | Database ownership, entities, constraints, transactions, and migrations |
| `05_API.md` | Public/internal HTTP contracts, events, errors, and compatibility rules |
| `06_CODEBASE_GUIDE.md` | Repository layout, dependency rules, naming, and change workflows |
| `07_DEVELOPMENT_AND_TESTING.md` | Local setup, commands, testing strategy, and troubleshooting |
| `08_DEPLOYMENT_AND_OPERATIONS.md` | Compose deployment, configuration, health, backup, and incident runbooks |
| `09_ROADMAP_AND_DECISIONS.md` | Implementation phases, risks, and accepted ADRs |

## 14. Documentation rules

- Documentation is written in English to align with code, logs, APIs, and common .NET terminology.
- User-facing Angular text may be Vietnamese in the first implementation.
- Source code and executable configuration override prose when they disagree.
- Any discrepancy must be fixed in the same change that discovers it.
- Planned, partial, and implemented behavior must be labeled accurately.
- Major architectural changes require a new ADR instead of silently editing an old decision.

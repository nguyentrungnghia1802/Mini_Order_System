# Roadmap and Decisions

Last reviewed: 2026-08-02.

## 1. Roadmap principles

1. Build visible communication before adding infrastructure sophistication.
2. Keep each phase runnable and testable.
3. Do not add a technology unless it teaches a named concept.
4. Prefer explicit failure states over pretending distributed calls are atomic.
5. Finish the small baseline before expanding the business domain.

## 2. Prioritized implementation roadmap

### Phase 0: Repository and documentation

- create solution and Angular workspace;
- add docs, README, AGENTS;
- pin SDK/package versions;
- add formatting/linting;
- add initial CI build;
- create Compose PostgreSQL/RabbitMQ.

Exit criteria:

- empty projects build;
- infrastructure starts;
- docs are linked from README.

Phase 0 implementation status: the solution/projects, strict Angular workspace, version and formatting standards, initial CI, and PostgreSQL/RabbitMQ Compose infrastructure are now present and validated locally. Empty-database migration validation and application image validation remain deferred because no business migrations or Dockerfiles exist yet.

### Phase 1: Product Service

- Product DB and migrations;
- product CRUD;
- active shopper list;
- seed products;
- integration tests with PostgreSQL;
- Angular catalog/operator form.

Learning objective: one independent .NET service with its own database.

### Phase 2: Order Service without remote inventory

- Order DB and migrations;
- order request/state model;
- temporary fake Product client;
- order list/detail;
- Angular checkout/order pages.

Learning objective: separate service ownership and HTTP API boundaries.

### Phase 3: Synchronous service communication

- Product internal reservation/release endpoints;
- Order typed `HttpClient`;
- Docker DNS;
- timeout and error mapping;
- idempotent reservation;
- concurrency tests;
- cancellation flow.

Learning objective: request/response coupling and distributed outcome states.

### Phase 4: YARP API Gateway

- Gateway project;
- product/order routes;
- path transforms;
- CORS;
- trace forwarding;
- route tests;
- Angular uses one public origin.

Learning objective: one entry point and hidden service topology.

### Phase 5: RabbitMQ and Notification Service

- versioned event contract;
- MassTransit producer/consumer;
- durable queue;
- Notification DB;
- duplicate suppression;
- notification read API/UI;
- failure/restart exercise.

Learning objective: asynchronous event-driven communication and at-least-once delivery.

### Phase 6: Docker Compose completion

- Dockerfiles;
- full-stack Compose;
- health checks;
- startup/migration scripts;
- non-public internal ports;
- one-command demo.

Learning objective: process isolation, service discovery, and operations.

### Phase 7: Reliability hardening

- transactional outbox;
- outbox dispatcher;
- consumer retry/error queue;
- readiness refinement;
- reconciliation helper for unknown inventory;
- failure-injection tests.

Learning objective: DB/broker dual-write, idempotency, and recovery.

### Phase 8: Observability and quality

- structured logs;
- W3C trace propagation;
- OpenTelemetry optional stack;
- Playwright E2E;
- CI container smoke tests;
- integration with existing log monitoring optional.

Learning objective: debugging distributed systems.

### Phase 9: Optional authentication

Only after all core phases:

- simple Identity service or external identity provider;
- Gateway/JWT validation;
- secure operator endpoints;
- user-owned orders.

This phase is optional because authentication can distract from the initial microservices learning goal.

## 3. Completion definition

The project is "complete for learning" at the end of Phase 8. Phase 9 and business expansion are not required.

## 4. Technical debt and risk register

| ID | Issue | Impact | Planned control |
| --- | --- | --- | --- |
| TD-001 | Direct publish after DB commit in early phase | Lost notification window | Phase 7 outbox |
| TD-002 | No public order idempotency key | Browser retry may duplicate orders | Disable duplicate UI submit; optional extension |
| TD-003 | No automated reconciliation initially | `inventory_unknown` requires manual inspection | Phase 7 helper/job |
| TD-004 | No authentication | Public demo operator writes are unsafe | Local-only baseline; optional Phase 9 |
| TD-005 | One PostgreSQL server locally | Learner may confuse server with shared DB | Separate DB/user and docs/tests |
| TD-006 | Shared contracts may grow | Tight coupling | Strict contract-only rule |
| TD-007 | Notification is simulated | No provider delivery learning | Explicit scope; later adapter if desired |
| TD-008 | Polling notification UI | Not realtime | Accept for baseline |
| TD-009 | Compose is not production orchestration | False production confidence | Production gaps documented |
| TD-010 | Product price can change during order flow | Snapshot consistency concerns | Reservation returns authoritative snapshot |
| TD-011 | Cancellation has remote ambiguity | Incorrect final status risk | `cancellation_pending` and idempotent release |
| TD-012 | Error queue replay is manual | Operational burden | Intended learning exercise |

## 5. ADR format

Each accepted decision records:

- **Status**
- **Context**
- **Decision**
- **Consequences**

Do not silently reverse an accepted ADR. Add a superseding ADR.

## ADR-001: Use a learning-focused ordering domain

**Status:** Accepted

**Context:** The purpose is understanding .NET, Angular, and microservices, not building a complete commercial system.

**Decision:** Use Product, Order, and Notification as the only baseline business contexts.

**Consequences:** The project remains small. Payment, shipping, users, discounts, and fulfillment are excluded.

## ADR-002: Use .NET 10 LTS and Angular 22

**Status:** Accepted

**Context:** A new project should use a current supported .NET LTS and current Angular baseline, while repository pinning prevents accidental drift.

**Decision:** Start with .NET 10 LTS and Angular 22, subject to exact compatible versions pinned in `global.json`, project files, `package.json`, and lockfiles.

**Consequences:** Documentation and CI must be updated deliberately during upgrades. Older tutorials may require adaptation.

## ADR-003: Use three independently owned services

**Status:** Accepted

**Context:** One API cannot teach service-to-service communication, while too many services obscure the learning objective.

**Decision:** Use Product Service, Order Service, and Notification Service. Gateway is infrastructure, not a business service.

**Consequences:** The project demonstrates both HTTP and messaging with minimal domain breadth.

## ADR-004: Use YARP as the API Gateway

**Status:** Accepted

**Context:** Angular needs one public entry point and the project should stay within the .NET ecosystem.

**Decision:** Use an ASP.NET Core YARP Gateway configured with explicit routes/clusters.

**Consequences:** Gateway routing and failure become operational concerns. Business rules remain in services.

## ADR-005: Use synchronous HTTP for inventory reservation

**Status:** Accepted

**Context:** Order confirmation cannot be returned until the system knows whether stock was reserved.

**Decision:** Order Service calls Product Service through a typed HTTP client and waits for an authoritative result.

**Consequences:** Product availability affects order latency/availability. Timeout, idempotency, and ambiguous outcomes must be modeled.

## ADR-006: Use asynchronous events for notifications

**Status:** Accepted

**Context:** Notification creation is not required to confirm the order and should not increase synchronous coupling.

**Decision:** Publish `OrderConfirmedV1` to RabbitMQ; Notification Service consumes it.

**Consequences:** Notification is eventually consistent, delivery is at least once, and consumer idempotency is required.

## ADR-007: Each service owns its database

**Status:** Accepted

**Context:** Shared databases undermine service autonomy and hide integration boundaries.

**Decision:** Product, Order, and Notification use separate databases and credentials. One PostgreSQL container may host them locally.

**Consequences:** No cross-service joins/transactions. Data duplication through snapshots/events is intentional.

## ADR-008: Persist order before remote reservation

**Status:** Accepted

**Context:** A remote call can time out after the remote service commits. An order ID and state are needed for diagnosis/idempotent reconciliation.

**Decision:** Order Service creates `pending_inventory` before calling Product Service.

**Consequences:** Failed requests may leave rejected/unknown order records. This is intentional operational evidence.

## ADR-009: Product Service returns authoritative commercial snapshots

**Status:** Accepted

**Context:** Browser prices are untrusted and Order Service does not own the current product catalog.

**Decision:** Reservation response returns product name, unit price, quantity, subtotals, and total.

**Consequences:** Order stores immutable snapshots; later product changes do not rewrite history.

## ADR-010: Reserve multiple items atomically inside Product Service

**Status:** Accepted

**Context:** Partial stock decrement would create an invalid order.

**Decision:** Product Service validates and reserves all requested items in one local transaction with concurrency-safe locking.

**Consequences:** Product transaction complexity increases, but stock correctness is clear and testable.

## ADR-011: Use explicit unknown states for ambiguous remote outcomes

**Status:** Accepted

**Context:** Timeout does not prove that Product Service did not commit.

**Decision:** Use `inventory_unknown` and `cancellation_pending` instead of assuming success or failure.

**Consequences:** Reconciliation is required for rare ambiguous cases. The project teaches a real distributed-systems problem.

## ADR-012: Use RabbitMQ with MassTransit

**Status:** Accepted

**Context:** The project needs a visible broker and a .NET-friendly abstraction over topology, serialization, retries, and consumers.

**Decision:** Use RabbitMQ transport with MassTransit.

**Consequences:** Broker concepts remain visible through RabbitMQ management, while repetitive client plumbing is reduced. Framework defaults must still be understood.

## ADR-013: Treat message delivery as at least once

**Status:** Accepted

**Context:** Broker redelivery and consumer retry can repeat messages.

**Decision:** Notification Service uses durable message-ID deduplication.

**Consequences:** Side effects must be transactional with consumed-message state. Exactly-once claims are avoided.

## ADR-014: Add transactional outbox after the first messaging milestone

**Status:** Accepted

**Context:** Direct publish is easier to learn first but creates a database/broker dual-write gap.

**Decision:** Implement direct publish to demonstrate the problem, then add an Order DB outbox as the final hardening baseline.

**Consequences:** The learner observes why the pattern exists instead of adding it mechanically. Final reliability is stronger.

## ADR-015: Keep shared code narrow

**Status:** Accepted

**Context:** A large shared library couples services and can become a disguised monolith.

**Decision:** Share only integration event contracts and small technical service defaults.

**Consequences:** Some DTO mapping/utility duplication is acceptable.

## ADR-016: Use pragmatic vertical slices inside each service

**Status:** Accepted

**Context:** Full Clean Architecture per tiny service would create too many projects and abstractions.

**Decision:** Start with one deployable project per service using feature folders, persistence, and infrastructure sections.

**Consequences:** Boundaries stay understandable. Extraction into more assemblies is allowed only with evidence.

## ADR-017: Use RFC 7807 Problem Details

**Status:** Accepted

**Context:** Clients need consistent, standard errors with stable machine-readable codes.

**Decision:** Return Problem Details plus `code`, `traceId`, and field errors.

**Consequences:** Angular can map errors consistently. Success responses remain direct typed resources.

## ADR-018: No authentication in the core baseline

**Status:** Accepted

**Context:** Identity introduces another service/domain and distracts from HTTP/messaging/database ownership.

**Decision:** Run as a local/demo system without security principals. Clearly label operator actions as unsecured.

**Consequences:** Do not expose write APIs publicly without adding authentication. Authentication remains an optional phase.

## ADR-019: Use polling for asynchronous UI visibility

**Status:** Accepted

**Context:** SignalR/WebSocket is unrelated to the core broker lesson and adds another realtime path.

**Decision:** Angular refreshes/polls notification data with a bounded interval/manual refresh.

**Consequences:** Notification UI has small latency. Broker flow remains the only asynchronous backend mechanism to learn first.

## ADR-020: Use Docker Compose before Kubernetes

**Status:** Accepted

**Context:** Compose demonstrates containers, DNS, networks, volumes, health, and process isolation with much lower operational overhead.

**Decision:** Make Compose the canonical local/demo orchestration.

**Consequences:** Kubernetes concepts are deferred. Compose behavior must not be described as automatic production scaling.

## ADR-021: Use one PostgreSQL server with separate databases locally

**Status:** Accepted

**Context:** Three PostgreSQL containers consume extra resources without adding much learning value.

**Decision:** Use one local PostgreSQL server, three databases, three users, and independent migrations.

**Consequences:** Ownership is logical/credential-enforced. Production can separate servers later.

## ADR-022: No hard product deletion

**Status:** Accepted

**Context:** Historical snapshots and reservation references make deletion unnecessary for the baseline.

**Decision:** Products are activated/deactivated.

**Consequences:** Catalog lifecycle is simpler and historical meaning remains intact.

## ADR-023: Do not use EF Core InMemory for relational correctness

**Status:** Accepted

**Context:** InMemory does not reproduce PostgreSQL transactions, constraints, and locking.

**Decision:** Use PostgreSQL Testcontainers for integration/concurrency tests.

**Consequences:** Tests are slower but meaningful. Pure unit tests remain fast.

## ADR-024: Notification event is self-contained

**Status:** Accepted

**Context:** A consumer calling Order Service for message data introduces synchronous coupling and failure.

**Decision:** `OrderConfirmedV1` includes destination, total, and item snapshots needed to create the simulated notification.

**Consequences:** Event payload duplicates data intentionally and must avoid sensitive/excessive fields.

## 6. Open decisions

| ID | Decision | Default until resolved |
| --- | --- | --- |
| OD-001 | Minimal APIs vs controllers | Minimal APIs/vertical slices |
| OD-002 | `xmin` vs explicit version column | Explicit version unless Npgsql mapping is clearer |
| OD-003 | MassTransit EF outbox vs custom teaching outbox | Choose based on learning clarity |
| OD-004 | Notification Service as Worker only or Worker+API | Worker+small read API |
| OD-005 | Angular Material vs minimal CSS | Angular Material if it does not dominate setup |
| OD-006 | OpenTelemetry backend | Optional Jaeger/Tempo stack |
| OD-007 | Order public idempotency key | Deferred |
| OD-008 | Internal service authentication | Deferred for local baseline |
| OD-009 | Cancellation event | Deferred |
| OD-010 | Reconciliation endpoint vs background job | Manual/internal endpoint first |

Open decisions must be resolved before their implementation phase, not all at project start.

## 7. Expansion guardrails

A proposed feature is accepted only when:

1. the core baseline is already working;
2. it teaches a named concept not already demonstrated;
3. it does not require splitting a service without a domain/scaling reason;
4. docs and tests can explain its failure behavior;
5. it does not turn the project into a generic full e-commerce product.

Useful extensions:

- JWT/Identity to learn distributed authorization;
- gRPC alternative for the Product internal contract;
- Redis cache to learn cache invalidation;
- Saga for payment/fulfillment only after the simple consistency model is mastered;
- Kubernetes after Compose operations are understood.

Low-value scope expansion:

- many product fields;
- marketing pages;
- coupons;
- reviews;
- dozens of CRUD tables;
- AI recommendation added only for appearance.

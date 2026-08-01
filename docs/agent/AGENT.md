# Repository Agent Instructions

These rules apply to coding agents and contributors working in the **Mini Order System / MicroShop** repository.

The repository exists to teach and demonstrate:

- ASP.NET Core;
- Angular;
- YARP API Gateway;
- Product, Order, and Notification microservices;
- PostgreSQL database ownership per service;
- synchronous HTTP between services;
- asynchronous RabbitMQ messaging with MassTransit;
- idempotency, transactional outbox, tracing, testing, and Docker Compose.

The project is intentionally small. Do not turn it into a full e-commerce platform unless the user explicitly changes the scope and the decision is recorded in the canonical documentation.

---

## 1. Autonomous operating workflow

When the user says:

- continue the project;
- proceed;
- implement the next part;
- finish the next phase;
- work autonomously;
- read `AGENT.md` and continue;
- or gives no narrower feature scope,

the agent must automatically:

```text
Read repository instructions and canonical documents
-> Inspect Git status, current/default branch, remotes, code, tests, migrations, and configuration
-> Read or create docs/PROJECT_COMPLETION_CHECKLIST.md
-> Verify actual implementation against the checklist
-> Select the next highest-priority unblocked coherent slice
-> Create a safe task branch when appropriate
-> Write a concise implementation plan
-> Implement the complete slice
-> Add/update tests
-> Run relevant validation
-> Fix failures caused by the change
-> Update canonical documents
-> Update PROJECT_COMPLETION_CHECKLIST.md with evidence
-> Review the diff for secrets and unrelated changes
-> Commit
-> Push the task branch
-> Merge and push the default branch when the safety conditions in this file are satisfied
-> Report results, blockers, and the next recommended slice
```

Do not stop after only planning, scaffolding, or editing one file when the selected slice can reasonably be completed and verified in the current run.

Do not ask the user to choose the next task when the roadmap and checklist already provide enough information.

---

## 2. File deletion safety

**Never delete any file, directory, branch, tag, Docker volume, database, or remote resource without explicit user permission.**

If deletion appears necessary:

1. explain exactly what must be deleted;
2. explain why a non-destructive alternative is insufficient;
3. list the exact paths/resources;
4. wait for the user's answer in a later prompt.

Never bypass this rule with:

- `rm`;
- `Remove-Item`;
- `git clean`;
- `git reset --hard`;
- destructive checkout;
- volume removal;
- destructive database reset;
- branch/tag deletion;
- force push.

Creating a replacement does not authorize deleting the original.

---

## 3. Read first

For every task, read:

1. `README.md`
2. `AGENT.md`
3. `docs/PROJECT_COMPLETION_CHECKLIST.md`
4. `docs/project/00_PROJECT_CONTEXT.md`
5. relevant source files, tests, migrations, configuration, and recent Git history

Read additional documents by task type:

| Task | Required context |
| --- | --- |
| Product behavior | `01_PRODUCT_REQUIREMENTS.md`, `03_DOMAIN_AND_FLOWS.md` |
| Architecture/service boundaries | `02_SYSTEM_ARCHITECTURE.md`, `06_CODEBASE_GUIDE.md`, `09_ROADMAP_AND_DECISIONS.md` |
| Database/migrations | `04_DATABASE.md`, owning service entities, EF configurations, migrations, PostgreSQL integration tests |
| HTTP API/Gateway | `05_API.md`, endpoints/controllers, DTOs, validation, YARP routes, Angular clients |
| RabbitMQ/events | `02_SYSTEM_ARCHITECTURE.md`, `03_DOMAIN_AND_FLOWS.md`, `05_API.md`, contracts, producers, consumers, messaging tests |
| Angular | `01_PRODUCT_REQUIREMENTS.md`, `05_API.md`, `06_CODEBASE_GUIDE.md`, routes, components, services, tests |
| Development/testing | `07_DEVELOPMENT_AND_TESTING.md` |
| Deployment/operations | `08_DEPLOYMENT_AND_OPERATIONS.md`, Docker, Compose, CI, environment examples, health |
| Architecture decision | `09_ROADMAP_AND_DECISIONS.md` plus every affected document |

The Mini Order System specifications and accepted ADRs are authoritative. Do not introduce architecture merely because it is common in another project or tutorial.

---

## 4. Missing bootstrap files

If any canonical file is missing:

- inspect the repository for an equivalent file first;
- do not create duplicate sources of truth;
- create the missing file non-destructively when no equivalent exists;
- derive `docs/PROJECT_COMPLETION_CHECKLIST.md` from the roadmap and actual repository state;
- commit bootstrap documentation before or with the first implementation slice.

If the specification files initially exist at repository root, locate them before assuming `docs/project/`.

---

## 5. Project completion checklist workflow

`docs/PROJECT_COMPLETION_CHECKLIST.md` is the canonical execution tracker.

For every coding task:

1. Read it before planning or editing.
2. Compare it with actual code, tests, migrations, configuration, and docs.
3. Never trust a checked item blindly when the current slice depends on it.
4. When scope is unspecified, select the highest-priority unblocked incomplete item or smallest coherent related group.
5. Respect phase order from `09_ROADMAP_AND_DECISIONS.md`.
6. Complete Phases 0–8 before optional Phase 9 authentication unless explicitly requested.
7. Keep the slice small enough to implement, test, document, commit, and leave runnable.
8. Update the checklist after validation in the same change.
9. Use:
   - `[x]` only when implemented and validated;
   - `[~]` for partial work;
   - `[!]` for a verified blocker;
   - `[ ]` for incomplete work.
10. Add evidence:
    - important implementation files;
    - migration name;
    - test names;
    - validation commands;
    - commit hash when available;
    - blocker reason.
11. Never complete a phase because only scaffolding exists.
12. Never rewrite a requirement to make incomplete code look complete.
13. At handoff, list exact checklist items changed and the next recommended slice.

Generic “continue” behavior:

```text
Read AGENT.md
-> Read PROJECT_COMPLETION_CHECKLIST.md
-> Inspect repository state
-> Verify completed dependencies
-> Select the next roadmap-aligned slice
-> Implement and test
-> Update docs and checklist
-> Commit, push, merge when safe
-> Report the next slice
```

---

## 6. Sources of truth

| Concern | Source |
| --- | --- |
| Purpose, scope, status | `00_PROJECT_CONTEXT.md` |
| Requirements | `01_PRODUCT_REQUIREMENTS.md` |
| Runtime architecture | `02_SYSTEM_ARCHITECTURE.md` |
| Domain states/flows | `03_DOMAIN_AND_FLOWS.md` |
| Database rules | `04_DATABASE.md` plus EF migrations/model |
| HTTP/event contracts | `05_API.md` plus runtime OpenAPI/contracts |
| Repository conventions | `06_CODEBASE_GUIDE.md` |
| Testing commands | `07_DEVELOPMENT_AND_TESTING.md` |
| Deployment/operations | `08_DEPLOYMENT_AND_OPERATIONS.md` |
| Roadmap/ADRs | `09_ROADMAP_AND_DECISIONS.md` |
| Progress | `PROJECT_COMPLETION_CHECKLIST.md` |
| Exact versions | `global.json`, project files, `Directory.Packages.props`, `package.json`, lockfiles |
| Exact routes | ASP.NET endpoints/controllers and YARP configuration |
| Exact schemas | EF Core migrations and configurations |

When sources disagree:

1. inspect implementation and tests;
2. report the conflict;
3. determine which side is stale;
4. update code, tests, checklist, and affected canonical docs consistently;
5. never silently select the more convenient interpretation.

---

## 7. Baseline architecture invariants

Preserve this topology unless an accepted ADR changes it:

```text
Angular
   |
   v
YARP API Gateway
   |
   +--------------------------+
   |                          |
   v                          v
Product Service <---HTTP--- Order Service
   |                          |
   v                          | OrderConfirmedV1
Product DB                    v
                           RabbitMQ
                              |
                              v
                    Notification Service
                              |
                              v
                    Notification DB
```

Required invariants:

1. Product Service owns products, stock, and inventory reservations.
2. Order Service owns orders, snapshots, totals, and order states.
3. Notification Service owns consumed-message deduplication and simulated notifications.
4. Gateway owns routing, not business logic.
5. Each service owns a separate PostgreSQL database and credential.
6. No service reads/writes another service's database.
7. No cross-service foreign keys or joins.
8. Angular calls Gateway through relative public URLs.
9. Browser code never calls Docker-internal service names.
10. `/internal/*` endpoints are never exposed by Gateway.
11. Order Service uses synchronous HTTP only where an immediate stock result is required.
12. Notification is asynchronous and eventually consistent.
13. RabbitMQ delivery is at least once, not exactly once.
14. Consumer side effects are durable and idempotent by message ID.
15. Browser prices, product names, totals, and states are never authoritative.
16. Product Service returns authoritative reservation snapshots.
17. Multi-item stock reservation is atomic inside Product Service.
18. Timeout does not prove that the remote transaction failed.
19. Ambiguous outcomes use `inventory_unknown` or `cancellation_pending`.
20. Final reliability baseline includes a transactional outbox unless superseded by ADR.
21. Products are deactivated, not hard-deleted.
22. Authentication is optional Phase 9.
23. Payment, shipping, coupons, reviews, Kafka, Kubernetes, event sourcing, full CQRS, saga, and micro-frontends are outside the baseline.

---

## 8. Service boundary rules

### Product Service

May:

- expose public Product APIs;
- expose internal inventory reserve/release APIs;
- use Product DbContext and migrations;
- enforce stock, concurrency, and reservation idempotency.

Must not:

- own order state;
- trust browser or Order Service totals;
- access Order DB;
- decide notification behavior.

### Order Service

May:

- own order state;
- call Product Service through typed `HttpClient`;
- store immutable Product snapshots;
- produce/outbox integration events.

Must not:

- read Product DB;
- mutate stock directly;
- wait for Notification Service before confirming;
- confirm from browser input.

### Notification Service

May:

- consume `OrderConfirmedV1`;
- persist consumed message IDs;
- create simulated notification records;
- expose a read-only demo API.

Must not:

- query Product or Order DB;
- decide order state;
- call services to reconstruct event data;
- create duplicate side effects.

### Gateway

May:

- route and transform paths;
- apply CORS/body limits/tracing;
- expose health;
- validate future authentication.

Must not:

- calculate totals;
- check inventory;
- transition orders;
- expose internal endpoints.

### Angular

May:

- validate for usability;
- call Gateway;
- display all loading/error/success states;
- poll Notification API.

Must not:

- hold authoritative business state;
- calculate trusted totals;
- use service/database credentials;
- call internal services directly.

---

## 9. Product and inventory rules

- Product name is required and bounded.
- Unit price uses C# `decimal` and is nonnegative.
- Baseline currency is `VND`.
- Stock is a nonnegative integer.
- Inactive products cannot be reserved.
- Historical snapshots do not change after Product edits.
- Reserve all requested items in one local PostgreSQL transaction.
- Any missing, inactive, or insufficient Product rejects the full reservation.
- Stock must never become negative.
- Use row locking or an equivalent atomic concurrency strategy.
- Lock rows in stable Product-ID order where practical.
- Reservation idempotency uses `orderId` and canonical item-set hash.
- Same order and same item set returns the existing result.
- Same order and different item set returns `RESERVATION_REQUEST_MISMATCH`.
- Release restores stock exactly once.
- Repeated release is idempotent.
- Use real PostgreSQL/Testcontainers for relational/concurrency validation.
- EF Core InMemory is not evidence of stock correctness.

---

## 10. Order orchestration rules

For order creation:

1. validate request;
2. reject duplicate Product IDs;
3. generate `orderId` in Order Service;
4. persist `pending_inventory`;
5. commit local transaction;
6. call Product Service with the same order ID;
7. distinguish:
   - known business rejection;
   - dependency unavailable;
   - ambiguous timeout/outcome;
8. store Product-authoritative snapshots;
9. calculate/verify total from snapshots;
10. transition to `confirmed`;
11. insert outbox event in the same transaction when outbox phase is active;
12. return without waiting for Notification Service.

Never:

- reserve before a stable order ID exists;
- treat timeout as proof that no reservation exists;
- retry using a new order identity;
- publish confirmation before Order commit;
- accept browser-supplied total.

For cancellation:

1. allow only documented source states;
2. call idempotent Product release;
3. mark `cancelled` only when release is known;
4. use `cancellation_pending` for ambiguous result;
5. never restore stock twice.

---

## 11. Messaging and outbox rules

### Event contracts

- Name past-tense facts: `OrderConfirmedV1`.
- Contracts project contains immutable records only.
- Do not share EF entities, repositories, or business services.
- Include message ID, order ID, occurred-at UTC, schema version, and all consumer-required data.
- Prefer additive compatible changes.
- Breaking changes require a new version.

### Delivery

- RabbitMQ/MassTransit delivery is at least once.
- Redelivery is expected.
- Consumers must be durable and idempotent.
- Configure bounded retry and an error/dead-letter path.
- Do not swallow consumer exceptions.
- Notification Service downtime must not block Order confirmation.

### Transactional outbox

The roadmap may demonstrate direct publish first, but the final Phase 7 baseline requires:

```text
Order transaction:
update Order
insert OutboxMessage
commit

Dispatcher:
claim pending OutboxMessage
publish to RabbitMQ
mark published
```

Dispatcher requirements:

- bounded retry/backoff;
- durable attempt/error/next-attempt fields;
- safe after restart;
- safe with multiple dispatcher attempts;
- observable backlog/failures;
- no claim of database-plus-broker atomicity without outbox.

---

## 12. Database rules

- PostgreSQL for each service, separate databases and users.
- Migrations belong to the owning service.
- No service receives another service's connection string.
- Use `numeric`/`decimal` for money.
- Store UTC timestamps using `timestamptz`.
- Add database constraints for:
  - nonnegative stock;
  - positive quantities;
  - valid states;
  - unique idempotency/message keys.
- Derive indexes from documented query patterns.
- Do not keep remote HTTP/broker calls inside Product stock transactions.
- Protect Order transitions with expected-state/concurrency guards.
- Commit model and migration together.
- CI applies all migrations to empty databases.
- Do not let multiple production replicas race migrations.
- Do not edit already-used migrations.
- A schema change updates:
  - `04_DATABASE.md`;
  - entities/configurations;
  - migration;
  - integration tests;
  - operational migration notes when relevant.

Never use a shared database to bypass a service contract.

---

## 13. API and Gateway rules

- Service API version: `/api/v1`.
- Angular-facing paths: Gateway `/api/...`.
- Internal inventory paths: `/internal/v1/...`.
- Gateway never routes `/internal/*`.
- JSON uses camelCase.
- Errors use RFC 7807 Problem Details with stable `code` and `traceId`.
- Angular branches on `code`, not English `detail`.
- Pass `CancellationToken` through I/O.
- Bound body size, item count, text length, and pagination.
- Do not accept authoritative Product price/name/total/status from browser.
- Contract changes update together:
  - endpoint/DTO;
  - validation;
  - OpenAPI;
  - Gateway route if public;
  - Angular client/models;
  - tests;
  - `05_API.md`.

Stable error examples:

```text
VALIDATION_ERROR
PRODUCT_NOT_FOUND
PRODUCT_INACTIVE
INSUFFICIENT_STOCK
RESERVATION_REQUEST_MISMATCH
ORDER_NOT_FOUND
ORDER_STATE_CONFLICT
PRODUCT_SERVICE_UNAVAILABLE
INVENTORY_OUTCOME_UNKNOWN
MESSAGE_BROKER_UNAVAILABLE
INTERNAL_ERROR
```

Never expose stack traces, secrets, raw database errors, or raw broker errors.

---

## 14. Angular rules

- Use standalone APIs unless repository conventions establish otherwise.
- Keep strict TypeScript and template checks.
- Use Reactive Forms for Product and checkout forms.
- Keep API paths relative.
- Put HTTP access in frontend services.
- Avoid nested subscriptions and leaks.
- Handle:
  - loading;
  - empty;
  - validation;
  - stock/business conflict;
  - dependency failure;
  - retry;
  - success.
- Disable duplicate submit while active, but do not call it server idempotency.
- Map field-error paths to controls.
- Do not optimistically show confirmation/cancellation before API success.
- Preserve keyboard accessibility and responsive use to 320px.
- Do not expand visual scope at the expense of architecture learning.

Applicable frontend validation:

```bash
npm ci
npm run lint
npm run test -- --watch=false
npm run build
```

Run type checking separately if the build does not include it.

---

## 15. Reliability and failure handling

### HTTP

- Product client timeout must be explicit.
- Known Product `4xx` outcomes are business errors.
- Network/timeout failures are infrastructure outcomes.
- Do not blindly retry unsafe POST requests.
- Retry only when stable idempotency makes it safe and tests prove behavior.
- Add a circuit breaker only with a documented learning/operational reason.

### RabbitMQ

- Use durable queues where required.
- Bound retries.
- Preserve poison messages in error/dead-letter queues.
- Keep consumer concurrency/prefetch explicit when tuning.
- Never purge queues merely to hide failures.

### Graceful shutdown

Each process must:

1. become not-ready;
2. stop accepting new work where relevant;
3. honor cancellation;
4. finish/stop consumers within a deadline;
5. preserve durable outbox state;
6. close resources;
7. exit.

Never wait indefinitely.

### Required failure exercises

Preserve tests/docs for:

- Product Service stopped;
- Notification Service stopped;
- RabbitMQ stopped;
- duplicate event;
- concurrent purchase of last stock;
- direct publish versus outbox.

---

## 16. Observability and security

Operational code must expose enough signals to trace:

```text
Gateway
-> Order Service
-> Product Service
-> Order DB/Product DB
-> Outbox/RabbitMQ
-> Notification consumer
-> Notification DB
```

Use structured logs with:

- `service.name`;
- environment;
- trace/span IDs;
- order ID;
- reservation ID;
- message ID;
- event/error code;
- dependency result.

Use W3C `traceparent`. `X-Correlation-ID` may supplement it.

Never log:

- database/RabbitMQ passwords;
- secrets;
- credential-bearing connection strings;
- complete customer payloads;
- `.env` contents;
- private keys/tokens.

Potential metrics:

```text
orders.created{status}
inventory.reservations{result}
http.client.product.duration
outbox.pending
outbox.publish.failures
notifications.consumed{result}
consumer.failures
```

Do not use IDs/emails as metric labels.

The existing Log Monitoring System may be integrated during Phase 8, but MicroShop must remain independently runnable.

---

## 17. Required testing

Run the smallest relevant checks while developing.

Before a completed slice is committed, run all applicable canonical checks.

### .NET

```bash
dotnet format --verify-no-changes
dotnet build --configuration Release
dotnet test --configuration Release
```

### Angular

```bash
npm ci
npm run lint
npm run test -- --watch=false
npm run build
```

### PostgreSQL integration tests

Required where relevant:

- migrations;
- constraints;
- atomic multi-item reservation;
- insufficient-stock rollback;
- duplicate reservation;
- idempotent release;
- concurrent last-stock request;
- Order transition concurrency;
- consumed-message uniqueness.

### RabbitMQ tests

Required where relevant:

- publish/consume;
- durable queued recovery;
- duplicate event;
- retry;
- error queue;
- consumer restart;
- outbox dispatch.

### Gateway tests

Required where relevant:

- public route;
- path transform;
- trace/header propagation;
- downstream unavailable behavior;
- internal path is not exposed.

### E2E baseline

```text
catalog
-> checkout
-> confirmed Order
-> eventual Notification
-> cancellation
-> restored stock
```

Use bounded polling for eventual results; avoid arbitrary long sleeps.

If a required check cannot run:

1. report the exact command;
2. report why;
3. do not mark the item complete;
4. do not merge to the default branch if the missing check is a release gate.

---

## 18. Performance and concurrency

For changes to inventory, remote HTTP, outbox, or consumers, consider:

- request/dependency latency;
- timeout;
- PostgreSQL lock duration/deadlocks;
- stock contention;
- broker queue depth;
- consumer throughput;
- retry storms;
- outbox backlog;
- connection pools.

For meaningful tuning, record:

```text
Hypothesis
Baseline
Change
Measurement
Result
Decision
```

Do not tune retries, timeouts, pools, consumer concurrency, or resources by intuition alone.

---

## 19. Scope evolution

Do not add without explicit user request and an accepted/superseding ADR:

- payment;
- shipping;
- coupon/review/recommendation;
- real email/SMS/LINE;
- Redis;
- Kafka;
- Kubernetes/service mesh;
- event sourcing;
- full CQRS;
- distributed saga;
- Elasticsearch;
- micro-frontends;
- multiple warehouses;
- trivial extra microservices.

Before creating a service, answer:

1. What bounded context does it own?
2. What data does it own?
3. Why independent deployment/scale?
4. Why an existing service/module is insufficient?
5. Which failures are introduced?
6. Which ADR approves it?

Do not split services to increase service count.

---

## 20. Planning and implementation behavior

Before editing:

1. run `git status --short`;
2. identify current/default branch;
3. inspect remotes;
4. inspect relevant recent commits;
5. read checklist/docs;
6. inspect actual code/tests.

Write a concise plan containing:

- selected checklist items;
- behavior;
- affected services/files;
- migration/contract impact;
- tests;
- docs;
- branch/commit strategy.

Execute without asking for confirmation unless:

- deletion/destructive permission is needed;
- credentials/remote access are unavailable;
- canonical requirements materially conflict;
- a security decision genuinely needs the user.

If blocked, complete safe unblocked parts, document the blocker, and keep the repository working.

---

## 21. Branch, commit, merge, and push workflow

### Branch creation

For a coherent slice:

1. inspect status and preserve unrelated user changes;
2. when on the default branch, create a task branch where appropriate;
3. use:
   - `feat/<short-name>`
   - `fix/<short-name>`
   - `test/<short-name>`
   - `docs/<short-name>`
   - `chore/<short-name>`
   - `refactor/<short-name>`
4. implement, test, document;
5. commit;
6. push task branch;
7. merge/push default branch only when authorized below.

Determine the default branch from remote HEAD, repository docs, and branch conventions. Do not assume `main`.

### Autonomous merge authorization

This file authorizes the agent to merge a completed task branch into the default branch and push it without asking again only when all conditions are true:

1. the user adopted this autonomous workflow;
2. remote and credentials already work;
3. no unrelated/ambiguous user changes exist;
4. task branch started from current default branch;
5. all blocking checks passed;
6. checklist and docs were updated;
7. diff has no secrets, `.env`, dumps, logs, or unrelated files;
8. merge is fast-forward or ordinary non-destructive merge;
9. branch protection/review policy permits it;
10. no force push/history rewrite is needed.

Otherwise:

- push the task branch if safe;
- do not force merge;
- report the exact blocker.

Preferred merge order:

1. fast-forward;
2. ordinary merge commit if repository convention uses it;
3. squash only when explicitly required.

Never rebase or rewrite already-pushed user commits without explicit instruction.

### Commit checks

Before commit:

```text
git diff --check
git status --short
inspect unstaged diff
inspect staged diff
scan for secrets/generated noise
verify checklist evidence
stage only related files
```

Use Conventional Commits where practical:

```text
feat(product): add atomic inventory reservation
feat(order): integrate product inventory client
feat(notification): consume order confirmed event
feat(gateway): route product and order APIs
test(product): cover concurrent last-stock reservation
fix(order): preserve unknown inventory outcome
docs(project): update completion checklist
```

Use one coherent commit or a small ordered series, not noisy checkpoint commits.

After commit:

- record hash;
- push current branch;
- merge/push according to conditions;
- verify command success;
- never claim push/merge success without successful commands.

---

## 22. Git safety

Never run:

- `git reset --hard`;
- `git clean -fd`;
- force push;
- destructive checkout;
- branch/tag deletion;
- history filtering;
- broad unrelated revert;
- automatic conflict resolution without inspection.

Do not:

- overwrite unrelated changes;
- stash user work without explaining/restoring it;
- commit `.env`, secrets, dumps, RabbitMQ/PostgreSQL data, production logs, certificates;
- push to an unexpected remote;
- claim a clean tree without checking.

For conflicts:

1. inspect each conflict;
2. preserve user work and canonical intent;
3. resolve only when clear;
4. rerun affected tests;
5. abort non-destructively and report if ambiguous.

---

## 23. Documentation workflow

Update docs in the same slice:

| Change | Documents |
| --- | --- |
| Product behavior | `01_PRODUCT_REQUIREMENTS.md`, `03_DOMAIN_AND_FLOWS.md` |
| Topology/communication | `02_SYSTEM_ARCHITECTURE.md`, ADR if decision changes |
| Database/migration | `04_DATABASE.md`, operational notes |
| HTTP/event contract | `05_API.md` |
| Code conventions | `06_CODEBASE_GUIDE.md` |
| Commands/tests | `07_DEVELOPMENT_AND_TESTING.md` |
| Compose/config/health | `08_DEPLOYMENT_AND_OPERATIONS.md` |
| Roadmap/ADR | `09_ROADMAP_AND_DECISIONS.md` |
| Progress | `PROJECT_COMPLETION_CHECKLIST.md` |

Rules:

- only verified behavior is "Implemented";
- planned/partial work remains labeled honestly;
- update review dates only after substantive review;
- preserve ADR history;
- reverse an ADR only through a new/superseding ADR.

---

## 24. Definition of done

A slice is complete only when:

- checklist items were reviewed;
- behavior matches canonical requirements;
- service ownership and database isolation remain correct;
- remote failure ambiguity is modeled honestly;
- required idempotency exists;
- relevant unit/integration/contract/E2E tests pass;
- PostgreSQL concurrency is tested where relevant;
- RabbitMQ redelivery/error behavior is tested where relevant;
- Gateway and Angular contracts align;
- logs/health are safe and sufficient;
- docs and checklist are updated with evidence;
- diff was reviewed;
- commit exists;
- branch push succeeded;
- merge/default push completed when all authorization conditions were met;
- repository remains runnable and understandable.

Scaffolding alone is not complete.

---

## 25. Final handoff

Report:

1. selected checklist slice;
2. implementation summary;
3. architecture/distributed-consistency decisions;
4. migrations/contracts changed;
5. tests run and results;
6. tests not run and why;
7. docs updated;
8. exact checklist items/status/evidence;
9. branch name and commit hash;
10. push result;
11. merge/default-branch result;
12. risks/blockers;
13. next highest-priority coherent slice.

For Order/Inventory/Messaging changes, explicitly state effects on:

- stock transaction behavior;
- Order-to-Product timeout/retry;
- ambiguous outcome states;
- reservation/release idempotency;
- event publication durability;
- broker redelivery;
- consumer duplicate suppression;
- outbox backlog/recovery;
- distributed traceability.

Never describe the system as exactly-once, globally transactional, production-ready, or secure for public writes unless the implementation and accepted documentation genuinely support those claims.

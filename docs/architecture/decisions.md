# Architecture Decision Records

This document captures the key architectural decisions made during the design of Flow, along with the reasoning and trade-offs behind each one. These decisions are fixed for the MVP and should not be revisited without explicit justification.

---

## ADR-001 — Modular Monolith over Microservices

**Status:** Accepted

### Decision
Flow is built as a single deployable unit — a modular monolith — not a set of microservices.

### Context
The system covers several distinct domains: Ideas, Projects, Results, Dashboard, and Gamification. At the design stage, the question arose whether to split these into independent services.

### Reasoning
The modules are tightly coupled at the data level. The Dashboard aggregates across Ideas, Projects, and Results in a single query. Splitting into microservices would require distributed queries, a message bus for audit event propagation, distributed transactions for the atomicity guarantee, and significant operational overhead — for no performance or scalability benefit at the current user count of up to 1,000.

The modular structure within the monolith (Clean Architecture layers, clear module boundaries, command/query separation) ensures that splitting services later is a contained refactor, not a rewrite.

### Trade-offs
- A bug in one module requires redeploying the entire API
- A single large codebase instead of independently deployable units
- All modules must be scaled together even if only one is under load

### Accepted because
At MVP scale these trade-offs are immaterial. The modular structure preserves future optionality.

---

## ADR-002 — Hybrid Traceability (AuditLog + Snapshot) over Event Sourcing

**Status:** Accepted

### Decision
All state changes are recorded in an append-only `AuditLog`. Project state is additionally captured as a full `ProjectSnapshot` at every transition. Full event sourcing was not adopted.

### Context
Governance and traceability are first-class requirements. The system must be able to answer "who approved this idea, when, and why?" and "what was the exact state of this project last Tuesday?" for any record, indefinitely.

### Reasoning
Full event sourcing delivers these capabilities but requires: an event store, event replay infrastructure, projection handlers, and consistency guarantees across projections. This is significant complexity for a team operating at MVP speed.

The hybrid approach delivers the governance guarantees without the operational overhead:
- `AuditLog` answers the *who/what/when/why* question for any entity
- `ProjectSnapshot` answers the *what was the state at time T* question for projects
- Both are written atomically with the state change — no eventual consistency, no partial writes

`ProjectSnapshot` is stored as structured fields rather than a JSON blob, which allows dashboard queries (e.g. "blocked since when") to be expressed in SQL without deserialization.

### Trade-offs
- Adding a new field to `Project` requires a migration on `ProjectSnapshot` as well
- Audit and snapshot writes are synchronous and add latency to each command
- The system cannot replay events to rebuild a different projection

### Accepted because
The maintenance overhead is low. The latency added by one extra row write per command is immeasurable at current scale. The inability to replay events is acceptable because the snapshot model already provides point-in-time reconstruction.

---

## ADR-003 — No Redis Cache

**Status:** Accepted

### Decision
No distributed cache is used in the MVP. All reads hit the database directly.

### Context
The Dashboard endpoint aggregates across six tables on every request. The question arose whether to cache the result.

### Reasoning
At 1,000 users, dashboard queries complete in single-digit milliseconds against an indexed Azure SQL database. Introducing Redis adds: connection management, cache invalidation logic, a new infrastructure component to operate, and a new failure mode (stale data). The benefit does not justify the complexity.

The `ICacheService` interface is already defined in the Application layer as a no-op. When caching becomes necessary, a Redis implementation can be swapped in with no changes to application logic.

### Trade-offs
- Every dashboard request hits the database
- No protection against a dashboard polling spike

### Accepted because
The `ICacheService` seam ensures this is reversible at low cost. The current access patterns do not require a cache.

---

## ADR-004 — No Background Jobs

**Status:** Accepted

### Decision
All side effects (audit log writes, snapshot creation) are executed synchronously within the command handler's database transaction. No background job framework (Hangfire, Quartz.NET) is used.

### Context
Every project state transition must produce both an `AuditLog` entry and a `ProjectSnapshot`. The question was whether these writes should be asynchronous (queued for a background worker).

### Reasoning
Asynchronous side effects introduce a partial-failure scenario: the state change commits but the audit write fails. This silently violates the governance model — the system would show a project as Blocked with no audit record of the blocking. This is unacceptable.

Synchronous writes inside a single transaction guarantee atomicity. If the audit write fails, the entire command fails and the state change is rolled back. The system never reaches an inconsistent state.

### Trade-offs
- Each command has slightly higher latency than a write-only approach
- No retry mechanism for transient failures (the entire command must be retried)

### Accepted because
The latency cost is a database round trip — immeasurable at current scale. The atomicity guarantee is a core governance requirement and cannot be traded away.

---

## ADR-005 — CQRS via MediatR (Lightweight)

**Status:** Accepted

### Decision
Commands and queries are separated using the CQRS pattern, implemented with MediatR. There is no separate read model or read database.

### Context
CQRS can be implemented across a spectrum — from logical separation (same database, different handlers) to full physical separation (separate read and write databases). The decision was which point on this spectrum to occupy.

### Reasoning
Full physical CQRS (separate databases, eventual consistency) is not warranted at current scale. However, logical CQRS delivers significant benefits: clear separation of intent, easy testability of handlers in isolation, and a clean foundation for introducing a read replica later without restructuring the codebase.

MediatR provides the dispatch mechanism with minimal overhead. Each command and query is a self-contained unit — easy to locate, test, and modify.

### Trade-offs
- An additional indirection layer (controller → mediator → handler)
- MediatR pipeline adds a small per-request overhead

### Accepted because
The indirection is low-cost and the structural benefits (testability, isolation, clear intent) outweigh it. The codebase currently has 120+ tests that rely on this structure.

---

## ADR-006 — Role-Based Access at Both Route and Data Level

**Status:** Accepted

### Decision
Authorization is enforced in two places: at the route (`[Authorize(Roles = "...")]`) and at the query level (data scoping by caller's role).

### Context
Operators should only see their own ideas. Managers and Leadership should see all ideas. The question was where to enforce this.

### Reasoning
Enforcing only at the route level is insufficient — an Operator routed to `GET /ideas` would receive all ideas if the query was not also scoped. The scoping must happen in the query handler, which receives the current user's role and filters accordingly.

This approach means no data leaks regardless of future routing changes. The query handler is the authoritative enforcer of data access policy, not the route definition.

### Trade-offs
- Data scoping logic lives in query handlers, not a centralised policy
- Each query handler must be aware of role-based scoping rules

### Accepted because
The alternative (central policy engine) is premature for the current role set. Each handler's scoping rule is simple, testable, and co-located with the query logic it governs.

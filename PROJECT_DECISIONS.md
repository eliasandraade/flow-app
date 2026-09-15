# Flow — Project Decisions

This document records the current product decisions, architectural choices, scope boundaries, and technical constraints for Flow. It is the authoritative project-level reference for decisions that should remain stable unless explicitly revised.

---

## Product Definition

Flow is a corporate innovation lifecycle management platform. It provides a structured and auditable way for organizations to capture operational problems as ideas, evaluate and approve them, convert approved ideas into projects, track execution, and measure business outcomes.

The product is designed around governance and traceability rather than an unstructured idea repository.

### Core Pipeline

```text
IDEA → ANALYSIS → APPROVAL → PROJECT → EXECUTION → RESULT
```

Every significant transition is persisted so the system can explain what changed, who changed it, when it changed, and why.

---

## Users and Roles

| Role | Responsibility |
|---|---|
| Operator | Submit ideas based on operational problems and follow their progress |
| Manager | Review ideas, approve or reject them, create projects and manage execution |
| Leadership | Define strategic direction, monitor KPIs and evaluate business outcomes |

---

## State Machines

### Idea Lifecycle

```text
Draft
  ↓ submit
UnderReview
  ├─→ Approved
  └─→ Rejected
```

Rules:

- Managers decide approval or rejection.
- Rejection requires contextual reasoning in the audit trail.
- Approved ideas may be converted into projects.

### Project Lifecycle

```text
Planned
  ├─→ InProgress
  └─→ Blocked

InProgress
  ├─→ Completed
  ├─→ Cancelled
  └─→ Blocked

Blocked
  ├─→ InProgress
  └─→ Cancelled
```

Rules:

- `BlockedReason` is required on every transition into `Blocked`.
- `AuditLog.Reason` is required for reject, cancel, and block transitions.
- Invalid transitions are rejected by the domain model and do not persist.
- `CompletedAt` is set automatically on completion.

---

## Confirmed Technical Decisions

| Concern | Decision | Rationale |
|---|---|---|
| Tenancy | Single organization | MVP does not require tenant isolation |
| Architecture | Modular monolith with Clean Architecture | Fast delivery, clear boundaries, decomposable later |
| Backend | ASP.NET Core 8 / C# | Strong domain modeling and enterprise ecosystem |
| Data access | `MongoDB.Driver` 3.11.1, official driver | Document model fits the read patterns; no EF provider over Mongo |
| Database | MongoDB 8.0, **replica set required** | Multi-document transactions are what keep aggregate, audit and snapshot atomic |
| Mobile | React Native with Expo | Cross-platform primary client |
| Web | React + Vite | Leadership dashboard only in MVP |
| Authentication | ASP.NET Core Identity + JWT | Email/password first with future SSO seam |
| Traceability | Append-only AuditLog + synchronous ProjectSnapshot | Governance-grade history without event-sourcing complexity |
| Snapshots | Same transaction as project transition | Guarantees consistency |
| ROI | Manual entry | Avoids premature ERP integration |
| ROI phases | Estimated and actual stored independently | Preserves planning vs. realized outcome |
| Dashboard | Conversion, completion time, ROI, status distribution, blockers | Leadership visibility and bottleneck detection |
| Gamification | Points ledger with entity reference and reason | Traceable recognition mechanism |
| Blocked state | Reachable from Planned and InProgress | Models pre-execution and execution blockers |
| Deploy | Docker image behind Traefik, on Dokploy | Portable, no cloud lock-in, TLS terminated by the proxy |
| Scale | 1,000+ users, single organization | Sufficient for MVP query and indexing design |
| Direct DB writes | Forbidden for audited business entities | Prevents bypassing domain rules and audit history |
| Snapshot schema | Versioned | Enables future interpretation of historical snapshots |
| Blockers | First-class dashboard KPI | Bottleneck visibility is a product differentiator |

---

## Sprint 2 Amendments

Decisions above that Sprint 2 changed, with the reasoning recorded so the history stays
readable. Full context in [`docs/sprint-2/architecture.md`](docs/sprint-2/architecture.md).

| Decision | Was | Is | Why it changed |
|---|---|---|---|
| Persistence | EF Core 8 over Azure SQL | `MongoDB.Driver` over MongoDB 8.0 | Sprint 2 requirement. The read patterns are document-shaped; the audit guarantees are preserved by explicit multi-document transactions rather than by change tracking. |
| Identity stores | `AddEntityFrameworkStores` | Six Identity contracts implemented in-project over Mongo | No maintained first-party Mongo store exists; adopting an unmaintained community package would be a larger risk than owning the six interfaces the product actually uses. |
| Deploy target | Azure | Docker image behind Traefik on Dokploy | Portability, and no dependency on a specific cloud for the delivery. |
| Background schedulers | Explicit non-goal | One `HostedService` for the notification outbox | Push delivery must not decide whether a domain write commits. The worker drains the outbox outside the domain transaction. |
| Intelligent features | Out of scope | Manager copilot, project draft and executive insights over Gemini | Sprint 2 requirement. The assistant advises and never decides: every run is recorded, and nothing it produces is persisted without a human confirming it. |

The non-goals that still hold: multi-tenancy, microservices, active Redis caching, active
SignalR, wired Azure AD SSO, ERP integration, file attachments and email notifications.

---

## MVP Scope

| Module | Included Capabilities |
|---|---|
| Authentication | Registration, login, JWT access and refresh tokens, role authorization |
| Strategic Guidelines | Leadership CRUD; authenticated read access |
| Ideas | Create, submit, list, filter, comment, prioritize, approve, reject |
| Projects | Create from idea or standalone, assign owner/deadline/cost, state transitions |
| Tracking | Immutable audit log, project snapshots and timeline |
| Results & ROI | Estimated and actual revenue/savings/cost with computed ROI |
| Dashboard | Conversion, completion time, project distribution, ROI, blockers and bottleneck index |
| Gamification | Points ledger, operator score and idea-approval award |
| Mobile App | Role-based flows connected to the live API |

---

## Explicit Non-Goals for the MVP

- Multi-tenancy
- Microservices
- Active Redis caching
- Active SignalR real-time updates
- Background schedulers
- Wired Azure AD SSO
- ERP/finance integration
- File attachments
- Email notifications
- Public API or webhooks
- Bulk import/export
- Advanced BI beyond core dashboard KPIs

Prepared interfaces may exist for future integrations, but deferred infrastructure must not be activated without an explicit product decision.

---

## Traceability Rules

Every state-changing operation on audited business entities must flow through domain logic and Application command handlers.

For project transitions, the following must happen atomically:

1. Validate and apply the domain transition.
2. Append the corresponding `AuditLog` entry.
3. Capture a full `ProjectSnapshot`.
4. Persist all changes in the same database transaction.

Direct controller-to-database mutations and raw SQL updates to audited entities are critical defects.

---

## ROI Rules

Estimated and actual values are separate business facts and never overwrite one another.

```text
ROI = (Revenue + Savings - Cost) / Cost × 100
```

Division by zero returns `null`. Calculation occurs in the application layer when values are saved.

---

## Success Criteria

### Innovation Pipeline

- Submitted ideas reach a recorded decision.
- Approved ideas can be converted to projects.
- Managers have a visible review queue.

### ROI Traceability

- Estimated values can be captured during planning/execution.
- Actual values can be captured after completion.
- Estimated versus actual outcomes are visible separately.
- Result changes are attributable to an actor and timestamp.

### Bottleneck Visibility

- Blocked projects are surfaced prominently.
- Each blocker exposes its reason and duration.
- The bottleneck index is visible to leadership.
- Blocking and unblocking remain auditable.

### Audit Completeness

- Every state transition has an audit entry.
- Every project transition has a matching snapshot.
- Governance-critical transitions carry a reason.

### User Experience

- Operators can submit ideas quickly.
- Managers can act on review items with minimal navigation.
- Leadership dashboard queries remain fast at the MVP scale target.

---

## Technical Constraints

- No cache layer for the MVP.
- No active real-time transport for dashboard updates.
- No background-job dependency for core behavior.
- No microservice decomposition in the MVP.
- Domain must remain framework-independent.
- Application must not reference Infrastructure or API.
- Every points award creates a traceable ledger entry.

---

## Delivery Phases

| Phase | Scope |
|---|---|
| 1 | Solution structure, database schema, authentication, repositories and audit infrastructure |
| 2 | Ideas, projects, state machines, audit writes and snapshots |
| 3 | Results, ROI, dashboard and gamification |
| 4 | React Native mobile application |
| 5 | End-to-end verification and technical documentation |

---

## Authoritative Design Specification

The detailed domain model, state machines, API surface and architectural design are documented in:

```text
docs/specs/2026-05-13-flow-mvp-design.md
```

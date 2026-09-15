# Flow — System Design Document

**Authors:** Elias Sales de Freitas · João Vitor Bernardo
**Institution:** FIAP
**Version:** 1.0

> This is the top-level system design document. Detailed breakdowns are in `/docs/architecture/`.

---

## 1. Executive Summary

Flow is an innovation lifecycle management system built to solve a structural problem in organisations: not a shortage of ideas, but an absence of structure around what happens to them. Ideas are created, discussed, and forgotten. Projects begin without traceability. Outcomes are never measured.

Flow imposes a governed pipeline from idea creation to measurable business outcome. Every action is recorded. Every decision carries a reason. Every state transition is permanent and auditable. The system is designed for three distinct roles — Operators who generate ideas, Managers who evaluate and execute them, and Leadership who monitors performance — each with a tailored interface and access scope.

---

## 2. High-Level Architecture

Flow is implemented as a **modular monolith** following **Clean Architecture**. The backend exposes a single REST API consumed by a React Native mobile application. All persistence goes through a single Azure SQL database. There are no microservices, no message brokers, and no external caches — by design, for the MVP scale target of up to 1,000 concurrent users in a single organisation.

```
┌─────────────────────────────────────────────────────────────────┐
│                        CLIENT LAYER                             │
│                                                                 │
│   ┌──────────────────────────────────────────────────────┐      │
│   │        React Native (Expo) — Mobile App              │      │
│   │   Operator UI │ Manager UI │ Leadership Dashboard    │      │
│   └──────────────────────┬───────────────────────────────┘      │
└─────────────────────────┼───────────────────────────────────────┘
                          │  HTTPS / REST (JWT Bearer)
┌─────────────────────────▼───────────────────────────────────────┐
│                        API LAYER                                │
│                                                                 │
│   ASP.NET Core 8 — Web API                                      │
│   Controllers → MediatR → Application Layer                     │
│   JWT Middleware │ Role-based Authorization │ OpenAPI            │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────┐
│                    APPLICATION LAYER                            │
│                                                                 │
│   Commands (mutations)     │  Queries (reads)                   │
│   ─────────────────────    │  ──────────────────                │
│   CreateIdeaCommand        │  GetIdeasQuery                     │
│   SubmitIdeaCommand        │  GetIdeaByIdQuery                  │
│   ApproveIdeaCommand       │  GetProjectsQuery                  │
│   ConvertIdeaToProject     │  GetProjectByIdQuery               │
│   StartProjectCommand      │  GetProjectTimelineQuery           │
│   BlockProjectCommand      │  GetDashboardSummaryQuery          │
│   CompleteProjectCommand   │  GetMyPointsQuery                  │
│   RecordResultCommand      │  ...                               │
└──────────────┬──────────────────────────────┬───────────────────┘
               │                              │
┌──────────────▼──────────┐   ┌──────────────▼───────────────────┐
│      DOMAIN LAYER       │   │       INFRASTRUCTURE LAYER        │
│                         │   │                                   │
│  Entities + State       │   │  Entity Framework Core 8          │
│  Machines               │   │  ApplicationDbContext             │
│                         │   │  EF Configurations                │
│  Idea                   │   │  Migrations                       │
│  Project                │   │                                   │
│  Result                 │   │  Identity (ASP.NET Core)          │
│  AuditLog               │   │  JWT issuance + refresh           │
│  ProjectSnapshot        │   │                                   │
│  PointLedgerEntry       │   │  Prepared (no-op for MVP):        │
│  StrategicGuideline     │   │  IAuthProvider (SSO seam)         │
│  IdeaComment            │   │  ICacheService (Redis seam)       │
│                         │   │  INotificationService             │
│  DomainException        │   │  IFinancialDataSource (ERP seam)  │
└─────────────────────────┘   └──────────────┬────────────────────┘
                                             │
                               ┌─────────────▼────────────┐
                               │     Azure SQL Database    │
                               │                           │
                               │  Users / Roles            │
                               │  Ideas / IdeaComments     │
                               │  Projects                 │
                               │  ProjectSnapshots         │
                               │  Results                  │
                               │  AuditLogs                │
                               │  PointLedgerEntries       │
                               │  StrategicGuidelines      │
                               │  RefreshTokens            │
                               └───────────────────────────┘
```

---

## 3. Component Breakdown

### Domain Layer
The innermost layer. No framework dependencies.
- Entities own their state — all properties `private set`
- State transitions are named domain methods (`Approve()`, `Block()`)
- Invalid transitions throw `DomainException`
- Factory methods (`Create()`) replace public constructors

### Application Layer
Orchestrates domain logic.
- Commands = all mutations
- Queries = all reads
- `SaveChangesWithAuditAsync` guarantees atomic: state change + audit log + snapshot
- Defines interfaces implemented in Infrastructure

### Infrastructure Layer
Implements application interfaces.
- EF Core context, configurations, migrations
- ASP.NET Core Identity + JWT
- No business logic

### API Layer
Thin HTTP entry point.
- Controller → MediatR → return DTO
- JWT middleware + role-based `[Authorize]`
- No business logic

---

## 4. Data Flow

### Command (mutation) flow
```
HTTP Request
    → Controller validates route + auth
    → mediator.Send(command)
    → Handler: load entity → domain method → audit log → SaveChangesWithAuditAsync
    → Atomic DB commit: entity + AuditLog + ProjectSnapshot (if applicable)
    → Return DTO
```

### Query (read) flow
```
HTTP Request
    → Controller validates route + auth
    → mediator.Send(query)
    → Handler: DbContext query (filtered by role) → map to DTO
    → Return DTO
```

---

## 5. Domain Model

### Idea State Machine
```
Draft → UnderReview → Approved
                   → Rejected
```

### Project State Machine
```
Planning → InProgress ↔ Blocked
                     → Completed
                     → Cancelled  (from any non-terminal state)
```

### Key Entities

| Entity | Purpose |
|--------|---------|
| `Idea` | Lifecycle unit from creation to approval |
| `Project` | Execution unit derived from an approved idea |
| `AuditLog` | Immutable record of every state-changing operation |
| `ProjectSnapshot` | Full point-in-time project state at every transition |
| `Result` | ROI record per project — estimated and actual tracked independently |
| `PointLedgerEntry` | Append-only gamification ledger |
| `StrategicGuideline` | Organisational priority anchors |
| `IdeaComment` | Manager commentary on ideas |

---

## 6. API Structure Overview

Base URL: `/api/v1`

| Module | Key Endpoints |
|--------|--------------|
| Auth | `POST /auth/login`, `/auth/refresh`, `/auth/logout` |
| Ideas | `POST /ideas`, `GET /ideas/{id}`, `POST /ideas/{id}/submit`, `/approve`, `/reject` |
| Projects | `POST /ideas/{id}/convert`, `GET /projects`, `POST /projects/{id}/start\|complete\|block\|unblock` |
| Results | `GET /projects/{id}/result`, `PUT /projects/{id}/result` |
| Dashboard | `GET /dashboard/summary` |
| Users | `GET /users/me/points`, `/ledger`, `/users/{id}/points` |
| Guidelines | `GET /guidelines`, `POST /guidelines`, `PUT /guidelines/{id}` |

Full endpoint reference: [`/docs/api/endpoints.md`](./api/endpoints.md)

---

## 7. Scalability Considerations

**Current target:** 1,000 concurrent users, single organisation.

**Database:** Indexes on `Status` + `OwnerId` for Ideas and Projects. Two-phase EF materialisation for enum aggregates.

**Scaling path — lowest to highest effort:**
1. **Redis:** `ICacheService` seam already defined. Swap implementation, no logic changes.
2. **Horizontal scale-out:** JWT is stateless. Azure Container Apps handles this with no coordination.
3. **Read replica:** Route dashboard queries to replica via connection string change.
4. **Message bus:** `INotificationService` seam. Add publisher to command handlers without touching domain.
5. **Service split:** Command/query boundaries are already the service boundaries. Split when justified.

---

## 8. Trade-offs and Decisions

| Decision | Choice | Key Reason |
|----------|--------|------------|
| Architecture | Modular monolith | Dashboard needs cross-module queries; services add overhead without benefit at 1k users |
| Traceability | AuditLog + Snapshot | Full event sourcing is too complex; hybrid delivers governance guarantees |
| Cache | None (Redis seam ready) | Queries are fast at current scale; complexity not justified |
| Side effects | Synchronous in transaction | Async side effects risk silent audit failures — unacceptable |
| CQRS | Logical (same DB) | Testability + clean intent separation without read-model overhead |
| Auth scope | Route + data level | Route-only is insufficient; data scoping in handler is the authoritative enforcer |

Full ADRs: [`/docs/architecture/decisions.md`](./architecture/decisions.md)

---

## 9. Future Improvements

### Near-term
- **Azure AD SSO** — `IAuthProvider` seam is ready. One implementation swap.
- **Push notifications** — `INotificationService` seam is ready.
- **Web dashboard** — React + Vite for Leadership. Richer layout on large screens.
- **ERP integration** — `IFinancialDataSource` for auto-populated project costs.

### Medium-term
- **Redis cache** — `ICacheService` implementation for dashboard queries.
- **Analytics layer** — idea funnel, project velocity trends, ROI by guideline.
- **Read replica** — offload dashboard to replica as data grows.

### Long-term
- **Multi-tenancy** — schema-per-tenant or row-level security.
- **Event-driven split** — promote to microservices along existing module boundaries when justified by scale.

---

## 10. Engineering Quality

| Standard | Implementation |
|----------|---------------|
| Architecture | Clean Architecture, strict inward-only dependencies |
| CQRS | All mutations via Commands; all reads via Queries |
| Audit compliance | `SaveChangesWithAuditAsync` on all state-changing operations |
| Traceability | AuditLog on every entity change; ProjectSnapshot on every project transition |
| Test coverage | 120+ automated tests |
| No direct DB writes | Controllers never access DbContext; all writes through application layer |
| Validation | Input validated at API boundary; 400 with structured error |
| Authorization | Role enforced at route and data-scope level |

---

*Flow — System Design Document · v1.0*
*Elias Sales de Freitas · João Vitor Bernardo · FIAP*

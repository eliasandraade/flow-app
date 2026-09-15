# Flow — System Design

**Authors:** Elias Sales de Freitas · João Vitor Bernardo
**Institution:** FIAP · Version 1.0

---

## Overview

Flow is an innovation lifecycle management system. Its purpose is to give organisations a governed pipeline from idea creation to measurable business outcome — with every action recorded, every decision traceable, and every outcome quantified.

The system is implemented as a **modular monolith** using **Clean Architecture**. A single ASP.NET Core 8 API serves a React Native (Expo) mobile application. All persistence is handled by a single Azure SQL database. There are no microservices, no message brokers, and no external caches in the MVP.

---

## Architecture Diagram

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
│   ASP.NET Core 8 — Controllers → MediatR → Application Layer   │
│   JWT Middleware │ Role Authorization │ Global Exception Handler │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────┐
│                    APPLICATION LAYER                            │
│   Commands (mutations)         │  Queries (reads)               │
│   CreateIdeaCommand            │  GetIdeasQuery                 │
│   ApproveIdeaCommand           │  GetProjectByIdQuery           │
│   StartProjectCommand          │  GetDashboardSummaryQuery      │
│   RecordResultCommand          │  GetMyPointsQuery              │
│   ...                          │  ...                           │
└──────────────┬──────────────────────────────┬───────────────────┘
               │                              │
┌──────────────▼──────────┐   ┌──────────────▼───────────────────┐
│      DOMAIN LAYER       │   │       INFRASTRUCTURE LAYER        │
│                         │   │                                   │
│  Entities + State       │   │  Entity Framework Core 8          │
│  Machines               │   │  ApplicationDbContext             │
│                         │   │  EF Configurations + Migrations   │
│  Idea                   │   │                                   │
│  Project                │   │  ASP.NET Core Identity            │
│  Result                 │   │  JWT issuance + refresh tokens    │
│  AuditLog               │   │                                   │
│  ProjectSnapshot        │   │  Prepared seams (no-op MVP):      │
│  PointLedgerEntry       │   │  IAuthProvider (future SSO)       │
│  StrategicGuideline     │   │  ICacheService (future Redis)     │
│  IdeaComment            │   │  INotificationService             │
│                         │   │  IFinancialDataSource (ERP)       │
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

## Layer Responsibilities

### Domain Layer
The innermost layer. No framework dependencies whatsoever.

- Entities own their own consistency — all properties are `private set`
- State transitions are domain methods (`Approve()`, `Block()`, `Complete()`)
- Invalid transitions throw `DomainException`
- Factory methods (`Create()`) replace public constructors
- No DTOs, no EF Core, no ASP.NET references

### Application Layer
Orchestrates domain logic in response to external requests.

- Commands handle all state mutations
- Queries handle all reads
- Every command that changes state calls `SaveChangesWithAuditAsync` — atomically writing the state change, audit log entry, and (for projects) a snapshot in a single transaction
- Defines interfaces (`IApplicationDbContext`, `ICurrentUserService`) implemented in Infrastructure

### Infrastructure Layer
Implements application interfaces. The only layer that knows about EF Core and SQL.

- `ApplicationDbContext` with `SaveChangesWithAuditAsync`
- Entity configurations — column types, indexes, relationships, enum-to-string conversions
- ASP.NET Core Identity integration
- JWT issuance and refresh token management
- All migrations

### API Layer
The thin HTTP entry point.

- Controller → `_mediator.Send(command)` → return DTO
- No business logic
- JWT middleware validates every request
- `[Authorize(Roles = "...")]` at both controller and action level
- Global exception handler maps domain exceptions to HTTP status codes

---

## Traceability Model

Flow uses a **hybrid traceability model** — combining an append-only audit log with synchronous project snapshots.

### AuditLog
Records *who did what and when* on any entity. Every command handler that mutates state writes at least one `AuditLog` entry. The log is append-only — never updated, never deleted.

### ProjectSnapshot
Records the *complete project state* at every transition. This allows any project's exact state at any point in time to be reconstructed without event replay. Stored as structured fields (not a JSON blob) to allow dashboard queries like "blocked since when."

### Atomicity Rule
The state change, the `AuditLog` write, and the `ProjectSnapshot` write are always committed in a single database transaction via `SaveChangesWithAuditAsync`. Partial writes are impossible by design.

---

## Domain Model

### Idea State Machine

```
┌─────────┐
│  Draft  │
└────┬────┘
     │ submit()
     ▼
┌─────────────┐
│ UnderReview │
└──────┬──────┘
  ┌────┴────┐
  │         │
  ▼         ▼
┌──────────┐ ┌──────────┐
│ Approved │ │ Rejected │
└──────────┘ └──────────┘
```

### Project State Machine

```
┌──────────┐
│ Planning │
└────┬─────┘
     │ start()
     ▼
┌────────────┐
│ InProgress │◄──────────────────┐
└─────┬──────┘                   │
      │                          │
 ┌────┴────┐                     │
 │         │                     │
 ▼         ▼                     │
┌──────────┐ ┌─────────┐  unblock()
│Completed │ │ Blocked │──────────┘
└──────────┘ └─────────┘

cancel(reason) → Cancelled  (from any non-terminal state)
```

---

## Key Entities

| Entity | Purpose |
|--------|---------|
| `Idea` | Lifecycle unit from creation to approval. Owned by Operator, reviewed by Manager. |
| `Project` | Execution unit derived from an approved idea. Transitions through a governed state machine. |
| `AuditLog` | Immutable record of every state-changing operation. Never modified after creation. |
| `ProjectSnapshot` | Full point-in-time project state captured at every transition. |
| `Result` | ROI record per project. Estimated and actual tracked independently. |
| `PointLedgerEntry` | Append-only gamification ledger. Score is computed from sum, never stored as a running total. |
| `StrategicGuideline` | Organisational priorities. Ideas can reference a guideline to anchor strategic intent. |
| `IdeaComment` | Manager commentary attached to an idea. |
| `RefreshToken` | Refresh token lifecycle. Rotated and revoked on logout. |

---

## Dashboard Metrics

| Metric | Source |
|--------|--------|
| Total Ideas | `COUNT(Ideas)` |
| Approved Ideas | `COUNT WHERE Status = 'Approved'` |
| Conversion Rate | `ApprovedIdeas / TotalIdeas × 100` |
| Active Projects | `COUNT WHERE Status IN ('Planning','InProgress')` |
| Blocked Projects | `COUNT WHERE Status = 'Blocked'` |
| Average Completion Days | `AVG(CompletedAt - StartDate)` |
| Total ROI | `SUM(ActualROI)` |
| Bottleneck Index | `Blocked / (Active + Blocked) × 100` |

The **Bottleneck Index** is computed live. A value above 30% signals that a significant share of in-flight work is stuck and requires management attention.

---

## Scalability

**Current target:** up to 1,000 concurrent users, single organisation.

**Database:** Indexes on `Status` and `OwnerId` for Ideas and Projects. Two-phase materialisation used where EF Core cannot translate enum aggregates.

**Scaling path:**
- **Read caching:** `ICacheService` seam is ready for Redis. Dashboard queries are the primary candidates.
- **Horizontal API scaling:** Azure Container Apps supports horizontal scale-out. JWT is stateless — no session coordination needed.
- **Read replica:** Dashboard and reporting queries can be offloaded to a read replica with a connection string change.
- **Background processing:** `INotificationService` seam supports adding email/push when a queue is introduced.

---

## Deployment

| Component | Platform |
|-----------|----------|
| API | Azure Container Apps |
| Database | Azure SQL |
| Mobile | Expo build (iOS + Android) |

# Flow MVP — System Design Specification

**Date:** 2026-05-13  
**Status:** Approved  
**Scope:** Unified MVP delivery (Sprint 1 + Sprint 2 combined)

---

## 1. System Overview

Flow is a corporate innovation lifecycle management platform. It connects operational problems to ideas, projects, execution, and measurable business results. The system enforces a structured pipeline where every transition is auditable, every decision is traceable, and every outcome is quantifiable.

Flow is **not** an idea box. It is a governance system for innovation.

### Core Pipeline

```
IDEA → ANALYSIS → APPROVAL → PROJECT → EXECUTION → RESULT
```

Each stage is a formal state. Transitions are events — they are recorded, not overwritten.

---

## 2. Architectural Decisions

### 2.1 Tenancy Model

**Single organization.** One deployment serves one company. No tenant isolation required.

### 2.2 Architecture Pattern

**Clean Architecture Modular Monolith.**

- Single deployable ASP.NET Core 8 application
- Internal module boundaries: Auth, Ideas, Projects, Tracking, Results, Dashboard, Gamification
- Each module owns its domain entities, application services, and infrastructure concerns
- Shared kernel: audit log, base entities, domain events, common exceptions

Layers per module:
```
Domain       — Entities, value objects, domain logic, state machine
Application  — Commands, queries, handlers (CQRS-lite), interfaces
Infrastructure — EF Core repositories, external service adapters
API          — Controllers, request/response DTOs, middleware
```

### 2.3 Frontend

- **Mobile:** React Native (Expo managed workflow) — primary client
- **Web:** React + Vite — Leadership dashboard only

### 2.4 Database

**Azure SQL Database** (SQL Server 2022 compatible), accessed via **Entity Framework Core 8**.

### 2.5 Authentication

**Primary:** Email/password via ASP.NET Core Identity + JWT bearer tokens  
**Prepared:** IAuthProvider abstraction in place for Azure AD SSO (not wired)

### 2.6 Traceability Model

**Hybrid: audit log + synchronous state snapshots.**

- Every state transition appends an immutable `AuditLog` row
- Every state transition also creates a `ProjectSnapshot` capturing full project state at that moment
- Snapshots are created **synchronously** within the same database transaction as the transition — no background jobs

### 2.7 Cloud and Deployment

**Azure** — Azure Container Apps (backend), Azure Static Web Apps (web), Azure SQL Database.

### 2.8 Infrastructure Constraints (MVP)

The following are explicitly **excluded** from the MVP:

| Component | Status | Notes |
|---|---|---|
| Redis / caching | Excluded | ICache abstraction prepared for future swap-in |
| SignalR | Excluded | SignalR hub scaffolded; dashboard uses polling |
| Background jobs | Excluded | Snapshot logic callable as service; no scheduler |
| Azure AD SSO | Excluded | IAuthProvider abstraction prepared |
| ERP integration | Excluded | IFinancialDataSource interface defined |

---

## 3. User Roles

| Role | Capabilities |
|---|---|
| **Operator** | Submit ideas, view own ideas and their status, view own points |
| **Manager** | Review, comment, prioritize, approve/reject ideas; convert ideas to projects; manage project execution; log results |
| **Leadership** | Manage strategic guidelines; view full dashboard and ROI reports; read-only access to all projects |

Role assignment is managed by system administrators at the infrastructure level (user seeding / admin endpoint). Self-registration assigns the Operator role by default.

---

## 4. Domain Model

### 4.1 Entities

#### User
```
Id (Guid)
Name (string)
Email (string)
PasswordHash (string)
Role (Operator | Manager | Leadership)
Points (int)                        ← total accumulated points
CreatedAt (DateTimeOffset)
```

#### PointLedgerEntry
```
Id (Guid)
UserId (Guid)
Points (int)
Reason (string)                     ← human-readable description
ReferenceType (string)              ← e.g., "Idea", "Project"
ReferenceId (Guid)                  ← FK to the referenced entity
AwardedAt (DateTimeOffset)
```

#### StrategicGuideline
```
Id (Guid)
Title (string)
Description (string)
CreatedBy (Guid → User)
CreatedAt (DateTimeOffset)
UpdatedAt (DateTimeOffset)
```

#### Idea
```
Id (Guid)
Title (string)
Description (string)
Problem (string)                    ← operational problem being addressed
SubmittedBy (Guid → User)
Status (IdeaStatus)
Priority (IdeaPriority)             ← set by Manager
ManagerComment (string?)
LinkedGuidelineId (Guid?)           ← optional strategic alignment
CreatedAt (DateTimeOffset)
UpdatedAt (DateTimeOffset)
```

**IdeaStatus state machine:**
```
Draft → UnderReview → Approved
                    → Rejected
```

#### IdeaComment
```
Id (Guid)
IdeaId (Guid → Idea)
AuthorId (Guid → User)
Body (string)
CreatedAt (DateTimeOffset)
```

#### Project
```
Id (Guid)
Title (string)
Description (string)
SourceIdeaId (Guid?)                ← null if manually created
OwnerId (Guid → User)
Status (ProjectStatus)
Priority (ProjectPriority)
EstimatedCost (decimal?)
ActualCost (decimal?)
StartDate (DateTimeOffset?)
Deadline (DateTimeOffset?)
CompletedAt (DateTimeOffset?)
BlockedReason (string?)             ← populated when Status = Blocked
CreatedAt (DateTimeOffset)
UpdatedAt (DateTimeOffset)
```

**ProjectStatus state machine:**
```
Planned → InProgress → Completed
                     → Cancelled
        → Blocked                  ← operational dependency or prerequisite not met
InProgress → Blocked
Blocked    → InProgress            ← unblock (resume)
Blocked    → Cancelled             ← cancel while blocked
```

`Blocked` is reachable from both `Planned` and `InProgress`. This reflects real-world operational friction — a project may be blocked before execution begins due to dependencies, approvals, or resource constraints. A `BlockedReason` is required on every transition into `Blocked`. All transitions are guarded. Invalid transitions return a domain error and do not persist.

#### AuditLog
```
Id (Guid)
EntityType (string)                 ← "Idea" | "Project"
EntityId (Guid)
Action (string)                     ← e.g., "StatusChanged", "CommentAdded"
ActorId (Guid → User)
ActorName (string)                  ← denormalized for historical accuracy
OldValue (string? / JSON)
NewValue (string? / JSON)
Reason (string?)                    ← optional actor-supplied context for why the action occurred
Timestamp (DateTimeOffset)
```

Append-only. Never updated or deleted. The `Reason` field is required for destructive or blocking transitions (`Reject`, `Cancel`, `Block`) and optional for all others.

#### ProjectSnapshot
```
Id (Guid)
ProjectId (Guid → Project)

← Full project state at moment of capture:
Title (string)
Description (string)
Status (ProjectStatus)
Priority (ProjectPriority)
OwnerId (Guid)
OwnerName (string)                 ← denormalized for historical accuracy
SourceIdeaId (Guid?)
EstimatedCost (decimal?)
ActualCost (decimal?)
StartDate (DateTimeOffset?)
Deadline (DateTimeOffset?)
CompletedAt (DateTimeOffset?)
BlockedReason (string?)

← Snapshot metadata:
TakenAt (DateTimeOffset)
TriggerAction (string)             ← transition that caused this snapshot (e.g., "Blocked", "Completed")
TriggeredByActorId (Guid)
SchemaVersion (int)                ← incremented when the snapshot schema changes; allows future migration and interpretation of old snapshots
```

Captured synchronously on every project state transition within the same DB transaction. Represents a complete, self-contained record of project state — sufficient to reconstruct what the project looked like at any point in its lifecycle without joining other tables.

#### Result
```
Id (Guid)
ProjectId (Guid → Project)

← Planning phase (estimated — populated before or during execution):
EstimatedRevenue (decimal?)
EstimatedSavings (decimal?)
EstimatedCost (decimal?)
EstimatedROI (decimal?)            ← computed: (EstimatedRevenue + EstimatedSavings - EstimatedCost) / EstimatedCost × 100
EstimatedRecordedAt (DateTimeOffset?)  ← when estimated values were last set

← Post-completion phase (actual — populated after project completes):
ActualRevenue (decimal?)
ActualSavings (decimal?)
ActualCost (decimal?)
ActualROI (decimal?)               ← computed: (ActualRevenue + ActualSavings - ActualCost) / ActualCost × 100
ActualRecordedAt (DateTimeOffset?) ← when actual values were last set

PaybackPeriodMonths (int?)
Notes (string?)
RecordedBy (Guid → User)
RecordedAt (DateTimeOffset)        ← record creation timestamp
UpdatedAt (DateTimeOffset)
```

Estimated ROI and actual ROI are independently tracked. Setting estimated values does not overwrite actuals and vice versa. Both are computed server-side on save and stored for query performance. Division by zero returns `null`. The `AuditLog` records every update to this entity, providing full traceability of when estimates and actuals were entered and by whom.

### 4.2 Relationships

```
User ──< Idea                (one user submits many ideas)
User ──< Project             (one user owns many projects)
User ──< PointLedgerEntry    (one user has many point entries)
Idea ──< IdeaComment         (one idea has many comments)
Idea ──o── Project           (one idea optionally becomes one project)
Project ──< AuditLog         (one project has many audit entries)
Project ──< ProjectSnapshot  (one project has many snapshots)
Project ──1── Result         (one project has one result record)
StrategicGuideline ──< Idea  (one guideline linked to many ideas)
```

---

## 5. State Machines

### 5.1 Idea Lifecycle

```
[Draft]
  │
  ▼ (submit for review — Operator action)
[UnderReview]
  │
  ├──▶ [Approved]   (Manager approves — triggers point award)
  │
  └──▶ [Rejected]   (Manager rejects — optional comment required)
```

### 5.2 Project Lifecycle

```
[Planned]
  │
  ├──▶ [InProgress]  (Manager starts project)
  │       │
  │       ├──▶ [Completed]   (Manager marks complete — triggers Result prompt)
  │       │
  │       ├──▶ [Cancelled]   (Manager cancels — reason required)
  │       │
  │       └──▶ [Blocked]     (Manager flags as blocked — reason required)
  │
  └──▶ [Blocked]     (Manager flags as blocked before execution — reason required)
            │
            ├──▶ [InProgress]  (Manager unblocks — resumes execution)
            │
            └──▶ [Cancelled]   (Manager cancels while blocked)
```

`Blocked` is reachable from both `Planned` and `InProgress`. This allows representing pre-execution blockers (e.g., pending budget approval, unresolved dependencies) as well as mid-execution interruptions. `BlockedReason` is required on every `→ Blocked` transition. `CompletedAt` is set automatically when transitioning to `Completed`.

---

## 6. API Design

REST API with JWT bearer authentication. All endpoints versioned under `/api/v1/`.

### Auth
| Method | Path | Description |
|---|---|---|
| POST | `/auth/register` | Register new user (default: Operator role) |
| POST | `/auth/login` | Login, returns access token + refresh token |
| POST | `/auth/refresh` | Refresh access token |
| POST | `/auth/logout` | Invalidate refresh token |

### Strategic Guidelines
| Method | Path | Roles |
|---|---|---|
| GET | `/guidelines` | All |
| GET | `/guidelines/{id}` | All |
| POST | `/guidelines` | Leadership |
| PUT | `/guidelines/{id}` | Leadership |
| DELETE | `/guidelines/{id}` | Leadership |

### Ideas
| Method | Path | Roles |
|---|---|---|
| POST | `/ideas` | Operator |
| GET | `/ideas` | Operator (own), Manager (all), Leadership (all) |
| GET | `/ideas/{id}` | All |
| PUT | `/ideas/{id}` | Operator (own, Draft only) |
| POST | `/ideas/{id}/submit` | Operator |
| POST | `/ideas/{id}/approve` | Manager |
| POST | `/ideas/{id}/reject` | Manager |
| GET | `/ideas/{id}/comments` | All |
| POST | `/ideas/{id}/comments` | Manager |

### Projects
| Method | Path | Roles |
|---|---|---|
| POST | `/projects` | Manager |
| POST | `/ideas/{id}/convert` | Manager (creates project from idea) |
| GET | `/projects` | Manager (all), Leadership (all), Operator (own) |
| GET | `/projects/{id}` | All |
| PUT | `/projects/{id}` | Manager (owner or admin) |
| POST | `/projects/{id}/start` | Manager |
| POST | `/projects/{id}/complete` | Manager |
| POST | `/projects/{id}/cancel` | Manager |
| POST | `/projects/{id}/block` | Manager |
| POST | `/projects/{id}/unblock` | Manager |
| GET | `/projects/{id}/timeline` | All |
| GET | `/projects/{id}/snapshots` | Manager, Leadership |

### Results
| Method | Path | Roles |
|---|---|---|
| GET | `/projects/{id}/result` | All |
| PUT | `/projects/{id}/result` | Manager |

### Dashboard
| Method | Path | Roles |
|---|---|---|
| GET | `/dashboard/summary` | Leadership, Manager |

Response includes:
- Total ideas submitted / approved / rejected / pending
- Idea-to-project conversion rate
- Projects by status (Planned, InProgress, Blocked, Completed, Cancelled)
- **Blocked projects count** — first-class KPI, not buried in status distribution
- **Blocked project list** — id, title, owner, blockedReason, duration in Blocked state (days)
- Global ROI (sum of actual ROI across completed projects)
- Average project completion time (in days)
- Bottleneck index — percentage of active projects currently in Blocked state

### Gamification
| Method | Path | Roles |
|---|---|---|
| GET | `/users/me/points` | Operator |
| GET | `/users/me/points/ledger` | Operator |
| GET | `/users/{id}/points` | Manager, Leadership |

---

## 7. Audit Log Behavior

The `AuditLog` table is written to on:

| Event | EntityType | Action |
|---|---|---|
| Idea submitted | Idea | `Submitted` |
| Idea approved | Idea | `Approved` |
| Idea rejected | Idea | `Rejected` |
| Idea commented | Idea | `CommentAdded` |
| Idea priority changed | Idea | `PriorityChanged` |
| Project created | Project | `Created` |
| Project started | Project | `Started` |
| Project blocked | Project | `Blocked` |
| Project unblocked | Project | `Unblocked` |
| Project completed | Project | `Completed` |
| Project cancelled | Project | `Cancelled` |
| Project field updated | Project | `Updated` |
| Result recorded | Project | `ResultRecorded` |

Every audit entry includes `ActorId`, `ActorName` (denormalized), `OldValue`, `NewValue` (JSON), and `Timestamp`.

---

## 8. ROI Calculation

```
EstimatedROI = (EstimatedRevenue + EstimatedSavings - EstimatedCost) / EstimatedCost × 100
ActualROI    = (ActualRevenue + ActualSavings - ActualCost) / ActualCost × 100
```

Division by zero returns `null`. Stored as computed columns on the Result entity (not database computed columns — computed in the application layer on save).

---

## 9. Gamification

Points are awarded for:

| Event | Points | Reason String |
|---|---|---|
| Idea approved | 50 | `"Idea approved by manager"` |
| (Future) Idea converted to project | 25 | `"Idea converted to project"` |

Each award creates one `PointLedgerEntry` with `ReferenceType = "Idea"`, `ReferenceId = ideaId`. The `User.Points` field is incremented atomically within the same transaction.

---

## 10. Mobile Application Structure

**Framework:** React Native, Expo managed workflow  
**Navigation:** React Navigation v6 (stack + bottom tab)  
**API Client:** Auto-generated from OpenAPI spec (openapi-typescript-codegen)  
**Auth:** JWT stored in SecureStore  
**State:** TanStack Query (server state) + Zustand (auth session)

### Screen Map

#### Operator
- Login
- Home (recent ideas, points summary)
- Submit Idea
- My Ideas (list with status badges)
- Idea Detail (read-only + comments)

#### Manager
- Login
- Home (review queue count, active projects count)
- Idea Queue (filterable list)
- Idea Detail (approve / reject / comment / prioritize)
- Project List (filterable by status)
- Project Detail (fields, status, actions)
- Project Timeline (ordered audit log)
- Update Project (edit fields, trigger transitions)
- Log Result (ROI entry form)

#### Leadership
- Login
- Dashboard (KPIs: conversion rate, avg completion time, ROI, status distribution)
- Project Overview (read-only list)
- Project Detail (read-only)
- Strategic Guidelines (CRUD)
- Result Detail (read-only)

---

## 11. Execution Phases

| Phase | Scope | Deliverable |
|---|---|---|
| **1** | Foundation | Solution structure, DB schema, auth, audit log infrastructure |
| **2** | Core Domain | Ideas, Projects, state machine, audit log writes, snapshots |
| **3** | Results & Dashboard | ROI engine, dashboard aggregations, gamification |
| **4** | Mobile Application | All three role flows, connected to live API |
| **5** | Integration & Docs | End-to-end verification, OpenAPI, technical documentation |

---

## 12. Prepared Abstractions (Not Implemented)

| Abstraction | Interface | Purpose |
|---|---|---|
| `IAuthProvider` | `IAuthProvider.cs` | Swap email/password for Azure AD SSO |
| `ICacheService` | `ICacheService.cs` | Swap no-op for Redis |
| `IFinancialDataSource` | `IFinancialDataSource.cs` | Swap manual ROI entry for ERP integration |
| `INotificationService` | `INotificationService.cs` | Swap no-op for push/email notifications |

Each interface has a concrete no-op or manual implementation registered in DI. Swapping is a one-file change.

---

## 13. Non-Goals (MVP Scope Boundary)

- Multi-tenancy
- Azure AD SSO (wired, not just prepared)
- Real-time dashboard updates (SignalR)
- File attachments on ideas or projects
- Email notifications
- Admin user management UI
- Public API or webhooks
- Bulk import/export

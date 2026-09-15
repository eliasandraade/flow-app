# Flow — Engineering Operating Rules

This document defines the engineering conventions, architectural constraints, and definition of done for the Flow project.

---

## Project Overview

Flow is a corporate innovation lifecycle management platform. Its purpose is to connect operational problems to ideas, formal projects, tracked execution, and measurable business outcomes.

The system enforces a structured pipeline:

```text
IDEA → ANALYSIS → APPROVAL → PROJECT → EXECUTION → RESULT
```

Every relevant transition is recorded. Governance and traceability are first-class product requirements.

---

## Core Principles

### 1. End-to-End Completeness

Features must work completely across all affected roles. Avoid partially implemented flows that create false confidence.

### 2. Traceability

Audit logs and project snapshots are core infrastructure. Every state transition must produce an `AuditLog` entry and, for projects, a `ProjectSnapshot` in the same database transaction.

### 3. Clean Architecture Discipline

```text
Domain          — Entities, state machines and domain logic. No framework dependencies.
Application     — Commands, queries, handlers and interfaces.
Infrastructure  — MongoDB persistence, Identity stores and external adapters.
API             — Controllers, middleware and dependency injection wiring.
```

Rules:

- Domain must not reference Application, Infrastructure, or API.
- Application must not reference Infrastructure or API.
- Dependencies always point inward.
- DTOs do not leak into Domain.
- Domain entities are never returned directly from API endpoints.

### 4. Lean Implementation

Keep the MVP complete without overengineering. Prefer existing patterns and straightforward code over speculative abstractions.

---

## Working Rules

Before implementing a feature:

1. Identify affected domain entities and state transitions.
2. Confirm audit-log and snapshot behavior.
3. Confirm role and authorization requirements.
4. Check existing code patterns before introducing new ones.
5. Clarify consequential ambiguities before implementation and document the resulting decision.

Avoid premature generic helpers, unnecessary configuration, hypothetical infrastructure, and defensive handling for impossible states.

---

## Architectural Decisions

| Concern | Decision |
|---|---|
| Architecture | Modular monolith with Clean Architecture |
| Backend | ASP.NET Core 8, C# |
| Data access | `MongoDB.Driver` (official driver, no ORM) |
| Database | MongoDB 8.0, replica set required |
| Mobile | React Native with Expo managed workflow |
| Web | React + Vite for leadership dashboard |
| Authentication | ASP.NET Core Identity + JWT |
| Traceability | Append-only AuditLog + synchronous ProjectSnapshot |
| Cloud | Azure |
| ROI | Manual entry; estimated and actual tracked independently |
| Scale target | 1,000+ users, single organization |

---

## Infrastructure Constraints

The MVP does not introduce Redis, active SignalR, background-job schedulers, microservices, wired Azure AD SSO, or ERP integration.

Prepared seams may exist for future integrations, including:

- `IAuthProvider`
- `ICacheService`
- `IFinancialDataSource`
- `INotificationService`

These interfaces are extension points, not justification to activate deferred infrastructure prematurely.

---

## State Machine Rules

State transitions are domain operations, never direct field assignments.

1. Transitions are explicitly defined in domain entities.
2. Every transition produces an `AuditLog` entry with actor, previous value, new value, reason when required, and timestamp.
3. Every project transition produces a full `ProjectSnapshot`.
4. State change, audit log and snapshot are persisted in the same transaction.
5. `BlockedReason` is mandatory when entering `Blocked`.
6. `AuditLog.Reason` is mandatory for reject, cancel and block operations.

### Direct Database Mutation Is Forbidden

Controllers and middleware must not mutate audited domain entities through repositories, the Mongo driver, or any other persistence shortcut.

All writes to `Idea`, `Project`, and `Result` flow through Application command handlers and domain logic. Reference-data seeding and index creation are the only exceptions, and must not bypass the audit model for business entities.

---

## ROI Rules

- Estimated ROI belongs to planning/execution.
- Actual ROI belongs to post-completion measurement.
- Estimated and actual values are stored independently.
- Formula: `(Revenue + Savings - Cost) / Cost × 100`.
- Division by zero returns `null`.
- ROI is computed in the application layer when values are saved.

---

## Definition of Done

A feature is complete when:

1. It works end-to-end for every affected role.
2. Role-based authorization is enforced at the API boundary.
3. State-changing actions are fully auditable.
4. Input is validated and invalid input returns structured errors.
5. API and domain behavior are documented when changed.
6. Layer boundaries remain intact.
7. Automated tests cover the relevant business and HTTP behavior.

---

## Module Boundaries

| Module | Owns |
|---|---|
| Auth | Registration, login, JWT and refresh-token lifecycle |
| Ideas | Idea lifecycle, comments, priority and approval/rejection |
| Projects | Project lifecycle, owner assignment and transitions |
| Tracking | Audit logs, snapshots and timeline queries |
| Results | ROI entry and outcome tracking |
| Dashboard | Aggregations and leadership KPIs |
| Gamification | Points ledger and awards |
| Guidelines | Strategic guideline CRUD |

Cross-module interaction goes through application contracts, never direct infrastructure-to-infrastructure calls.

---

## Naming Conventions

- Commands: `{Action}{Entity}Command.cs`
- Queries: `Get{Entity}{Qualifier}Query.cs`
- Entities: singular nouns
- Controllers: plural nouns
- DTOs: `{Entity}{Purpose}Dto.cs`

---

## Authoritative Product Specification

Structural changes to the domain model, API surface, or module boundaries must be checked against:

```text
docs/specs/2026-05-13-flow-mvp-design.md
```

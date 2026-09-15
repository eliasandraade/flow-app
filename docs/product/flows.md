# Flow — User Flows

This document describes the key user flows in Flow — how each role interacts with the system and how information moves through the pipeline.

---

## 1. Idea Lifecycle

The idea lifecycle begins when an Operator submits a problem or proposal and ends when it is either rejected or converted into an active project.

```
┌─────────────────────────────────────────────────────────────────┐
│  OPERATOR                                                       │
│                                                                 │
│  1. Creates idea                                                │
│     → Title, Problem statement, Description                     │
│     → Idea status: Draft                                        │
│                                                                 │
│  2. Reviews draft, makes edits                                  │
│                                                                 │
│  3. Submits idea for review                                     │
│     → Idea status: UnderReview                                  │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  MANAGER                                                        │
│                                                                 │
│  4. Reviews the submitted idea                                  │
│     → Reads problem, description, strategic guideline link      │
│                                                                 │
│  5a. Approves                                                   │
│      → Optional comment                                         │
│      → Idea status: Approved                                    │
│      → Operator notified (future: push notification)            │
│                                                                 │
│  5b. Rejects                                                    │
│      → Reason is mandatory                                      │
│      → Idea status: Rejected                                    │
│      → Operator sees the rejection reason in their idea detail  │
└─────────────────────────────────────────────────────────────────┘

Audit: every status change → AuditLog entry with actor, timestamp, reason
```

---

## 2. Decision Flow (Approval / Rejection)

The decision flow is the most governance-sensitive part of the system. Every decision is permanent and carries a reason.

```
Manager opens idea in UnderReview state
         │
         ├── Reads full idea: title, problem, description, guideline
         │
         ├── Adds optional comment (for context or feedback)
         │
         ├─── APPROVE ─────────────────────────────────────────────►
         │    ManagerComment: optional
         │    Idea.Status → Approved
         │    AuditLog: Action="Approved", Reason=comment
         │
         └─── REJECT ──────────────────────────────────────────────►
              ManagerComment: REQUIRED
              Idea.Status → Rejected
              AuditLog: Action="Rejected", Reason=comment
              Operator sees reason on their idea detail screen
```

**Invariants enforced by the domain:**
- Only `UnderReview` ideas can be approved or rejected
- Rejection without a comment is refused at the domain level — not just the UI
- Once approved or rejected, the idea cannot return to `UnderReview`

---

## 3. Project Execution Flow

Once an idea is approved, a Manager converts it to a project and drives it through the execution lifecycle.

```
APPROVED IDEA
     │
     │  Manager: Convert to Project
     │  → Title, Description, Priority, Owner, EstimatedCost, Deadline
     │  → Project.Status: Planning
     │  → ProjectSnapshot created
     ▼
┌────────────┐
│  Planning  │  Manager reviews scope, assigns resources
└────┬───────┘
     │  start()
     ▼
┌────────────┐
│ InProgress │  Active execution
└────┬───────┘
     │
     ├─── block(reason) ──────────────────────────────────────────►
     │    BlockedReason: REQUIRED
     │    Project.Status → Blocked
     │    Dashboard: blocked project appears with days blocked
     │    ProjectSnapshot captured
     │
     │         ┌──────────┐
     │         │ Blocked  │
     │         └────┬─────┘
     │              │ unblock()
     │              └──────────────────────────────────────────────►
     │                  Project.Status → InProgress (back)
     │                  ProjectSnapshot captured
     │
     ├─── complete() ─────────────────────────────────────────────►
     │    Project.Status → Completed
     │    ProjectSnapshot captured
     │    → Manager records Result (ROI)
     │
     └─── cancel(reason) ─────────────────────────────────────────►
          Reason: REQUIRED
          Project.Status → Cancelled
          ProjectSnapshot captured
          Terminal state — no further transitions

Every transition → AuditLog entry + ProjectSnapshot (in one transaction)
```

---

## 4. ROI Recording Flow

After a project is completed (or while in progress), a Manager records the financial outcome.

```
Manager opens project result screen
         │
         ├── Records ESTIMATED values (before completion)
         │   EstimatedRevenue, EstimatedSavings, EstimatedCost
         │   → EstimatedROI computed: (Revenue + Savings - Cost) / Cost × 100
         │
         └── Records ACTUAL values (after completion)
             ActualRevenue, ActualSavings, ActualCost
             → ActualROI computed
             → PaybackPeriodMonths (optional)
             → Notes (optional)

Both estimated and actual are stored independently.
Neither overwrites the other.
Both are audited.

ROI formula: (Revenue + Savings - Cost) / Cost × 100
Division by zero (or null cost) → ROI stored as null (not zero)
```

---

## 5. Leadership Monitoring Flow

Leadership has a read-only view that aggregates the full pipeline into actionable metrics.

```
Leadership opens Dashboard
         │
         ├── IDEAS SECTION
         │   Total submitted │ Approved │ Rejected │ Pending Review
         │   Conversion rate: Approved / Total × 100
         │
         ├── PROJECTS SECTION
         │   Active │ Blocked │ Completed
         │   Average completion days
         │   Total ROI (sum of actuals)
         │   Bottleneck Index: Blocked / (Active + Blocked) × 100
         │
         └── BLOCKED PROJECTS (critical visibility)
             Per blocked project:
             → Title
             → Days blocked (computed from last "Blocked" snapshot)
             → Reason for blocking
             Sorted: longest blocked first

Dashboard auto-refreshes every 60 seconds.
```

The **Bottleneck Index** is the key signal. When it exceeds 30%, a significant share of in-flight work is stuck and requires management intervention.

---

## 6. Authentication Flow

```
User opens app
     │
     ├── Has saved session? (SecureStore check on startup)
     │   YES → hydrate Zustand store → route to role screen
     │   NO  → show LoginScreen
     │
     └── Login
         POST /auth/login { email, password }
         → AccessToken (JWT, ~60 min) + RefreshToken
         → Saved to SecureStore (accessToken, refreshToken, userId, name, email, role)
         → Zustand store updated
         → Navigate to role-appropriate screen

On 401 response (expired token):
     → SecureStore cleared
     → Zustand session cleared
     → User returned to LoginScreen automatically

Sign Out:
     → POST /auth/logout { refreshToken } (server revokes token)
     → SecureStore cleared
     → Zustand session cleared
     → LoginScreen shown
```

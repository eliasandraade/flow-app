# Flow API — Endpoint Reference

**Base URL:** `http://localhost:5153/api/v1` (development)
**Authentication:** Bearer JWT token required on all endpoints unless marked `Public`
**Content-Type:** `application/json`

Error responses follow ProblemDetails (RFC 7807):
```json
{
  "title": "Validation failed",
  "status": 400,
  "detail": "...",
  "errors": { "field": ["message"] }
}
```

---

## Authentication

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/auth/register` | Public | Create a new user account |
| POST | `/auth/login` | Public | Authenticate and receive tokens |
| POST | `/auth/refresh` | Public | Exchange refresh token for new access token |
| POST | `/auth/logout` | Any | Revoke refresh token |

### POST `/auth/login`
```json
// Request
{ "email": "user@company.com", "password": "..." }

// Response 200
{
  "accessToken": "eyJ...",
  "refreshToken": "...",
  "userId": "uuid",
  "name": "Elias Freitas",
  "email": "user@company.com",
  "role": "Operator"  // "Operator" | "Manager" | "Leadership"
}
```

### POST `/auth/refresh`
```json
// Request
{ "refreshToken": "..." }

// Response 200 — same shape as login
```

### POST `/auth/logout`
```json
// Request
{ "refreshToken": "..." }

// Response 204 No Content
```

---

## Ideas

**Role scoping:** Operators receive only their own ideas. Managers and Leadership receive all ideas.

| Method | Path | Role | Description |
|--------|------|------|-------------|
| POST | `/ideas` | Operator | Create an idea |
| GET | `/ideas` | Any | List ideas (role-scoped) |
| GET | `/ideas/{id}` | Any | Idea detail |
| PUT | `/ideas/{id}` | Operator | Update a Draft idea |
| POST | `/ideas/{id}/submit` | Operator | Submit Draft for review |
| POST | `/ideas/{id}/approve` | Manager | Approve an UnderReview idea |
| POST | `/ideas/{id}/reject` | Manager | Reject with mandatory reason |
| GET | `/ideas/{id}/comments` | Any | List manager comments |
| POST | `/ideas/{id}/comments` | Manager | Add a comment |

### POST `/ideas`
```json
// Request
{
  "title": "Reduce delivery time via route optimisation",
  "description": "Currently drivers use manual routing...",
  "problem": "Average delivery takes 4 hours. Target is 2.5 hours.",
  "linkedGuidelineId": "uuid-or-null"
}

// Response 201
{
  "id": "uuid",
  "title": "...",
  "status": "Draft",
  "priority": "Medium",
  "submittedBy": "uuid",
  "createdAt": "2026-05-14T..."
}
```

### POST `/ideas/{id}/approve`
```json
// Request
{ "managerComment": "Strong business case. Approving." }  // comment optional

// Response 204 No Content
```

### POST `/ideas/{id}/reject`
```json
// Request
{ "managerComment": "Duplicate of Initiative #12." }  // comment REQUIRED

// Response 204 No Content
```

**Idea status values:** `Draft` → `UnderReview` → `Approved` or `Rejected`

---

## Projects

**Role scoping:** Operators see only projects they own. Managers and Leadership see all projects.

| Method | Path | Role | Description |
|--------|------|------|-------------|
| POST | `/projects` | Manager | Create a project directly |
| POST | `/ideas/{id}/convert` | Manager | Convert approved idea to project |
| GET | `/projects` | Any | List projects (role-scoped) |
| GET | `/projects/{id}` | Any | Project detail |
| PUT | `/projects/{id}` | Manager | Update project fields |
| POST | `/projects/{id}/start` | Manager | Planning → InProgress |
| POST | `/projects/{id}/complete` | Manager | InProgress → Completed |
| POST | `/projects/{id}/block` | Manager | InProgress → Blocked |
| POST | `/projects/{id}/unblock` | Manager | Blocked → InProgress |
| POST | `/projects/{id}/cancel` | Manager | Any → Cancelled |
| GET | `/projects/{id}/timeline` | Any | Full audit timeline |
| GET | `/projects/{id}/snapshots` | Manager/Leadership | Full snapshot history |

### POST `/ideas/{id}/convert`
```json
// Request
{
  "title": "Route Optimisation Project",
  "description": "Implement ML-based route planning...",
  "priority": "High",          // "Low" | "Medium" | "High" | "Critical"
  "ownerId": "uuid",
  "estimatedCost": 15000.00,
  "deadline": "2026-09-01T00:00:00Z"
}

// Response 201 — ProjectSummaryDto
```

### POST `/projects/{id}/block`
```json
// Request
{ "reason": "Waiting on procurement approval for vendor contract." }  // REQUIRED

// Response 204 No Content
```

### POST `/projects/{id}/cancel`
```json
// Request
{ "reason": "Strategic priority shift — deprioritised by board." }  // REQUIRED

// Response 204 No Content
```

**Project status values:** `Planning` → `InProgress` ↔ `Blocked` → `Completed` | `Cancelled`

### GET `/projects/{id}/timeline`
Returns the chronological audit history for a project.
```json
[
  {
    "action": "Created",
    "actorName": "João Bernardo",
    "timestamp": "2026-05-14T10:00:00Z",
    "reason": null
  },
  {
    "action": "Blocked",
    "actorName": "João Bernardo",
    "timestamp": "2026-05-14T15:30:00Z",
    "reason": "Waiting on procurement."
  }
]
```

---

## Results (ROI)

One result record per project. Upsert semantics — calling PUT multiple times updates the existing record.

| Method | Path | Role | Description |
|--------|------|------|-------------|
| GET | `/projects/{id}/result` | Any | Get project result |
| PUT | `/projects/{id}/result` | Manager | Record estimated or actual ROI |

### PUT `/projects/{id}/result`
```json
// Request — provide any combination of estimated/actual fields
{
  "estimatedRevenue": 50000,
  "estimatedSavings": 20000,
  "estimatedCost": 15000,
  "actualRevenue": 48000,
  "actualSavings": 22000,
  "actualCost": 16500,
  "paybackPeriodMonths": 8,
  "notes": "Slightly over budget but delivery time target met."
}

// Response 200
{
  "id": "uuid",
  "projectId": "uuid",
  "estimatedRevenue": 50000,
  "estimatedCost": 15000,
  "estimatedROI": 366.67,       // computed: (50000+20000-15000)/15000*100
  "actualRevenue": 48000,
  "actualCost": 16500,
  "actualROI": 327.27,          // computed
  "paybackPeriodMonths": 8,
  "notes": "...",
  "recordedAt": "2026-05-14T..."
}
```

ROI formula: `(Revenue + Savings - Cost) / Cost × 100`. Returns `null` if cost is null or zero.

---

## Dashboard

| Method | Path | Role | Description |
|--------|------|------|-------------|
| GET | `/dashboard/summary` | Manager/Leadership | All KPI metrics |

### GET `/dashboard/summary`
```json
{
  "totalIdeas": 47,
  "approvedIdeas": 18,
  "rejectedIdeas": 9,
  "pendingIdeas": 5,
  "conversionRate": 38.3,
  "activeProjects": 11,
  "blockedProjects": 3,
  "completedProjects": 7,
  "totalRoi": 1240.5,
  "averageCompletionDays": 42.0,
  "bottleneckIndex": 21.4,
  "blockedProjectList": [
    {
      "id": "uuid",
      "title": "Route Optimisation",
      "ownerId": "uuid",
      "blockedReason": "Waiting on procurement approval.",
      "daysBlocked": 12
    }
  ]
}
```

---

## Users (Gamification)

| Method | Path | Role | Description |
|--------|------|------|-------------|
| GET | `/users/me/points` | Operator | Own points summary |
| GET | `/users/me/points/ledger` | Operator | Own point history |
| GET | `/users/{id}/points` | Manager/Leadership | Any user's points |

### GET `/users/me/points`
```json
{ "userId": "uuid", "userName": "Elias Freitas", "points": 85 }
```

### GET `/users/me/points/ledger`
```json
[
  {
    "id": "uuid",
    "points": 25,
    "reason": "Idea approved",
    "referenceType": "Idea",
    "referenceId": "uuid",
    "awardedAt": "2026-05-14T..."
  }
]
```

**Point awards:** Submit idea → +10 pts · Idea approved → +25 pts · Project completed → +50 pts

---

## Strategic Guidelines

| Method | Path | Role | Description |
|--------|------|------|-------------|
| GET | `/guidelines` | Any | List all guidelines |
| GET | `/guidelines/{id}` | Any | Guideline detail |
| POST | `/guidelines` | Manager | Create guideline |
| PUT | `/guidelines/{id}` | Manager | Update guideline |
| DELETE | `/guidelines/{id}` | Manager | Delete guideline |

Guidelines are organisational priority anchors. Operators link ideas to guidelines to signal strategic alignment.

---

## HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | Success with body |
| 201 | Created — includes `Location` header |
| 204 | Success with no body (mutations) |
| 400 | Validation error — `errors` object included |
| 401 | Authentication required or token expired |
| 403 | Authenticated but insufficient role |
| 404 | Resource not found |
| 409 | Conflict — invalid state transition |
| 500 | Internal server error |

# Mobile App — Screens

All screens are in `mobile/src/screens/`. Each screen is a self-contained functional component that owns its own data fetching and mutation logic.

---

## Authentication

### `LoginScreen`
**Path:** `src/screens/auth/LoginScreen.tsx`
**Route:** shown when `session === null`
**Role:** Public

The entry point for all users. Handles credential input, API authentication, token persistence, and Zustand store population.

**User actions:**
- Enter email and password
- Tap "Sign In"

**On success:**
- `POST /auth/login` called
- All session fields written to SecureStore
- Zustand `setSession()` called
- `AppNavigator` re-renders to the appropriate role screen automatically

**On failure:**
- Alert shown with error message from API (e.g. "Invalid credentials")

**Edge cases handled:**
- Empty fields: validation alert before API call
- Network error: alert with error message
- Loading state: spinner replaces button during request

---

## Operator Screens

### `MyIdeasScreen`
**Path:** `src/screens/operator/MyIdeasScreen.tsx`
**Route:** Operator stack root
**API:** `GET /ideas`

The Operator's home screen. Lists all ideas submitted by the current user.

**Features:**
- Ideas displayed as cards: title, status (colour-coded), priority, problem snippet
- `UnderReview` ideas show in amber, `Approved` in green, `Rejected` in red, `Draft` in grey
- Pull-to-refresh re-fetches the list
- Empty state message when no ideas exist
- "＋ New Idea" button navigates to `SubmitIdeaScreen`
- Tapping a card navigates to `IdeaDetailScreen`
- "Sign Out" at the bottom

---

### `SubmitIdeaScreen`
**Path:** `src/screens/operator/SubmitIdeaScreen.tsx`
**Route:** Operator stack (`SubmitIdea`)
**API:** `POST /ideas`

Form screen for creating a new idea.

**Fields:**
- Title (required)
- Problem — what problem does this solve? (required)
- Description — full explanation (required)

**On submit:**
- Validates all fields non-empty
- Calls `POST /ideas`
- Invalidates `['ideas']` cache
- Shows success alert → navigates back to `MyIdeasScreen` on OK

**On error:**
- Alert with API error message

---

### `IdeaDetailScreen`
**Path:** `src/screens/operator/IdeaDetailScreen.tsx`
**Route:** Operator stack (`IdeaDetail`, receives `{ id: string }`)
**API:** `GET /ideas/{id}`, `POST /ideas/{id}/submit`

Full detail view for a single idea.

**Displays:**
- Title, status badge, priority
- Problem statement
- Full description
- Manager comment (if present — shown in a highlighted yellow box)
- Creation date

**Conditional actions:**
- **Status = Draft:** "Submit for Review" button visible
  - Calls `POST /ideas/{id}/submit`
  - Invalidates `['ideas']` and `['idea', id]`
  - Status updates to `UnderReview` and button disappears
- **Other statuses:** no action buttons shown

---

## Manager Screens

The Manager view uses a bottom tab navigator with two tabs: **Ideas** and **Projects**.

### `IdeaQueueScreen`
**Path:** `src/screens/manager/IdeaQueueScreen.tsx`
**Route:** Manager → Ideas tab root
**API:** `GET /ideas`

All ideas submitted across the organisation, sorted with `UnderReview` first.

**Features:**
- `UnderReview` cards highlighted with amber border and background
- Status colour-coded for all ideas
- Pull-to-refresh
- Tapping navigates to `ManagerIdeaDetailScreen`

---

### `ManagerIdeaDetailScreen`
**Path:** `src/screens/manager/ManagerIdeaDetailScreen.tsx`
**Route:** Manager → Ideas tab (`ManagerIdeaDetail`, receives `{ id: string }`)
**API:** `GET /ideas/{id}`, `POST /ideas/{id}/approve`, `POST /ideas/{id}/reject`

Full idea review screen with decision controls.

**Displays:**
- Title, status badge, priority
- Problem and description
- Existing manager comment (if any)

**Conditional actions (only when status = `UnderReview`):**
- Comment text input — optional for approval, **required for rejection**
- **Approve** button (green): calls `POST /ideas/{id}/approve` with optional comment
- **Reject** button (red): validates comment non-empty, then calls `POST /ideas/{id}/reject`
- Both actions invalidate `['idea', id]` and `['ideas', 'all']`

**When already resolved:**
- "This idea has already been approved/rejected." note
- No action buttons shown

---

### `ProjectListScreen`
**Path:** `src/screens/manager/ProjectListScreen.tsx`
**Route:** Manager → Projects tab root
**API:** `GET /projects`

All projects across the organisation.

**Features:**
- Status colour-coded: Planning (grey), InProgress (blue), Blocked (red), Completed (green)
- Blocked project cards shown with red border and background
- Blocked reason shown (truncated to one line)
- Pull-to-refresh
- Tapping navigates to `ProjectDetailScreen`
- "Sign Out" at the bottom

---

### `ProjectDetailScreen`
**Path:** `src/screens/manager/ProjectDetailScreen.tsx`
**Route:** Manager → Projects tab (`ProjectDetail`, receives `{ id: string }`)
**API:** `GET /projects/{id}`, `POST /projects/{id}/start|complete|block|unblock`

Full project detail with context-aware state transition controls.

**Displays:**
- Title, status badge, priority
- Description
- Blocked reason (if blocked — shown in a red highlighted box)
- Estimated cost, deadline, start date, completion date (when present)

**State-gated actions:**

| Status | Actions shown |
|--------|--------------|
| `Planning` | "Start Project" button (blue) |
| `InProgress` | "Mark Complete" (green) + Block reason input + "Block Project" (red) |
| `Blocked` | "Unblock Project" (amber) |
| `Completed` | No actions |
| `Cancelled` | No actions |

- Block requires a non-empty reason (validated before API call)
- All actions invalidate `['project', id]` and `['projects']`
- Spinner shown during in-flight action

---

## Leadership Screens

### `DashboardScreen`
**Path:** `src/screens/leadership/DashboardScreen.tsx`
**Route:** Leadership stack root
**API:** `GET /dashboard/summary` (auto-refreshes every 60 seconds)

The full operational view of the innovation pipeline.

**KPI sections:**

**Ideas:**
| Metric | Display |
|--------|---------|
| Total | plain number |
| Approved | green number |
| Rejected | red number |
| Pending Review | amber number |
| Conversion Rate | blue `X.X%` |

**Projects:**
| Metric | Display |
|--------|---------|
| Active | blue number |
| Blocked | red number |
| Completed | green number |
| Avg Completion | `Xd` |
| Bottleneck Index | green if ≤ 30%, red if > 30% |
| Total ROI | blue `X%` |

**Blocked Projects section (shown only when blockedProjectList is non-empty):**
- Sorted by `daysBlocked` descending (longest blocked first)
- Each card: title, days blocked (red), reason

**Auto-refresh:** `refetchInterval: 60_000` keeps the dashboard live.

**Sign Out:** button at bottom of scroll view.

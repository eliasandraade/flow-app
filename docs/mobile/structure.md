# Mobile App — Structure

**Stack:** React Native (Expo SDK 51) · TypeScript · React Navigation 6 · TanStack Query v5 · Zustand v5 · expo-secure-store

---

## Folder Structure

```
mobile/
├── App.tsx                          # Root component — hydrates auth, renders AppNavigator
├── app.json                         # Expo configuration
├── package.json
├── tsconfig.json
└── src/
    ├── api/
    │   └── client.ts                # apiFetch wrapper + logout utility
    ├── navigation/
    │   └── AppNavigator.tsx         # Auth gate + role-based routing
    ├── screens/
    │   ├── auth/
    │   │   └── LoginScreen.tsx
    │   ├── operator/
    │   │   ├── MyIdeasScreen.tsx
    │   │   ├── SubmitIdeaScreen.tsx
    │   │   └── IdeaDetailScreen.tsx
    │   ├── manager/
    │   │   ├── IdeaQueueScreen.tsx
    │   │   ├── ManagerIdeaDetailScreen.tsx
    │   │   ├── ProjectListScreen.tsx
    │   │   └── ProjectDetailScreen.tsx
    │   └── leadership/
    │       └── DashboardScreen.tsx
    ├── store/
    │   └── authStore.ts             # Zustand auth store
    └── types/
        └── api.ts                   # TypeScript interfaces for all API DTOs
```

---

## API Client (`src/api/client.ts`)

The `apiFetch` function is the single point of contact with the backend. All screens use it — never `fetch` directly.

**What it does:**
- Reads the Bearer token from SecureStore and attaches it to every request
- Returns the parsed JSON response body typed as `T`
- On `401 Unauthorized`: clears SecureStore, clears Zustand session, throws — the app automatically returns to LoginScreen
- On any non-2xx: extracts the error message from ProblemDetails and throws
- On `204 No Content`: returns `undefined` without attempting JSON parse

**`logout(refreshToken)`:**
- Calls `POST /auth/logout` (best-effort, swallows errors)
- Clears all SecureStore keys
- Clears Zustand session

**API base URL:**
```typescript
export const API_BASE = 'http://10.0.2.2:5153/api/v1';
// Android emulator: 10.0.2.2
// iOS simulator:    localhost
// Real device:      machine's LAN IP (e.g. 192.168.1.X)
```

---

## State Management (`src/store/authStore.ts`)

Zustand manages the authentication session in memory. SecureStore handles persistence across app restarts.

```typescript
interface AuthSession {
  accessToken: string;
  refreshToken: string;
  userId: string;
  name: string;
  email: string;
  role: 'Operator' | 'Manager' | 'Leadership';
}

interface AuthState {
  session: AuthSession | null;   // null = not authenticated
  hydrated: boolean;             // false until SecureStore has been read
  setSession(session): void;
  clearSession(): void;
  setHydrated(): void;
}
```

**Startup hydration (App.tsx):**
1. App renders a spinner while `hydrated === false`
2. `useEffect` reads SecureStore on mount
3. If a valid session exists, `setSession()` is called
4. `setHydrated()` is called regardless — removes the spinner
5. `AppNavigator` renders based on `session` state

---

## Navigation (`src/navigation/AppNavigator.tsx`)

Navigation is role-based. The `NavigationContainer` renders a different navigator tree depending on `session.role`.

```
NavigationContainer
  │
  ├── session === null
  │     AuthStack (header hidden)
  │       └── LoginScreen
  │
  ├── role === 'Operator'
  │     OperatorStack
  │       ├── MyIdeasScreen        ("My Ideas")
  │       ├── SubmitIdeaScreen     ("Submit Idea")
  │       └── IdeaDetailScreen     ("Idea")
  │
  ├── role === 'Manager'
  │     ManagerTabs (bottom tabs)
  │       ├── Ideas tab
  │       │     ManagerIdeasStack
  │       │       ├── IdeaQueueScreen        ("Ideas")
  │       │       └── ManagerIdeaDetailScreen ("Idea Review")
  │       └── Projects tab
  │             ManagerProjectsStack
  │               ├── ProjectListScreen     ("Projects")
  │               └── ProjectDetailScreen   ("Project")
  │
  └── role === 'Leadership'
        LeadershipStack
          └── DashboardScreen       ("Dashboard")
```

**Key implementation note:** Each role uses its own `createNativeStackNavigator()` instance. React Navigation requires a 1:1 relationship between a navigator instance and the `Navigator` component that renders it. Sharing an instance across multiple components causes a runtime invariant error.

---

## Data Fetching (TanStack Query)

All API data fetching uses TanStack Query. The `QueryClient` is configured in `App.tsx` and provided via `QueryClientProvider`.

**Default configuration:**
```typescript
new QueryClient({
  defaultOptions: {
    queries: { retry: 1, staleTime: 30_000 }
  }
})
```

**Query key conventions:**

| Key | Data |
|-----|------|
| `['ideas']` | Operator's own ideas |
| `['ideas', 'all']` | All ideas (Manager) |
| `['idea', id]` | Single idea detail |
| `['projects']` | All projects |
| `['project', id]` | Single project detail |
| `['dashboard']` | Dashboard summary |

**After every mutation:**
- Call `queryClient.invalidateQueries({ queryKey: [...] })` to mark the relevant cache as stale
- TanStack Query automatically re-fetches on the next render where the data is needed
- This is how status updates propagate through the UI without manual state management

**Pull-to-refresh:**
```typescript
const { data, isFetching, refetch } = useQuery({ ... });

<FlatList
  onRefresh={() => { refetch(); }}
  refreshing={isFetching}        // isFetching (not isLoading) for correct behaviour
  ...
/>
```
Note: `isFetching` is `true` on every in-flight request, including background re-fetches. `isLoading` is only `true` on the initial fetch when no cached data exists.

---

## TypeScript Types (`src/types/api.ts`)

All API response shapes are typed as interfaces matching the backend DTOs exactly (camelCase, since ASP.NET Core serialises to camelCase by default).

Types defined: `AuthResult`, `IdeaSummary`, `IdeaDetail`, `ProjectSummary`, `ProjectDetail`, `BlockedProject`, `DashboardSummary`.

These types are imported in screens and the API client — never redefined inline.

# Flow Mobile App — UI/UX Polish Design Spec

**Date:** 2026-05-14
**Scope:** Visual consistency and perceived quality improvement across all 9 mobile screens.
**Approach:** Foundation first — design system + 5 shared components, then screen-by-screen polish.
**Constraint:** No new libraries, no architecture changes, no animations, no logic changes.

---

## 1. Design System (`src/theme.ts`)

A single exported `theme` constant. Every screen imports from this file instead of using hardcoded values.

### 1.1 Colors

```typescript
colors: {
  primary: '#2563EB',
  primarySurface: '#EEF2FF',

  text: {
    primary: '#111827',      // headings, body text
    secondary: '#6B7280',    // labels, metadata — use text.secondary for these
    muted: '#9CA3AF',        // placeholders, empty states — use text.muted for these
    inverse: '#FFFFFF',      // text on colored backgrounds
  },

  surface: {
    background: '#F9FAFB',   // screen background
    card: '#FFFFFF',         // card background
    border: '#E5E7EB',       // card and divider borders
    inputBorder: '#D1D5DB',  // form input borders
  },

  status: {
    draft:       { bg: '#F3F4F6', text: '#6B7280' },
    underReview: { bg: '#FFFBEB', text: '#D97706' },
    approved:    { bg: '#ECFDF5', text: '#059669' },
    rejected:    { bg: '#FEF2F2', text: '#DC2626' },
    inProgress:  { bg: '#EEF2FF', text: '#2563EB' },
    blocked:     { bg: '#FFF7ED', text: '#C2410C' },  // orange warning, distinct from rejected
    completed:   { bg: '#ECFDF5', text: '#059669' },
    cancelled:   { bg: '#F3F4F6', text: '#6B7280' },
  },
}
```

### 1.2 Typography

```typescript
typography: {
  display: { fontSize: 36, fontWeight: '700' },  // login title
  heading: { fontSize: 22, fontWeight: '700' },  // screen headings
  title:   { fontSize: 18, fontWeight: '600' },  // card titles, section headers
  body:    { fontSize: 15, fontWeight: '400' },  // body text
  label:   { fontSize: 13, fontWeight: '500' },  // metadata, form labels (color: text.secondary)
  caption: { fontSize: 12, fontWeight: '400' },  // timestamps, hints (color: text.muted)
  kpi:     { fontSize: 32, fontWeight: '700' },  // dashboard metric numbers
}
```

### 1.3 Spacing

```typescript
spacing: {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 24,
  xxl: 32,
}
```

### 1.4 Border Radius

```typescript
radius: {
  sm: 6,
  md: 8,
  lg: 12,
  full: 999,
}
```

---

## 1.5 Status Normalization (`src/utils/normalizeStatus.ts`)

The backend returns status values in PascalCase (e.g. `"UnderReview"`, `"InProgress"`).
The UI theme uses camelCase keys (e.g. `"underReview"`, `"inProgress"`).
A normalization utility converts backend values before they reach `StatusBadge`.

```typescript
// Exhaustive mapping — new backend values are caught at the call site as TS errors
const STATUS_MAP: Record<string, StatusKey> = {
  Draft:       'draft',
  UnderReview: 'underReview',
  Approved:    'approved',
  Rejected:    'rejected',
  InProgress:  'inProgress',
  Blocked:     'blocked',
  Completed:   'completed',
  Cancelled:   'cancelled',
};

export function normalizeStatus(raw: string): StatusKey {
  const key = STATUS_MAP[raw];
  if (!key) {
    if (__DEV__) console.warn(`[normalizeStatus] Unknown status: "${raw}". Falling back to 'draft'.`);
    return 'draft';
  }
  return key;
}
```

`StatusKey` is the same union type used in `StatusBadge.tsx`.
Every screen calls `normalizeStatus(item.status)` before passing the value to `StatusBadge`.
This is the single point of truth for the PascalCase → camelCase mapping.

---

## 2. Shared Components (`src/components/`)

Five files. No new libraries. All built on React Native primitives + `theme`.

### 2.1 `ScreenContainer.tsx`

Wraps every screen's root view. Provides safe-area awareness, consistent background color, and horizontal padding.

```
Props:
  children:     ReactNode
  style?:       ViewStyle
  scrollable?:  boolean  (default false — wraps in ScrollView when true)

Root element: SafeAreaView (from react-native-safe-area-context)
  — avoids notch, status bar, and home indicator overlap on all devices
  — edges: ['top', 'left', 'right', 'bottom']

Layout:
  flex: 1
  backgroundColor: theme.colors.surface.background
  paddingHorizontal: theme.spacing.xl (24)
```

Every screen uses `ScreenContainer` as its outermost element, replacing the per-screen container styles.

### 2.2 `Button.tsx`

```
Props:
  variant:   'primary' | 'success' | 'danger' | 'secondary'
  size?:     'sm' | 'md' | 'lg'   (default: 'md')
  label:     string
  onPress:   () => void
  loading?:  boolean              (default: false)
  disabled?: boolean              (default: false)

Variant styles:
  primary   — backgroundColor: #2563EB, textColor: #FFFFFF
  success   — backgroundColor: #059669, textColor: #FFFFFF
  danger    — backgroundColor: #DC2626, textColor: #FFFFFF
  secondary — backgroundColor: #FFFFFF, borderWidth: 1, borderColor: #E5E7EB, textColor: #111827

Size → paddingVertical / paddingHorizontal:
  sm  → 8  / 12
  md  → 12 / 20
  lg  → 14 / 24

Loading state:
  — label hidden (opacity 0, kept in layout to prevent width shift)
  — ActivityIndicator centered absolutely, color matches text color for that variant
  — button is non-interactive while loading
  — onPress is suppressed when loading=true (guard in the handler, not just via disabled)
    to prevent double-trigger on rapid taps before state propagates

Disabled:
  — opacity: 0.5
  — non-interactive (pointerEvents: 'none')

Common:
  borderRadius: theme.radius.md (8)
  fontWeight: '600', fontSize matching body (15)

Hierarchy rule: Only one `primary` variant button per screen.
```

### 2.3 `StatusBadge.tsx`

```
Props:
  status: 'draft' | 'underReview' | 'approved' | 'rejected'
        | 'inProgress' | 'blocked' | 'completed' | 'cancelled'

Behavior:
  — maps status key to theme.colors.status[key].bg + .text
  — display labels:
      draft       → "Draft"
      underReview → "Under Review"
      approved    → "Approved"
      rejected    → "Rejected"
      inProgress  → "In Progress"
      blocked     → "Blocked"
      completed   → "Completed"
      cancelled   → "Cancelled"
  — unknown status: falls back to draft colors; logs console.warn(__DEV__ guard)

Layout:
  paddingVertical: 4, paddingHorizontal: 10
  borderRadius: theme.radius.full (999)
  font: theme.typography.label (13px, weight 500)
  alignSelf: 'flex-start'  (badge does not stretch)
```

### 2.4 `Card.tsx`

```
Props:
  children:  ReactNode
  onPress?:  () => void
  style?:    ViewStyle
  padding?:  number  (default: theme.spacing.lg = 16)

Layout:
  backgroundColor: theme.colors.surface.card (#FFF)
  borderWidth: 1, borderColor: theme.colors.surface.border (#E5E7EB)
  borderRadius: theme.radius.md (8)
  padding: prop value or default

Shadow (visual lift):
  iOS:     shadowColor '#000', shadowOffset {0,1}, shadowOpacity 0.06, shadowRadius 3
  Android: elevation 2

When onPress provided: wraps children in TouchableOpacity (activeOpacity: 0.7, minHeight: 64)
When no onPress: plain View

Accessibility:
  — TouchableOpacity minHeight: 64 ensures adequate touch target for list item cards
  — applies to all Card instances used in lists (MyIdeasScreen, IdeaQueueScreen,
    ProjectListScreen, DashboardScreen blocked rows)
```

### 2.5 `FormInput.tsx`

```
Props:
  label:           string
  value:           string
  onChangeText:    (text: string) => void
  placeholder?:    string
  multiline?:      boolean
  numberOfLines?:  number
  secureTextEntry?: boolean
  error?:          string

Layout (top to bottom):
  [label text]       — marginBottom: 6
  [TextInput]        — marginBottom: 4
  [error text]       — (only rendered when error prop is truthy)

Label:
  font: theme.typography.label (13px, weight 500)
  color: theme.colors.text.secondary

Input:
  minHeight: 48px (grows for multiline)
  borderWidth: 1
  borderColor: error ? '#DC2626' : theme.colors.surface.inputBorder
  borderRadius: theme.radius.md (8)
  paddingHorizontal: theme.spacing.md (12)
  paddingVertical: theme.spacing.md (12)
  font: theme.typography.body (15px)
  color: theme.colors.text.primary
  placeholderTextColor: theme.colors.text.muted

Error text:
  color: '#DC2626'  (theme.colors.status.rejected.text — danger color, not secondary)
  font: theme.typography.caption (12px)
```

---

## 3. Screen-Level Polish

### Global Rules (apply to every screen)

- Outermost element: `ScreenContainer` (replaces per-screen container styles)
- Section spacing: `xxl` (32px) between major sections, `xl` (24px) between sub-sections
- No dense vertical stacking — breathe between sections
- List item hierarchy: `title` (top, bold) → `metadata` (middle, label/secondary) → `StatusBadge` (bottom-right)
- One `primary` Button per screen maximum

---

### 3.1 `LoginScreen.tsx`

| Element | Before | After |
|---|---|---|
| App title | fontSize varies, color #1E3A5F | `typography.display`, `text.primary` |
| Subtitle | ad hoc | `typography.body`, `text.secondary` |
| Inputs | inline TextInput styles | `FormInput` component |
| Sign In button | inline TouchableOpacity | `Button variant="primary" size="lg"` |
| Error message | ad hoc | danger color, `typography.label` |
| Background | #F9FAFB hardcoded | via `ScreenContainer` |

---

### 3.2 `MyIdeasScreen.tsx`

| Element | Before | After |
|---|---|---|
| Idea card | inline card styles | `Card` with `onPress` |
| Idea title | ad hoc | `typography.title`, `text.primary` |
| Metadata (date, category) | ad hoc | `typography.label`, `text.secondary` |
| Status | inline badge | `StatusBadge` |
| Badge alignment | varies | bottom-right within card row |
| Empty state | ad hoc | centered, `text.muted`, `typography.body` |
| New Idea button | inline | `Button variant="primary"` |
| ActivityIndicator | hardcoded color | `theme.colors.primary` |

---

### 3.3 `SubmitIdeaScreen.tsx`

| Element | Before | After |
|---|---|---|
| All text inputs | inline TextInput | `FormInput` component |
| Section headings | ad hoc | `typography.title` |
| Submit button | inline | `Button variant="primary" size="lg"` |
| Cancel button | inline | `Button variant="secondary" size="md"` |
| Character hints | ad hoc | `typography.caption`, `text.muted` |
| Section spacing | tight | `xxl` between sections |

---

### 3.4 `IdeaDetailScreen.tsx`

| Element | Before | After |
|---|---|---|
| Screen title | ad hoc | `typography.heading` |
| Idea title | ad hoc | `typography.title` |
| Status | inline badge | `StatusBadge` |
| Metadata rows | ad hoc | `typography.label`, `text.secondary` |
| Body text | ad hoc | `typography.body`, `text.primary` |
| Submit for Review | inline | `Button variant="primary"` |
| Withdraw | inline | `Button variant="danger"` |
| Comments header | ad hoc | `typography.title` |
| Comment cards | inline | `Card` component |
| Comment timestamp | ad hoc | `typography.caption`, `text.muted` |
| Comment input | inline TextInput | `FormInput` (no label — placeholder only) |

---

### 3.5 `IdeaQueueScreen.tsx`

| Element | Before | After |
|---|---|---|
| Filter tabs | ad hoc | active: `primary` underline + text; inactive: `text.secondary` |
| Idea card | inline | `Card` with `onPress` |
| Idea title | ad hoc | `typography.title` |
| Submitter + date | ad hoc | `typography.label`, `text.secondary` |
| Status badge | inline | `StatusBadge`, bottom-right aligned |
| Empty state | ad hoc | centered, `text.muted` |

---

### 3.6 `ManagerIdeaDetailScreen.tsx`

| Element | Before | After |
|---|---|---|
| Layout | mirrors IdeaDetail | same field/label/body pattern |
| Status | inline badge | `StatusBadge` |
| Approve | inline | `Button variant="success"` |
| Reject | inline | `Button variant="danger"` |
| Secondary actions | inline | `Button variant="secondary"` |
| Rejection reason input | inline TextInput | `FormInput` with label |
| Metadata | ad hoc | `typography.label`, `text.secondary` |

Action hierarchy: Approve (success) is the primary positive action. Reject (danger) is the destructive action. No `primary` variant here — both actions are decisive but neither is a neutral CTA.

---

### 3.7 `ProjectListScreen.tsx`

| Element | Before | After |
|---|---|---|
| Project card | inline | `Card` with `onPress` |
| Project name | ad hoc | `typography.title` |
| Status | inline badge | `StatusBadge`, bottom-right |
| Metadata (owner, phase) | ad hoc | `typography.label`, `text.secondary` |
| Empty state | ad hoc | centered, `text.muted` |

---

### 3.8 `ProjectDetailScreen.tsx`

| Element | Before | After |
|---|---|---|
| Section headers | ad hoc | `typography.title` + bottom border separator |
| Field labels | ad hoc | `typography.label`, `text.secondary` |
| Field values | ad hoc | `typography.body`, `text.primary` |
| Status | inline badge | `StatusBadge`, prominent position below title |
| Block button | inline | `Button variant="danger"` |
| Complete button | inline | `Button variant="success"` |
| Cancel button | inline | `Button variant="secondary"` |
| ROI numbers | ad hoc | `typography.title`, bold |
| Timeline entries | inline | `Card` + `typography.label` + `typography.caption` |
| Section spacing | dense | `xxl` between Overview / ROI / Timeline / Actions |

---

### 3.9 `DashboardScreen.tsx`

| Element | Before | After |
|---|---|---|
| KPI grid | inline cards | 2-column `Card` grid, padding `xl`, `alignItems: 'stretch'` on row so all cards equal height |
| KPI card content | varies | `justifyContent: 'center'` inside card so number + label are vertically centered |
| KPI number | ad hoc fontSize | `typography.kpi` (32px, bold), `text.primary` |
| KPI label | ad hoc | `typography.label`, `text.secondary` |
| KPI sub-text | ad hoc | `typography.caption`, `text.muted` |
| Section header | ad hoc | `typography.heading` |
| Blocked projects section | no emphasis | subtle `#FFF7ED` row/card tint to signal priority |
| Blocked project name | ad hoc | `typography.title` |
| Blocked project status | inline | `StatusBadge` (renders "Blocked" in orange) |
| Section spacing | tight | `xxl` between KPI grid and Blocked Projects |

---

## 4. File Manifest

### New files

```
mobile/src/theme.ts
mobile/src/utils/normalizeStatus.ts
mobile/src/components/ScreenContainer.tsx
mobile/src/components/Button.tsx
mobile/src/components/StatusBadge.tsx
mobile/src/components/Card.tsx
mobile/src/components/FormInput.tsx
```

### Modified files

```
mobile/src/screens/auth/LoginScreen.tsx
mobile/src/screens/operator/MyIdeasScreen.tsx
mobile/src/screens/operator/SubmitIdeaScreen.tsx
mobile/src/screens/operator/IdeaDetailScreen.tsx
mobile/src/screens/manager/IdeaQueueScreen.tsx
mobile/src/screens/manager/ManagerIdeaDetailScreen.tsx
mobile/src/screens/manager/ProjectListScreen.tsx
mobile/src/screens/manager/ProjectDetailScreen.tsx
mobile/src/screens/leadership/DashboardScreen.tsx
```

**Total: 6 new files, 9 modified files.**

---

## 5. Definition of Done

A screen is complete when:

1. All hardcoded color/font/spacing values are replaced with `theme` references
2. `ScreenContainer` (SafeAreaView-based) is the outermost element
3. Cards use the `Card` component; list-item cards have `minHeight: 64` touch target
4. Status values are normalized via `normalizeStatus()` before reaching `StatusBadge`
5. Status badges use `StatusBadge` with the normalized camelCase key
6. Buttons use `Button` with the correct variant; loading prop suppresses double-tap
7. Form inputs use `FormInput`
8. Section spacing follows the `xxl`/`xl` hierarchy
9. List items follow the `title → metadata → badge` hierarchy
10. No more than one `primary` button variant appears on the screen
11. Dashboard KPI rows use `alignItems: 'stretch'`; KPI card content uses `justifyContent: 'center'`

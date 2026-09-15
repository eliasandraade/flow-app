# Flow — Design System

A minimal, consistent design system for the Flow mobile application. All values here are used directly in `StyleSheet.create()` across all screens. The goal is visual consistency without introducing a design framework.

---

## Colours

```typescript
export const Colors = {
  // Brand
  primary:    '#2563EB',   // Blue — buttons, links, active state
  primaryDark:'#1D4ED8',   // Blue dark — pressed state

  // Semantic
  success:    '#059669',   // Green — approved, completed, positive values
  warning:    '#D97706',   // Amber — under review, blocked highlights, caution
  danger:     '#DC2626',   // Red — rejected, blocked, errors
  dangerLight:'#FEF2F2',   // Red background — blocked cards
  dangerBorder:'#FECACA',  // Red border — blocked cards

  // Neutrals
  background: '#F9FAFB',   // Screen background
  card:       '#FFFFFF',   // Card background
  border:     '#E5E7EB',   // Default card border
  textPrimary:'#111827',   // Titles and important text
  textSecondary:'#374151', // Body text
  textMuted:  '#6B7280',   // Labels, subtitles, metadata
  textPlaceholder:'#9CA3AF',// Input placeholders, empty states

  // Status badge backgrounds
  badgeBlue:  '#EEF2FF',   // InProgress, general badge
  badgeAmber: '#FEF3C7',   // UnderReview
  badgeGreen: '#D1FAE5',   // Approved / Completed

  // Accent text for badges
  accentBlue: '#4338CA',
  accentAmber:'#92400E',
  accentGreen:'#065F46',
};
```

### Status → Colour Mapping

| Status | Text colour | Background |
|--------|-------------|------------|
| Draft | `textMuted` (#6B7280) | — |
| UnderReview | `warning` (#D97706) | `badgeAmber` |
| Approved | `success` (#059669) | `badgeGreen` |
| Rejected | `danger` (#DC2626) | — |
| Planning | `textMuted` (#6B7280) | — |
| InProgress | `primary` (#2563EB) | `badgeBlue` |
| Blocked | `danger` (#DC2626) | `dangerLight` |
| Completed | `success` (#059669) | `badgeGreen` |
| Cancelled | `textMuted` (#9CA3AF) | — |

---

## Typography

```typescript
export const Typography = {
  screenTitle: {
    fontSize: 22,
    fontWeight: 'bold' as const,
    color: '#1E3A5F',
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold' as const,
    color: '#111827',
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '600' as const,
    color: '#111827',
  },
  sectionLabel: {
    fontSize: 13,
    fontWeight: '600' as const,
    color: '#374151',
  },
  body: {
    fontSize: 15,
    color: '#374151',
    lineHeight: 22,
  },
  meta: {
    fontSize: 13,
    color: '#6B7280',
  },
  badge: {
    fontSize: 13,
    fontWeight: '500' as const,
  },
  kpi: {
    fontSize: 22,
    fontWeight: 'bold' as const,
  },
  kpiLabel: {
    fontSize: 11,
    color: '#6B7280',
    textAlign: 'center' as const,
  },
  buttonText: {
    color: '#FFFFFF',
    fontWeight: '600' as const,
    fontSize: 16,
  },
  emptyState: {
    textAlign: 'center' as const,
    color: '#9CA3AF',
    fontSize: 15,
    lineHeight: 22,
  },
};
```

---

## Spacing

All spacing is based on a 4pt grid.

```typescript
export const Spacing = {
  xs:   4,
  sm:   8,
  md:   12,
  base: 16,   // Standard screen padding
  lg:   20,
  xl:   24,
  xxl:  32,
};
```

**Usage rules:**
- Screen horizontal padding: `16` (base)
- Card internal padding: `14`
- Gap between cards: `12` (md)
- Gap between row items: `8` (sm)
- Section title margin top: `20` (lg)

---

## Component Definitions

### ScreenContainer

Wraps every screen's root view. Provides consistent background and padding.

```typescript
// Usage
<View style={styles.container}>...</View>

// Style
container: {
  flex: 1,
  backgroundColor: '#F9FAFB',
  padding: 16,
}

// For scrollable screens
<ScrollView style={styles.container} contentContainerStyle={{ paddingBottom: 32 }}>
```

---

### Card

Standard list card for ideas and projects.

```typescript
// Base card
card: {
  backgroundColor: '#FFFFFF',
  borderRadius: 8,
  padding: 14,
  marginBottom: 12,
  borderWidth: 1,
  borderColor: '#E5E7EB',
}

// Highlighted card (e.g. UnderReview ideas)
cardHighlight: {
  borderColor: '#FCD34D',
  backgroundColor: '#FFFBEB',
}

// Danger card (e.g. Blocked projects)
cardDanger: {
  borderColor: '#FECACA',
  backgroundColor: '#FEF2F2',
}
```

**Example:**
```typescript
<TouchableOpacity
  style={[styles.card, item.status === 'Blocked' && styles.cardDanger]}
  onPress={() => navigation.navigate('ProjectDetail', { id: item.id })}
>
  <Text style={styles.cardTitle}>{item.title}</Text>
  ...
</TouchableOpacity>
```

---

### StatusBadge

Inline status label used in card meta rows and detail screens.

```typescript
// Inline text style — colour applied dynamically
status: {
  fontSize: 13,
  fontWeight: '500',
}

// Pill badge (for detail screens)
badge: {
  paddingHorizontal: 10,
  paddingVertical: 3,
  borderRadius: 12,
  fontSize: 13,
  fontWeight: '500',
  // backgroundColor and color applied per status
}
```

**Example:**
```typescript
const STATUS_COLORS: Record<string, string> = {
  Draft:       '#6B7280',
  UnderReview: '#D97706',
  Approved:    '#059669',
  Rejected:    '#DC2626',
  Planning:    '#6B7280',
  InProgress:  '#2563EB',
  Blocked:     '#DC2626',
  Completed:   '#059669',
  Cancelled:   '#9CA3AF',
};

<Text style={[styles.status, { color: STATUS_COLORS[item.status] }]}>
  {item.status}
</Text>
```

---

### Button (Primary)

Main action button. Used for form submission and primary screen actions.

```typescript
button: {
  backgroundColor: '#2563EB',
  borderRadius: 8,
  padding: 14,
  alignItems: 'center',
  marginTop: 8,
}
buttonText: {
  color: '#FFFFFF',
  fontWeight: '600',
  fontSize: 16,
}
```

**Variants:**

```typescript
// Success (approve, complete)
buttonSuccess: { backgroundColor: '#059669' }

// Danger (reject, block)
buttonDanger: { backgroundColor: '#DC2626' }

// Warning (unblock)
buttonWarning: { backgroundColor: '#D97706' }

// Secondary / outline (sign out)
buttonSecondary: {
  borderRadius: 8,
  borderWidth: 1,
  borderColor: '#D1D5DB',
  padding: 14,
  alignItems: 'center',
}
buttonSecondaryText: { color: '#374151', fontWeight: '600' }
```

**Hierarchy rule:** One primary action per screen. Secondary actions (destructive, sign out) use outline style.

---

### Input

Text input for forms.

```typescript
input: {
  borderWidth: 1,
  borderColor: '#D1D5DB',
  borderRadius: 8,
  padding: 12,
  backgroundColor: '#FFFFFF',
  fontSize: 15,
}

// Multiline variant
inputMultiline: {
  minHeight: 80,
  textAlignVertical: 'top',
}
```

**Label:**
```typescript
label: {
  fontSize: 14,
  fontWeight: '600',
  color: '#374151',
  marginBottom: 4,
  marginTop: 14,
}
```

---

### KPI Metric Card

Used exclusively on the Leadership Dashboard.

```typescript
// Container (flex: 1 so cards fill row equally)
kpiCard: {
  flex: 1,
  backgroundColor: '#FFFFFF',
  borderRadius: 8,
  padding: 14,
  alignItems: 'center',
  borderWidth: 1,
  borderColor: '#E5E7EB',
}

// Value (colour applied per metric)
kpiValue: {
  fontSize: 22,
  fontWeight: 'bold',
  marginBottom: 4,
}

// Label
kpiLabel: {
  fontSize: 11,
  color: '#6B7280',
  textAlign: 'center',
}
```

**Usage:**
```typescript
<View style={{ flexDirection: 'row', gap: 8, marginBottom: 8 }}>
  <MetricCard label="Active" value={data.activeProjects} color="#2563EB" />
  <MetricCard label="Blocked" value={data.blockedProjects} color="#DC2626" />
  <MetricCard label="Completed" value={data.completedProjects} color="#059669" />
</View>
```

---

### Loading State

Consistent loading indicator across all screens.

```typescript
// Full-screen loading
<ActivityIndicator style={{ flex: 1 }} size="large" color="#2563EB" />

// Inline loading (replaces a button during action)
<ActivityIndicator size="large" color="#2563EB" style={{ marginTop: 20 }} />
```

**Rule:** Never show raw text like "Loading...". Always use `ActivityIndicator`.

---

### Error State

```typescript
errorText: {
  textAlign: 'center',
  color: '#EF4444',
  marginTop: 48,
  padding: 16,
}

// Usage
if (error) return <Text style={styles.errorText}>{(error as Error).message}</Text>;
```

---

### Empty State

```typescript
emptyText: {
  textAlign: 'center',
  color: '#9CA3AF',
  marginTop: 48,
  fontSize: 15,
  lineHeight: 22,
}

// Usage
ListEmptyComponent={
  <Text style={styles.emptyText}>No ideas yet. Tap "+ New Idea" to get started.</Text>
}
```

---

## Layout Patterns

### Meta Row (status + priority in a card)
```typescript
metaRow: {
  flexDirection: 'row',
  gap: 8,
  marginBottom: 4,
}
```

### Section label + content (detail screens)
```typescript
sectionLabel: {
  fontSize: 13,
  fontWeight: '600',
  color: '#374151',
  marginTop: 16,
  marginBottom: 4,
}
```

### Two-button action row (approve + reject)
```typescript
actionsRow: {
  flexDirection: 'row',
  gap: 12,
  marginTop: 16,
}
actionBtn: {
  flex: 1,
  padding: 14,
  borderRadius: 8,
  alignItems: 'center',
}
```

---

## Consistency Checklist

Before adding a new screen, verify:

- [ ] Background is `#F9FAFB`, cards are `#FFFFFF`
- [ ] Screen padding is `16`
- [ ] All loading states use `ActivityIndicator` with `color="#2563EB"`
- [ ] All mutations invalidate the relevant query cache
- [ ] Pull-to-refresh uses `isFetching` (not `isLoading`) for `refreshing` prop
- [ ] Status colours come from the `STATUS_COLORS` map, not hardcoded inline
- [ ] Primary action button is blue; destructive actions are red; secondary is outline
- [ ] Empty list state has a helpful message, not just blank space
- [ ] Error state shows the API error message, not a generic string

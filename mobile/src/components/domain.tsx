import React from 'react';
import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';
import { theme, statusTone } from '../theme';
import { Card, ProgressBar, Txt } from './primitives';
import {
  flowScoreDimensionLabel,
  ideaPriorityLabel,
  ideaStatusLabel,
  projectPriorityLabel,
  projectStageLabel,
  projectStatusLabel,
} from '../i18n/labels';
import { formatCompactCurrency, formatDeadline, formatPercent, formatRelative } from '../utils/format';
import type {
  FlowScore,
  IdeaSummary,
  ProjectStatus,
  ProjectSummary,
} from '../api/types';

// ─── Badges ─────────────────────────────────────────────────────────────────

export function StatusBadge({ status, label }: { status: string; label?: string }) {
  const tone = statusTone(status);

  return (
    <View style={[styles.badge, { backgroundColor: tone.bg, borderColor: tone.border }]}>
      <Txt variant="caption" color={tone.text} style={styles.badgeLabel}>
        {label ?? status}
      </Txt>
    </View>
  );
}

export function IdeaStatusBadge({ status }: { status: IdeaSummary['status'] }) {
  return <StatusBadge status={status} label={ideaStatusLabel[status]} />;
}

export function ProjectStatusBadge({ status }: { status: ProjectStatus }) {
  return <StatusBadge status={status} label={projectStatusLabel[status]} />;
}

/** Priority reads as weight, not as another status, so it stays visually quieter. */
export function PriorityBadge({ priority, kind }: { priority: string; kind: 'idea' | 'project' }) {
  const label =
    kind === 'idea'
      ? ideaPriorityLabel[priority as keyof typeof ideaPriorityLabel] ?? priority
      : projectPriorityLabel[priority as keyof typeof projectPriorityLabel] ?? priority;

  const emphasised = priority === 'High' || priority === 'Critical';

  return (
    <View
      style={[
        styles.badge,
        styles.priorityBadge,
        emphasised && { borderColor: theme.colors.status.risk.border, backgroundColor: theme.colors.status.risk.bg },
      ]}
    >
      <Txt
        variant="caption"
        color={emphasised ? theme.colors.status.risk.text : theme.colors.text.secondary}
        style={styles.badgeLabel}
      >
        {label}
      </Txt>
    </View>
  );
}

/**
 * Deadline health, stated in plain words.
 *
 * "faltam 5 dias" beats a date the reader has to subtract from today, and "3 dias em
 * atraso" beats a red dot they have to interpret.
 */
export function DeadlineBadge({
  daysToDeadline,
  isOverdue,
  isAtRisk,
}: {
  daysToDeadline: number | null;
  isOverdue: boolean;
  isAtRisk: boolean;
}) {
  if (daysToDeadline === null && !isOverdue && !isAtRisk) return null;

  const tone = isOverdue
    ? theme.colors.status.overdue
    : isAtRisk
      ? theme.colors.status.risk
      : theme.colors.status.neutral;

  return (
    <View style={[styles.badge, { backgroundColor: tone.bg, borderColor: tone.border }]}>
      <Txt variant="caption" color={tone.text} style={styles.badgeLabel}>
        {formatDeadline(daysToDeadline)}
      </Txt>
    </View>
  );
}

// ─── KPI ────────────────────────────────────────────────────────────────────

export function KpiTile({
  label,
  value,
  hint,
  tone,
  style,
}: {
  label: string;
  value: string;
  hint?: string;
  tone?: 'default' | 'positive' | 'warning' | 'danger';
  style?: StyleProp<ViewStyle>;
}) {
  const color =
    tone === 'positive'
      ? theme.colors.success
      : tone === 'warning'
        ? theme.colors.warning
        : tone === 'danger'
          ? theme.colors.danger
          : theme.colors.text.primary;

  return (
    <View style={[styles.kpi, style]}>
      <Txt variant="overline" color={theme.colors.text.muted} numberOfLines={1}>
        {label}
      </Txt>
      <Txt variant="kpi" color={color} style={styles.kpiValue} numberOfLines={1}>
        {value}
      </Txt>
      {hint ? (
        <Txt variant="caption" color={theme.colors.text.muted} numberOfLines={1}>
          {hint}
        </Txt>
      ) : null}
    </View>
  );
}

export function KpiGrid({ children }: { children: React.ReactNode }) {
  return <View style={styles.kpiGrid}>{children}</View>;
}

// ─── FlowScore ──────────────────────────────────────────────────────────────

/**
 * The FlowScore, shown with its components.
 *
 * The number alone would be exactly the "magic number" the product is meant to avoid.
 * Showing every dimension is what lets a manager answer why A ranks above B.
 */
export function FlowScoreCard({ score }: { score: FlowScore }) {
  const dimensions = [
    { key: 'strategicAlignment', value: score.strategicAlignment, weight: '35%' },
    { key: 'impact', value: score.impact, weight: '25%' },
    { key: 'feasibility', value: score.feasibility, weight: '25%' },
    { key: 'urgency', value: score.urgency, weight: '15%' },
  ] as const;

  return (
    <Card>
      <View style={styles.flowScoreHeader}>
        <View>
          <Txt variant="overline" color={theme.colors.text.muted}>
            FlowScore
          </Txt>
          <Txt variant="caption" color={theme.colors.text.secondary}>
            Priorização calculada
          </Txt>
        </View>
        <Txt variant="display" color={theme.colors.text.brand}>
          {score.total}
        </Txt>
      </View>

      <View style={styles.flowScoreDimensions}>
        {dimensions.map((dimension) => (
          <View key={dimension.key} style={styles.flowScoreRow}>
            <View style={styles.flowScoreRowHeader}>
              <Txt variant="caption" color={theme.colors.text.secondary}>
                {flowScoreDimensionLabel[dimension.key]}
              </Txt>
              <Txt variant="caption" color={theme.colors.text.muted}>
                {`${dimension.value}/10 · peso ${dimension.weight}`}
              </Txt>
            </View>
            <ProgressBar value={dimension.value * 10} height={6} />
          </View>
        ))}
      </View>

      <View style={styles.flowScoreFooter}>
        <Txt variant="caption" color={theme.colors.text.secondary}>
          {`Confiança ${score.confidence}/10 — multiplica o total entre 0,6 e 1,0`}
        </Txt>
        <Txt variant="caption" color={theme.colors.text.muted}>
          {`Fórmula v${score.formulaVersion}`}
        </Txt>
      </View>
    </Card>
  );
}

/** Compact score pill for list rows. */
export function ScorePill({ label, value }: { label: string; value: number | null }) {
  if (value === null) return null;

  return (
    <View style={styles.scorePill}>
      <Txt variant="caption" color={theme.colors.text.muted}>
        {label}
      </Txt>
      <Txt variant="label" color={theme.colors.text.brand}>
        {value}
      </Txt>
    </View>
  );
}

// ─── Cards ──────────────────────────────────────────────────────────────────

export function IdeaCard({
  idea,
  onPress,
  selected,
}: {
  idea: IdeaSummary;
  onPress: () => void;
  selected?: boolean;
}) {
  const tone = statusTone(idea.status);

  return (
    <Card
      onPress={onPress}
      accent={selected ? theme.colors.brand : tone.text}
      accessibilityLabel={`Ideia ${idea.title}`}
      style={selected ? styles.selectedCard : undefined}
    >
      <View style={styles.cardHeader}>
        <IdeaStatusBadge status={idea.status} />
        <PriorityBadge priority={idea.priority} kind="idea" />
      </View>

      <Txt variant="title" numberOfLines={2} style={styles.cardTitle}>
        {idea.title}
      </Txt>

      <Txt variant="body" color={theme.colors.text.secondary} numberOfLines={2}>
        {idea.problem}
      </Txt>

      <View style={styles.cardFooter}>
        <Txt variant="caption" color={theme.colors.text.muted} numberOfLines={1} style={styles.cardMeta}>
          {`${idea.submittedByName} · ${formatRelative(idea.createdAt)}`}
        </Txt>

        <View style={styles.scoreRow}>
          <ScorePill label="Flow" value={idea.flowScore} />
          <ScorePill label="Nota" value={idea.score} />
        </View>
      </View>
    </Card>
  );
}

export function ProjectCard({
  project,
  onPress,
}: {
  project: ProjectSummary;
  onPress: () => void;
}) {
  const tone = statusTone(project.status);

  const daysToDeadline = project.deadline
    ? Math.floor((new Date(project.deadline).getTime() - Date.now()) / 86_400_000)
    : null;

  return (
    <Card onPress={onPress} accent={tone.text} accessibilityLabel={`Projeto ${project.title}`}>
      <View style={styles.cardHeader}>
        <ProjectStatusBadge status={project.status} />
        <StatusBadge status="neutral" label={projectStageLabel[project.stage]} />
      </View>

      <Txt variant="title" numberOfLines={2} style={styles.cardTitle}>
        {project.title}
      </Txt>

      {project.blockedReason ? (
        <View style={styles.blockedNote}>
          <Txt variant="caption" color={theme.colors.status.blocked.text} numberOfLines={2}>
            {project.blockedReason}
          </Txt>
        </View>
      ) : null}

      <View style={styles.progressWrapper}>
        <ProgressBar
          value={project.progressPercentage}
          color={project.status === 'Blocked' ? theme.colors.status.blocked.text : undefined}
        />
      </View>

      <View style={styles.cardFooter}>
        <Txt variant="caption" color={theme.colors.text.muted} numberOfLines={1} style={styles.cardMeta}>
          {`${project.ownerName} · ${project.progressPercentage}%`}
        </Txt>

        <DeadlineBadge
          daysToDeadline={daysToDeadline}
          isOverdue={project.isOverdue}
          isAtRisk={project.isAtRisk}
        />
      </View>
    </Card>
  );
}

/** Financial summary line used on results and dashboard cards. */
export function MoneyRow({
  label,
  value,
  tone,
}: {
  label: string;
  value: number | null;
  tone?: 'positive' | 'negative' | 'muted';
}) {
  const color =
    tone === 'positive'
      ? theme.colors.success
      : tone === 'negative'
        ? theme.colors.danger
        : theme.colors.text.primary;

  return (
    <View style={styles.moneyRow}>
      <Txt variant="body" color={theme.colors.text.secondary}>
        {label}
      </Txt>
      <Txt variant="bodyStrong" color={color}>
        {formatCompactCurrency(value)}
      </Txt>
    </View>
  );
}

export function MetricRow({
  label,
  value,
  hint,
}: {
  label: string;
  value: string;
  hint?: string;
}) {
  return (
    <View style={styles.moneyRow}>
      <View style={styles.flex}>
        <Txt variant="body" color={theme.colors.text.secondary}>
          {label}
        </Txt>
        {hint ? (
          <Txt variant="caption" color={theme.colors.text.muted}>
            {hint}
          </Txt>
        ) : null}
      </View>
      <Txt variant="bodyStrong">{value}</Txt>
    </View>
  );
}

/** Horizontal distribution bar, for status and stage breakdowns. */
export function DistributionBar({
  slices,
  labelFor,
}: {
  slices: { label: string; count: number; percentage: number }[];
  labelFor?: (label: string) => string;
}) {
  const total = slices.reduce((sum, slice) => sum + slice.count, 0);
  if (total === 0) return null;

  return (
    <View>
      <View style={styles.distributionTrack}>
        {slices.map((slice, index) => (
          <View
            key={slice.label}
            style={{
              flex: slice.count,
              backgroundColor: statusTone(slice.label).text ?? theme.colors.chart[index % theme.colors.chart.length],
            }}
          />
        ))}
      </View>

      <View style={styles.distributionLegend}>
        {slices.map((slice) => (
          <View key={slice.label} style={styles.legendItem}>
            <View style={[styles.legendDot, { backgroundColor: statusTone(slice.label).text }]} />
            <Txt variant="caption" color={theme.colors.text.secondary}>
              {`${labelFor ? labelFor(slice.label) : slice.label} ${slice.count}`}
            </Txt>
          </View>
        ))}
      </View>
    </View>
  );
}

export function PercentRow({ label, value }: { label: string; value: number }) {
  return (
    <View style={styles.percentRow}>
      <View style={styles.percentHeader}>
        <Txt variant="caption" color={theme.colors.text.secondary} numberOfLines={1} style={styles.flex}>
          {label}
        </Txt>
        <Txt variant="label">{formatPercent(value)}</Txt>
      </View>
      <ProgressBar value={value} height={6} />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },

  badge: {
    paddingHorizontal: theme.spacing.sm,
    paddingVertical: 3,
    borderRadius: theme.radius.full,
    borderWidth: 1,
    alignSelf: 'flex-start',
  },
  priorityBadge: {
    backgroundColor: theme.colors.surface.sunken,
    borderColor: theme.colors.surface.border,
  },
  badgeLabel: { fontWeight: '600' },

  kpi: {
    backgroundColor: theme.colors.surface.card,
    borderRadius: theme.radius.lg,
    borderWidth: 1,
    borderColor: theme.colors.surface.border,
    padding: theme.spacing.lg,
    flexGrow: 1,
    flexBasis: '46%',
    minWidth: 140,
  },
  kpiValue: { marginTop: theme.spacing.xs, marginBottom: theme.spacing.xxs },
  kpiGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.md },

  flowScoreHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  flowScoreDimensions: { marginTop: theme.spacing.lg, gap: theme.spacing.md },
  flowScoreRow: { gap: theme.spacing.xs },
  flowScoreRowHeader: { flexDirection: 'row', justifyContent: 'space-between' },
  flowScoreFooter: {
    marginTop: theme.spacing.lg,
    paddingTop: theme.spacing.md,
    borderTopWidth: 1,
    borderTopColor: theme.colors.surface.border,
    gap: theme.spacing.xxs,
  },

  scorePill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: theme.spacing.xs,
    backgroundColor: theme.colors.brandSurface,
    paddingHorizontal: theme.spacing.sm,
    paddingVertical: 3,
    borderRadius: theme.radius.full,
  },
  scoreRow: { flexDirection: 'row', gap: theme.spacing.xs },

  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: theme.spacing.sm,
    marginBottom: theme.spacing.sm,
  },
  cardTitle: { marginBottom: theme.spacing.xs },
  cardFooter: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: theme.spacing.md,
    gap: theme.spacing.sm,
  },
  cardMeta: { flex: 1 },
  selectedCard: { borderColor: theme.colors.brand, borderWidth: 1.5 },

  blockedNote: {
    backgroundColor: theme.colors.status.blocked.bg,
    borderRadius: theme.radius.sm,
    padding: theme.spacing.sm,
    marginTop: theme.spacing.xs,
  },
  progressWrapper: { marginTop: theme.spacing.md },

  moneyRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: theme.spacing.sm,
    gap: theme.spacing.md,
  },

  distributionTrack: {
    flexDirection: 'row',
    height: 10,
    borderRadius: theme.radius.full,
    overflow: 'hidden',
    backgroundColor: theme.colors.surface.sunken,
  },
  distributionLegend: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: theme.spacing.md,
    marginTop: theme.spacing.md,
  },
  legendItem: { flexDirection: 'row', alignItems: 'center', gap: theme.spacing.xs },
  legendDot: { width: 8, height: 8, borderRadius: 4 },

  percentRow: { gap: theme.spacing.xs, marginBottom: theme.spacing.md },
  percentHeader: { flexDirection: 'row', alignItems: 'center', gap: theme.spacing.sm },
});

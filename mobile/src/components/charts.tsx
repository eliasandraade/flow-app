import React, { useMemo } from 'react';
import { StyleSheet, View } from 'react-native';
import { Bar, CartesianChart, Line } from 'victory-native';
import { theme } from '../theme';
import { Txt } from './primitives';
import { formatPeriod } from '../utils/format';

/**
 * Charts for the executive dashboard.
 *
 * Built on Victory Native (Skia + Reanimated), verified against this exact stack —
 * Expo 54, RN 0.81.5, React 19, Skia 2.2.12, Reanimated 4.1.1 — with expo-doctor at
 * 18/18. Reanimated 4 needs its react-native-worklets peer installed explicitly; without
 * it the app builds and then crashes outside Expo Go.
 *
 * Every chart handles the empty dataset itself, because a dashboard on a fresh
 * installation is the normal first experience, not an edge case.
 */

const CHART_HEIGHT = 200;

function EmptyChart({ message }: { message: string }) {
  return (
    <View style={styles.empty}>
      <Txt variant="caption" color={theme.colors.text.muted} align="center">
        {message}
      </Txt>
    </View>
  );
}

function Legend({ items }: { items: { label: string; color: string }[] }) {
  return (
    <View style={styles.legend}>
      {items.map((item) => (
        <View key={item.label} style={styles.legendItem}>
          <View style={[styles.legendDot, { backgroundColor: item.color }]} />
          <Txt variant="caption" color={theme.colors.text.secondary}>
            {item.label}
          </Txt>
        </View>
      ))}
    </View>
  );
}

// ─── Trend ──────────────────────────────────────────────────────────────────

export interface TrendDatum {
  period: string;
  ideas: number;
  projects: number;
  completed: number;
}

/**
 * Ideas, projects and completions over time — the shape of the innovation funnel as it
 * moves, which a table of totals cannot show.
 */
export function TrendChart({ data }: { data: TrendDatum[] }) {
  const points = useMemo(
    () => data.map((point) => ({ ...point, label: formatPeriod(point.period) })),
    [data]
  );

  if (points.length < 2) {
    return <EmptyChart message="A tendência aparece a partir de dois meses de histórico." />;
  }

  return (
    <View>
      <View style={{ height: CHART_HEIGHT }}>
        <CartesianChart
          data={points}
          xKey="label"
          yKeys={['ideas', 'projects', 'completed']}
          padding={{ left: 4, right: 4, top: 8, bottom: 4 }}
          axisOptions={{
            lineColor: theme.colors.surface.border,
            labelColor: theme.colors.text.muted,
            font: null,
          }}
        >
          {({ points: series }) => (
            <>
              <Line
                points={series.ideas}
                color={theme.colors.chart[0]}
                strokeWidth={2.5}
                curveType="natural"
                animate={{ type: 'timing', duration: 400 }}
              />
              <Line
                points={series.projects}
                color={theme.colors.chart[1]}
                strokeWidth={2.5}
                curveType="natural"
                animate={{ type: 'timing', duration: 400 }}
              />
              <Line
                points={series.completed}
                color={theme.colors.chart[2]}
                strokeWidth={2.5}
                curveType="natural"
                animate={{ type: 'timing', duration: 400 }}
              />
            </>
          )}
        </CartesianChart>
      </View>

      <View style={styles.axisRow}>
        {points.map((point) => (
          <Txt key={point.period} variant="caption" color={theme.colors.text.muted}>
            {point.label}
          </Txt>
        ))}
      </View>

      <Legend
        items={[
          { label: 'Ideias', color: theme.colors.chart[0] },
          { label: 'Projetos', color: theme.colors.chart[1] },
          { label: 'Concluídos', color: theme.colors.chart[2] },
        ]}
      />
    </View>
  );
}

// ─── Category bars ──────────────────────────────────────────────────────────

export interface CategoryDatum {
  label: string;
  value: number;
}

/**
 * Horizontal comparison across a handful of named categories — strategies, campaigns,
 * stages. Kept to the top entries so the labels stay readable on a phone.
 */
export function CategoryBarChart({
  data,
  emptyMessage = 'Ainda não há dados para comparar.',
  color = theme.colors.chart[0],
  maxItems = 6,
}: {
  data: CategoryDatum[];
  emptyMessage?: string;
  color?: string;
  maxItems?: number;
}) {
  const points = useMemo(
    () =>
      data
        .filter((point) => point.value > 0)
        .slice(0, maxItems)
        .map((point, index) => ({
          ...point,
          // A phone cannot render a long strategy title under a bar.
          short: point.label.length > 12 ? `${point.label.slice(0, 11)}…` : point.label,
          index,
        })),
    [data, maxItems]
  );

  if (points.length === 0) return <EmptyChart message={emptyMessage} />;

  return (
    <View>
      <View style={{ height: CHART_HEIGHT }}>
        <CartesianChart
          data={points}
          xKey="short"
          yKeys={['value']}
          domainPadding={{ left: 24, right: 24 }}
          padding={{ left: 4, right: 4, top: 8, bottom: 4 }}
          axisOptions={{
            lineColor: theme.colors.surface.border,
            labelColor: theme.colors.text.muted,
            font: null,
          }}
        >
          {({ points: series, chartBounds }) => (
            <Bar
              points={series.value}
              chartBounds={chartBounds}
              color={color}
              roundedCorners={{ topLeft: 6, topRight: 6 }}
              animate={{ type: 'timing', duration: 400 }}
            />
          )}
        </CartesianChart>
      </View>

      <View style={styles.barLegend}>
        {points.map((point) => (
          <View key={point.label} style={styles.barLegendRow}>
            <Txt variant="caption" color={theme.colors.text.secondary} numberOfLines={1} style={styles.flex}>
              {point.label}
            </Txt>
            <Txt variant="label">{point.value}</Txt>
          </View>
        ))}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  empty: {
    height: 120,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: theme.colors.surface.sunken,
    borderRadius: theme.radius.md,
    paddingHorizontal: theme.spacing.lg,
  },
  legend: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: theme.spacing.lg,
    marginTop: theme.spacing.md,
  },
  legendItem: { flexDirection: 'row', alignItems: 'center', gap: theme.spacing.xs },
  legendDot: { width: 10, height: 10, borderRadius: 5 },
  axisRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: theme.spacing.xs,
  },
  barLegend: { marginTop: theme.spacing.md, gap: theme.spacing.xs },
  barLegendRow: { flexDirection: 'row', alignItems: 'center', gap: theme.spacing.sm },
});

import React from 'react';
import { StyleSheet, View } from 'react-native';
import { useRoute, type RouteProp } from '@react-navigation/native';
import { useGuidelineHistory } from '../../api/queries';
import { Card, Screen, Txt } from '../../components/primitives';
import { EmptyState, ErrorState, SkeletonList } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { StatusBadge } from '../../components/domain';
import { guidelineCategoryLabel } from '../../i18n/labels';
import { formatDate, formatDateTime } from '../../utils/format';
import { theme } from '../../theme';
import type { StrategyStackParams } from '../../navigation/types';

const CHANGE_LABEL: Record<string, string> = {
  Created: 'Criada',
  Updated: 'Atualizada',
  Closed: 'Encerrada',
};

/**
 * Append-only history of a strategic guideline. Each entry is the state at the moment of
 * the change, so a past decision can be read exactly as it stood then.
 */
export function StrategyHistoryScreen() {
  const { guidelineId } = useRoute<RouteProp<StrategyStackParams, 'StrategyHistory'>>().params;

  const query = useGuidelineHistory(guidelineId);
  const refreshControl = useRefreshControl(query);

  if (query.isPending) {
    return (
      <Screen>
        <SkeletonList count={3} />
      </Screen>
    );
  }

  if (query.isError) {
    return (
      <Screen>
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      </Screen>
    );
  }

  if (query.data.length === 0) {
    return (
      <Screen>
        <EmptyState
          icon="≡"
          title="Sem histórico"
          message="As alterações desta diretriz aparecerão aqui."
        />
      </Screen>
    );
  }

  return (
    <Screen scroll refreshControl={refreshControl}>
      {query.data.map((entry, index) => (
        <View key={entry.id} style={styles.timelineRow}>
          <View style={styles.timelineRail}>
            <View style={styles.timelineDot} />
            {index < query.data.length - 1 ? <View style={styles.timelineLine} /> : null}
          </View>

          <Card style={styles.entryCard}>
            <View style={styles.entryHeader}>
              <StatusBadge
                status={entry.changeType === 'Closed' ? 'cancelled' : 'approved'}
                label={CHANGE_LABEL[entry.changeType] ?? entry.changeType}
              />
              <Txt variant="caption" color={theme.colors.text.muted}>
                {formatDateTime(entry.changedAt)}
              </Txt>
            </View>

            <Txt variant="title" numberOfLines={2} style={styles.entryTitle}>
              {entry.title}
            </Txt>

            <Txt variant="caption" color={theme.colors.text.secondary} numberOfLines={3}>
              {entry.description}
            </Txt>

            <View style={styles.entryMeta}>
              <Txt variant="caption" color={theme.colors.text.muted}>
                {guidelineCategoryLabel[entry.category]}
                {entry.campaign ? ` · ${entry.campaign}` : ''}
              </Txt>
              <Txt variant="caption" color={theme.colors.text.muted}>
                {entry.validUntil
                  ? `${formatDate(entry.validFrom)} → ${formatDate(entry.validUntil)}`
                  : `Desde ${formatDate(entry.validFrom)}`}
              </Txt>
              <Txt variant="caption" color={theme.colors.text.muted}>
                {`por ${entry.changedByName}`}
              </Txt>
            </View>
          </Card>
        </View>
      ))}
    </Screen>
  );
}

const styles = StyleSheet.create({
  timelineRow: { flexDirection: 'row', gap: theme.spacing.md, marginTop: theme.spacing.lg },
  timelineRail: { width: 12, alignItems: 'center' },
  timelineDot: {
    width: 12,
    height: 12,
    borderRadius: 6,
    backgroundColor: theme.colors.brand,
    marginTop: theme.spacing.lg,
  },
  timelineLine: { flex: 1, width: 2, backgroundColor: theme.colors.surface.border, marginTop: 4 },
  entryCard: { flex: 1 },
  entryHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: theme.spacing.sm,
  },
  entryTitle: { marginTop: theme.spacing.sm, marginBottom: theme.spacing.xs },
  entryMeta: { marginTop: theme.spacing.md, gap: theme.spacing.xxs },
});

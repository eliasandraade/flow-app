import React, { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useRoute, type RouteProp } from '@react-navigation/native';
import { useProjectSnapshots, useProjectTimeline } from '../../api/queries';
import { Card, Chip, ChipRow, Screen, Txt } from '../../components/primitives';
import { EmptyState, ErrorState, SkeletonList } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { ProjectStatusBadge, StatusBadge } from '../../components/domain';
import { auditActionLabel, projectStageLabel, projectStatusLabel } from '../../i18n/labels';
import { formatDateTime, formatPercent } from '../../utils/format';
import { theme, statusTone } from '../../theme';

type Tab = 'timeline' | 'snapshots';

/**
 * The governance view of a project.
 *
 * The timeline is the append-only audit trail — who changed what, when and why. The
 * snapshots are the complete, immutable state captured at each transition, which is what
 * makes it possible to answer "how did this project look in July" rather than merely "what
 * changed".
 */
export function ProjectTimelineScreen() {
  const { projectId } = useRoute<RouteProp<{ p: { projectId: string } }, 'p'>>().params;

  const [tab, setTab] = useState<Tab>('timeline');
  const timeline = useProjectTimeline(projectId);
  const snapshots = useProjectSnapshots(projectId, { enabled: tab === 'snapshots' });

  const active = tab === 'timeline' ? timeline : snapshots;
  const refreshControl = useRefreshControl(active);

  return (
    <Screen scroll refreshControl={refreshControl}>
      <ChipRow>
        <Chip label="Auditoria" selected={tab === 'timeline'} onPress={() => setTab('timeline')} />
        <Chip label="Snapshots" selected={tab === 'snapshots'} onPress={() => setTab('snapshots')} />
      </ChipRow>

      {active.isPending ? (
        <SkeletonList count={4} />
      ) : active.isError ? (
        <ErrorState error={active.error} onRetry={() => void active.refetch()} />
      ) : tab === 'timeline' ? (
        timeline.data && timeline.data.length > 0 ? (
          <View style={styles.list}>
            {timeline.data.map((entry, index) => (
              <View key={`${entry.timestamp}-${index}`} style={styles.timelineRow}>
                <View style={styles.rail}>
                  <View
                    style={[
                      styles.dot,
                      { backgroundColor: statusTone(entry.newValue ?? 'neutral').text },
                    ]}
                  />
                  {index < timeline.data.length - 1 ? <View style={styles.line} /> : null}
                </View>

                <Card style={styles.entry}>
                  <View style={styles.entryHeader}>
                    <Txt variant="label">
                      {auditActionLabel[entry.action] ?? entry.action}
                    </Txt>
                    <Txt variant="caption" color={theme.colors.text.muted}>
                      {formatDateTime(entry.timestamp)}
                    </Txt>
                  </View>

                  {entry.oldValue && entry.newValue ? (
                    <Txt variant="caption" color={theme.colors.text.secondary} style={styles.transition}>
                      {`${translate(entry.oldValue)} → ${translate(entry.newValue)}`}
                    </Txt>
                  ) : null}

                  {entry.reason ? (
                    <View style={styles.reason}>
                      <Txt variant="caption" color={theme.colors.text.secondary}>
                        {entry.reason}
                      </Txt>
                    </View>
                  ) : null}

                  <Txt variant="caption" color={theme.colors.text.muted} style={styles.actor}>
                    {entry.actorName || 'Sistema'}
                  </Txt>
                </Card>
              </View>
            ))}
          </View>
        ) : (
          <EmptyState icon="≡" title="Sem registros" message="A auditoria deste projeto está vazia." />
        )
      ) : snapshots.data && snapshots.data.length > 0 ? (
        <View style={styles.list}>
          {snapshots.data.map((snapshot) => (
            <Card key={snapshot.id}>
              <View style={styles.entryHeader}>
                <Txt variant="label">
                  {auditActionLabel[snapshot.triggerAction] ?? snapshot.triggerAction}
                </Txt>
                <Txt variant="caption" color={theme.colors.text.muted}>
                  {formatDateTime(snapshot.takenAt)}
                </Txt>
              </View>

              <View style={styles.snapshotBadges}>
                <ProjectStatusBadge status={snapshot.status} />
                <StatusBadge status="neutral" label={projectStageLabel[snapshot.stage]} />
                <StatusBadge
                  status="neutral"
                  label={formatPercent(snapshot.progressPercentage)}
                />
              </View>

              <Txt variant="body" numberOfLines={2} style={styles.snapshotTitle}>
                {snapshot.title}
              </Txt>

              {snapshot.blockedReason ? (
                <Txt variant="caption" color={theme.colors.status.blocked.text} style={styles.transition}>
                  {snapshot.blockedReason}
                </Txt>
              ) : null}

              <Txt variant="caption" color={theme.colors.text.muted} style={styles.actor}>
                {`${snapshot.ownerName} · esquema v${snapshot.schemaVersion}`}
              </Txt>
            </Card>
          ))}
        </View>
      ) : (
        <EmptyState
          icon="◫"
          title="Sem snapshots"
          message="Cada transição de projeto captura um snapshot completo e imutável."
        />
      )}

      <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.note}>
        Auditoria e snapshots são somente-acréscimo: nada aqui pode ser alterado ou removido.
      </Txt>
    </Screen>
  );
}

/** Audit values are stored as domain identifiers; the reader gets Portuguese. */
function translate(value: string): string {
  return (
    projectStatusLabel[value as keyof typeof projectStatusLabel] ??
    projectStageLabel[value as keyof typeof projectStageLabel] ??
    value
  );
}

const styles = StyleSheet.create({
  list: { marginTop: theme.spacing.md, gap: theme.spacing.md },
  timelineRow: { flexDirection: 'row', gap: theme.spacing.md },
  rail: { width: 12, alignItems: 'center' },
  dot: { width: 12, height: 12, borderRadius: 6, marginTop: theme.spacing.lg },
  line: { flex: 1, width: 2, backgroundColor: theme.colors.surface.border, marginTop: 4 },
  entry: { flex: 1 },
  entryHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: theme.spacing.sm,
  },
  transition: { marginTop: theme.spacing.xs },
  reason: {
    marginTop: theme.spacing.sm,
    padding: theme.spacing.sm,
    backgroundColor: theme.colors.surface.sunken,
    borderRadius: theme.radius.sm,
  },
  actor: { marginTop: theme.spacing.sm },
  snapshotBadges: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: theme.spacing.xs,
    marginTop: theme.spacing.sm,
  },
  snapshotTitle: { marginTop: theme.spacing.sm },
  note: { marginTop: theme.spacing.xl },
});

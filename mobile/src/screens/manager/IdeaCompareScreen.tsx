import React, { useEffect } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useCompareIdeas } from '../../api/queries';
import { Button, Card, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ErrorState, SkeletonList } from '../../components/feedback';
import { IdeaStatusBadge, PriorityBadge } from '../../components/domain';
import { flowScoreDimensionLabel } from '../../i18n/labels';
import { formatDays, formatRelative } from '../../utils/format';
import { theme } from '../../theme';
import type { ManagerIdeasStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<ManagerIdeasStackParams>;

const COLUMN_WIDTH = 220;

/**
 * Side-by-side comparison built entirely from stored data.
 *
 * No model is involved here, so it works offline, costs nothing, and always agrees with
 * the numbers the queue shows. The copilot is a separate, optional layer on top.
 */
export function IdeaCompareScreen() {
  const navigation = useNavigation<Nav>();
  const { ideaIds } = useRoute<RouteProp<ManagerIdeasStackParams, 'IdeaCompare'>>().params;

  const compare = useCompareIdeas();

  useEffect(() => {
    compare.mutate(ideaIds);
    // Runs once for the ids this screen was opened with.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ideaIds.join(',')]);

  if (compare.isPending || compare.isIdle) {
    return (
      <Screen>
        <SkeletonList count={3} />
      </Screen>
    );
  }

  if (compare.isError) {
    return (
      <Screen>
        <ErrorState error={compare.error} onRetry={() => compare.mutate(ideaIds)} />
      </Screen>
    );
  }

  const comparison = compare.data;

  return (
    <Screen scroll>
      <SectionHeader
        title="Comparação"
        subtitle={`${comparison.ideas.length} ideias, ordenadas pelo FlowScore`}
      />

      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.columns}>
        {comparison.ideas.map((idea) => {
          const isTopFlow = comparison.highestFlowScoreId === idea.id;
          const isTopScore = comparison.highestScoreId === idea.id;

          return (
            <Card
              key={idea.id}
              style={[styles.column, isTopFlow && styles.columnHighlighted]}
              onPress={() => navigation.navigate('ManagerIdeaDetail', { ideaId: idea.id })}
            >
              {isTopFlow ? (
                <View style={styles.topBadge}>
                  <Txt variant="caption" color={theme.colors.text.inverse}>
                    Maior FlowScore
                  </Txt>
                </View>
              ) : null}

              <View style={styles.columnBadges}>
                <IdeaStatusBadge status={idea.status} />
                <PriorityBadge priority={idea.priority} kind="idea" />
              </View>

              <Txt variant="title" numberOfLines={3} style={styles.columnTitle}>
                {idea.title}
              </Txt>

              <View style={styles.scoreBlock}>
                <View style={styles.scoreCell}>
                  <Txt variant="overline" color={theme.colors.text.muted}>
                    FlowScore
                  </Txt>
                  <Txt variant="kpiSmall" color={theme.colors.text.brand}>
                    {idea.flowScore ?? '—'}
                  </Txt>
                </View>

                <View style={styles.scoreCell}>
                  <Txt variant="overline" color={theme.colors.text.muted}>
                    Nota
                  </Txt>
                  <Txt
                    variant="kpiSmall"
                    color={isTopScore ? theme.colors.success : theme.colors.text.primary}
                  >
                    {idea.score ?? '—'}
                  </Txt>
                </View>
              </View>

              {idea.flowScoreBreakdown ? (
                <View style={styles.breakdown}>
                  {(
                    ['strategicAlignment', 'impact', 'feasibility', 'urgency', 'confidence'] as const
                  ).map((dimension) => (
                    <View key={dimension} style={styles.breakdownRow}>
                      <Txt variant="caption" color={theme.colors.text.secondary} numberOfLines={1} style={styles.flex}>
                        {flowScoreDimensionLabel[dimension]}
                      </Txt>
                      <Txt variant="caption" color={theme.colors.text.primary}>
                        {idea.flowScoreBreakdown![dimension]}
                      </Txt>
                    </View>
                  ))}
                </View>
              ) : (
                <Txt variant="caption" color={theme.colors.text.muted} style={styles.breakdown}>
                  Ainda não avaliada.
                </Txt>
              )}

              <View style={styles.meta}>
                <Txt variant="caption" color={theme.colors.text.muted} numberOfLines={2}>
                  {idea.linkedGuidelineTitle
                    ? `${idea.linkedGuidelineTitle}${idea.guidelineIsCurrent ? ' (vigente)' : ' (encerrada)'}`
                    : 'Sem diretriz vinculada'}
                </Txt>

                <Txt variant="caption" color={theme.colors.text.muted}>
                  {`${idea.commentCount} comentário(s)`}
                </Txt>

                {idea.daysUnderReview > 0 ? (
                  <Txt variant="caption" color={theme.colors.warning}>
                    {`Em análise há ${formatDays(idea.daysUnderReview)}`}
                  </Txt>
                ) : (
                  <Txt variant="caption" color={theme.colors.text.muted}>
                    {formatRelative(idea.createdAt)}
                  </Txt>
                )}
              </View>
            </Card>
          );
        })}
      </ScrollView>

      <SectionHeader title="Quer uma leitura mais profunda?" />

      <Card>
        <Txt variant="body" color={theme.colors.text.secondary}>
          O Copiloto analisa riscos, trade-offs e alinhamento estratégico destas mesmas ideias,
          citando os dados que usou. A recomendação é sua; ele apenas organiza o raciocínio.
        </Txt>

        <Button
          title="Analisar com o Copiloto"
          onPress={() => navigation.navigate('Copilot', { ideaIds })}
          variant="secondary"
          style={styles.copilotCta}
        />
      </Card>
    </Screen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  columns: { gap: theme.spacing.md, paddingVertical: theme.spacing.xs },
  column: { width: COLUMN_WIDTH },
  columnHighlighted: { borderColor: theme.colors.brand, borderWidth: 1.5 },
  topBadge: {
    alignSelf: 'flex-start',
    backgroundColor: theme.colors.brand,
    borderRadius: theme.radius.full,
    paddingHorizontal: theme.spacing.sm,
    paddingVertical: 3,
    marginBottom: theme.spacing.sm,
  },
  columnBadges: { flexDirection: 'row', gap: theme.spacing.xs, marginBottom: theme.spacing.sm },
  columnTitle: { minHeight: 66 },
  scoreBlock: {
    flexDirection: 'row',
    gap: theme.spacing.lg,
    marginTop: theme.spacing.md,
    paddingTop: theme.spacing.md,
    borderTopWidth: 1,
    borderTopColor: theme.colors.surface.border,
  },
  scoreCell: { flex: 1 },
  breakdown: {
    marginTop: theme.spacing.md,
    paddingTop: theme.spacing.md,
    borderTopWidth: 1,
    borderTopColor: theme.colors.surface.border,
    gap: theme.spacing.xxs,
  },
  breakdownRow: { flexDirection: 'row', alignItems: 'center', gap: theme.spacing.sm },
  meta: {
    marginTop: theme.spacing.md,
    paddingTop: theme.spacing.md,
    borderTopWidth: 1,
    borderTopColor: theme.colors.surface.border,
    gap: theme.spacing.xxs,
  },
  copilotCta: { marginTop: theme.spacing.lg },
});

import React, { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useCloseGuideline, useDeleteGuideline, useGuideline, useStrategyDashboard } from '../../api/queries';
import { useAuthStore } from '../../store/authStore';
import { Button, Card, Divider, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ConfirmDialog, ErrorBanner, ErrorState, SkeletonList } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { DistributionBar, KpiGrid, KpiTile, StatusBadge } from '../../components/domain';
import { guidelineCategoryLabel, ideaStatusLabel, projectStatusLabel } from '../../i18n/labels';
import { formatCompactCurrency, formatDate } from '../../utils/format';
import { theme } from '../../theme';
import type { StrategyStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<StrategyStackParams>;

export function StrategyDetailScreen() {
  const navigation = useNavigation<Nav>();
  const { guidelineId } = useRoute<RouteProp<StrategyStackParams, 'StrategyDetail'>>().params;

  const role = useAuthStore((state) => state.session?.role);
  const canManage = role === 'Leadership';
  const canSeeMetrics = role === 'Leadership' || role === 'Manager';

  const query = useGuideline(guidelineId);
  const metrics = useStrategyDashboard(guidelineId, { enabled: canSeeMetrics });
  const closeGuideline = useCloseGuideline();
  const deleteGuideline = useDeleteGuideline();
  const refreshControl = useRefreshControl(query);

  const [confirming, setConfirming] = useState<'close' | 'delete' | null>(null);
  const [error, setError] = useState<string | null>(null);

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

  const guideline = query.data;

  async function confirmAction() {
    setError(null);

    try {
      if (confirming === 'close') {
        await closeGuideline.mutateAsync(guidelineId);
        setConfirming(null);
      } else if (confirming === 'delete') {
        await deleteGuideline.mutateAsync(guidelineId);
        setConfirming(null);
        navigation.goBack();
      }
    } catch (caught) {
      // A guideline with linked ideas or projects cannot be deleted; the server explains
      // why and suggests closing instead, so that message is worth showing verbatim.
      setError(caught instanceof Error ? caught.message : 'Não foi possível concluir a ação.');
      setConfirming(null);
    }
  }

  return (
    <Screen scroll refreshControl={refreshControl}>
      {error ? <ErrorBanner message={error} onDismiss={() => setError(null)} /> : null}

      <Card style={styles.first}>
        <View style={styles.badges}>
          <StatusBadge
            status={guideline.isCurrent ? 'approved' : 'cancelled'}
            label={guideline.isCurrent ? 'Vigente' : 'Encerrada'}
          />
          <StatusBadge status="strategy" label={guidelineCategoryLabel[guideline.category]} />
        </View>

        <Txt variant="heading" style={styles.title}>
          {guideline.title}
        </Txt>

        <Txt variant="body" color={theme.colors.text.secondary}>
          {guideline.description}
        </Txt>

        <Divider />

        <View style={styles.metaRow}>
          <Txt variant="caption" color={theme.colors.text.muted}>
            Início
          </Txt>
          <Txt variant="label">{formatDate(guideline.validFrom)}</Txt>
        </View>

        <View style={styles.metaRow}>
          <Txt variant="caption" color={theme.colors.text.muted}>
            Encerramento
          </Txt>
          <Txt variant="label">
            {guideline.validUntil ? formatDate(guideline.validUntil) : 'Sem data definida'}
          </Txt>
        </View>

        {guideline.campaign ? (
          <View style={styles.metaRow}>
            <Txt variant="caption" color={theme.colors.text.muted}>
              Campanha
            </Txt>
            <Txt variant="label" color={theme.colors.text.brand}>
              {guideline.campaign}
            </Txt>
          </View>
        ) : null}
      </Card>

      {canSeeMetrics && metrics.data ? (
        <>
          <SectionHeader title="Resultado desta diretriz" />

          <KpiGrid>
            <KpiTile label="Ideias" value={String(metrics.data.ideaCount)} />
            <KpiTile label="Projetos" value={String(metrics.data.projectCount)} />
            <KpiTile
              label="Valor estimado"
              value={formatCompactCurrency(metrics.data.estimatedNetValue)}
            />
            <KpiTile
              label="Valor realizado"
              value={formatCompactCurrency(metrics.data.actualNetValue)}
              tone={metrics.data.actualNetValue > 0 ? 'positive' : 'default'}
            />
          </KpiGrid>

          {metrics.data.ideasByStatus.length > 0 ? (
            <Card style={styles.spaced}>
              <Txt variant="label" style={styles.blockTitle}>
                Ideias por situação
              </Txt>
              <DistributionBar
                slices={metrics.data.ideasByStatus}
                labelFor={(label) => ideaStatusLabel[label as keyof typeof ideaStatusLabel] ?? label}
              />
            </Card>
          ) : null}

          {metrics.data.projectsByStatus.length > 0 ? (
            <Card style={styles.spaced}>
              <Txt variant="label" style={styles.blockTitle}>
                Projetos por situação
              </Txt>
              <DistributionBar
                slices={metrics.data.projectsByStatus}
                labelFor={(label) =>
                  projectStatusLabel[label as keyof typeof projectStatusLabel] ?? label
                }
              />
            </Card>
          ) : null}
        </>
      ) : null}

      <SectionHeader title="Governança" />

      <Button
        title="Ver histórico de alterações"
        onPress={() => navigation.navigate('StrategyHistory', { guidelineId })}
        variant="secondary"
      />

      {canManage ? (
        <View style={styles.actions}>
          <Button
            title="Editar diretriz"
            onPress={() => navigation.navigate('StrategyForm', { guidelineId })}
            variant="secondary"
          />

          {guideline.isCurrent ? (
            <Button title="Encerrar vigência" onPress={() => setConfirming('close')} variant="secondary" />
          ) : null}

          <Button title="Excluir diretriz" onPress={() => setConfirming('delete')} variant="danger" />
        </View>
      ) : null}

      <ConfirmDialog
        visible={confirming !== null}
        title={confirming === 'close' ? 'Encerrar vigência' : 'Excluir diretriz'}
        message={
          confirming === 'close'
            ? 'A diretriz deixa de ser vigente a partir de agora, mas continua vinculada às ideias e projetos que já a referenciam.'
            : 'A exclusão é permanente. Se a diretriz já estiver vinculada a ideias ou projetos, o Flow recusará e sugerirá encerrá-la.'
        }
        confirmLabel={confirming === 'close' ? 'Encerrar' : 'Excluir'}
        destructive={confirming === 'delete'}
        loading={closeGuideline.isPending || deleteGuideline.isPending}
        onConfirm={confirmAction}
        onCancel={() => setConfirming(null)}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  first: { marginTop: theme.spacing.lg },
  spaced: { marginTop: theme.spacing.md },
  badges: { flexDirection: 'row', gap: theme.spacing.sm, marginBottom: theme.spacing.md },
  title: { marginBottom: theme.spacing.sm },
  metaRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: theme.spacing.xs,
  },
  blockTitle: { marginBottom: theme.spacing.md },
  actions: { gap: theme.spacing.sm, marginTop: theme.spacing.sm },
});

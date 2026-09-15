import React from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useDashboardSummary } from '../../api/queries';
import { useAuthStore } from '../../store/authStore';
import { Button, Card, Divider, Screen, SectionHeader, Txt } from '../../components/primitives';
import { EmptyState, ErrorState, SkeletonList } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { CategoryBarChart, TrendChart } from '../../components/charts';
import {
  DeadlineBadge,
  DistributionBar,
  KpiGrid,
  KpiTile,
  MoneyRow,
  PercentRow,
  ProjectStatusBadge,
} from '../../components/domain';
import { projectStageLabel, projectStatusLabel } from '../../i18n/labels';
import {
  formatCompactCurrency,
  formatDays,
  formatNumber,
  formatPercent,
} from '../../utils/format';
import { theme } from '../../theme';
import type { LeadershipStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<LeadershipStackParams>;

/**
 * The executive dashboard.
 *
 * Everything here arrives pre-aggregated from a single endpoint — the client renders, it
 * does not compute. The order is deliberate: what the programme produced, then what is in
 * the way, then where the value came from.
 */
export function DashboardScreen() {
  const navigation = useNavigation<Nav>();
  const session = useAuthStore((state) => state.session);

  const query = useDashboardSummary();
  const refreshControl = useRefreshControl(query);

  if (query.isPending) {
    return (
      <Screen>
        <SkeletonList count={6} />
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

  const data = query.data;
  const hasAnything = data.ideas.total > 0 || data.projects.total > 0;

  if (!hasAnything) {
    return (
      <Screen scroll refreshControl={refreshControl}>
        <EmptyState
          icon="◷"
          title="O programa ainda não começou"
          message="Assim que as primeiras ideias forem enviadas e os projetos criados, este painel passa a mostrar conversão, gargalos e resultado."
        />
      </Screen>
    );
  }

  const { ideas, projects, financial, impact } = data;

  return (
    <Screen scroll refreshControl={refreshControl}>
      <View style={styles.greeting}>
        <Txt variant="caption" color={theme.colors.text.secondary}>
          Painel executivo
        </Txt>
        <Txt variant="heading">{session?.name?.split(' ')[0] ?? 'Liderança'}</Txt>
      </View>

      {/* ── Value delivered ── */}
      <SectionHeader title="Resultado" subtitle="Realizado, não projetado" />

      <KpiGrid>
        <KpiTile
          label="Valor realizado"
          value={formatCompactCurrency(financial.actualNetValue)}
          hint={`${financial.projectsWithActual} projeto(s) medido(s)`}
          tone={financial.actualNetValue > 0 ? 'positive' : 'default'}
        />
        <KpiTile
          label="ROI realizado"
          value={formatPercent(financial.actualRoiAverage, 1)}
          hint="Média dos projetos medidos"
          tone={
            financial.actualRoiAverage !== null && financial.actualRoiAverage > 0
              ? 'positive'
              : 'default'
          }
        />
        <KpiTile
          label="Horas economizadas"
          value={formatNumber(impact.totalTimeSavedHours)}
          hint={`${impact.projectsReporting} projeto(s)`}
        />
        <KpiTile
          label="Produtividade"
          value={formatPercent(impact.averageProductivityGainPercent, 1)}
          hint="Ganho médio"
        />
      </KpiGrid>

      <Card style={styles.spaced}>
        <Txt variant="overline" color={theme.colors.text.muted}>
          Estimado vs realizado
        </Txt>

        <View style={styles.blockGap}>
          <MoneyRow label="Valor estimado" value={financial.estimatedNetValue} />
          <MoneyRow label="Valor realizado" value={financial.actualNetValue} tone="positive" />
          <Divider spacing={theme.spacing.sm} />
          <MoneyRow label="Receita realizada" value={financial.actualRevenue} />
          <MoneyRow label="Economia realizada" value={financial.actualSavings} />
          <MoneyRow label="Custo realizado" value={financial.actualCost} tone="negative" />
        </View>

        {financial.averagePaybackMonths !== null ? (
          <>
            <Divider spacing={theme.spacing.sm} />
            <Txt variant="caption" color={theme.colors.text.secondary}>
              {`Payback médio de ${formatNumber(financial.averagePaybackMonths)} meses`}
            </Txt>
          </>
        ) : null}
      </Card>

      {/* ── Bottlenecks ── */}
      <SectionHeader title="O que está travando" />

      <KpiGrid>
        <KpiTile
          label="Índice de gargalo"
          value={formatPercent(projects.bottleneckIndex)}
          hint="Do trabalho em andamento"
          tone={projects.bottleneckIndex > 30 ? 'danger' : projects.bottleneckIndex > 10 ? 'warning' : 'positive'}
        />
        <KpiTile
          label="Bloqueados"
          value={String(projects.blocked)}
          hint={projects.averageBlockedDays > 0 ? `Média ${formatDays(projects.averageBlockedDays)}` : undefined}
          tone={projects.blocked > 0 ? 'danger' : 'positive'}
        />
        <KpiTile
          label="Atrasados"
          value={String(projects.overdue)}
          tone={projects.overdue > 0 ? 'danger' : 'positive'}
        />
        <KpiTile
          label="Prazo em risco"
          value={String(projects.atRisk)}
          tone={projects.atRisk > 0 ? 'warning' : 'positive'}
        />
      </KpiGrid>

      {data.blockedProjects.length > 0 ? (
        <Card style={styles.spaced}>
          <Txt variant="overline" color={theme.colors.status.blocked.text}>
            Projetos bloqueados
          </Txt>

          {data.blockedProjects.slice(0, 5).map((project) => (
            <Card
              key={project.projectId}
              style={styles.riskRow}
              accent={theme.colors.status.blocked.text}
              onPress={() => navigation.navigate('ProjectDetail', { projectId: project.projectId })}
            >
              <View style={styles.riskHeader}>
                <Txt variant="title" numberOfLines={2} style={styles.flex}>
                  {project.title}
                </Txt>
                <Txt variant="label" color={theme.colors.status.blocked.text}>
                  {formatDays(project.daysBlocked)}
                </Txt>
              </View>

              <Txt variant="caption" color={theme.colors.text.secondary} numberOfLines={2} style={styles.riskReason}>
                {project.reason}
              </Txt>

              <Txt variant="caption" color={theme.colors.text.muted} style={styles.riskOwner}>
                {project.ownerName}
              </Txt>
            </Card>
          ))}
        </Card>
      ) : null}

      {data.projectsAtRisk.length > 0 ? (
        <Card style={styles.spaced}>
          <Txt variant="overline" color={theme.colors.warning}>
            Prazo em risco
          </Txt>

          {data.projectsAtRisk.slice(0, 5).map((project) => (
            <Card
              key={project.projectId}
              style={styles.riskRow}
              accent={project.isOverdue ? theme.colors.danger : theme.colors.warning}
              onPress={() => navigation.navigate('ProjectDetail', { projectId: project.projectId })}
            >
              <View style={styles.riskHeader}>
                <Txt variant="title" numberOfLines={2} style={styles.flex}>
                  {project.title}
                </Txt>
                <DeadlineBadge
                  daysToDeadline={project.daysToDeadline}
                  isOverdue={project.isOverdue}
                  isAtRisk={!project.isOverdue}
                />
              </View>

              <Txt variant="caption" color={theme.colors.text.muted} style={styles.riskOwner}>
                {`${project.ownerName} · ${formatPercent(project.progressPercentage)} concluído`}
              </Txt>
            </Card>
          ))}
        </Card>
      ) : null}

      {/* ── Funnel ── */}
      <SectionHeader title="Funil de inovação" />

      <Card>
        <PercentRow label="Taxa de aprovação" value={ideas.approvalRate} />
        <PercentRow label="Conversão em projeto" value={ideas.conversionRate} />

        <Divider spacing={theme.spacing.sm} />

        <View style={styles.funnelRow}>
          <FunnelCell label="Enviadas" value={ideas.total} />
          <FunnelCell label="Em análise" value={ideas.underReview} />
          <FunnelCell label="Aprovadas" value={ideas.approved} />
          <FunnelCell label="Viraram projeto" value={ideas.convertedToProjects} />
        </View>
      </Card>

      {/* ── Portfolio ── */}
      <SectionHeader title="Portfólio" subtitle={`${projects.total} projetos`} />

      <Card>
        <Txt variant="label" style={styles.blockTitle}>
          Por situação
        </Txt>
        <DistributionBar
          slices={projects.byStatus}
          labelFor={(label) => projectStatusLabel[label as keyof typeof projectStatusLabel] ?? label}
        />

        <Divider />

        <Txt variant="label" style={styles.blockTitle}>
          Por etapa
        </Txt>
        <DistributionBar
          slices={projects.byStage}
          labelFor={(label) => projectStageLabel[label as keyof typeof projectStageLabel] ?? label}
        />

        <Divider spacing={theme.spacing.sm} />

        <Txt variant="caption" color={theme.colors.text.secondary}>
          {`Progresso médio ${formatPercent(projects.averageProgress)} · conclusão média em ${formatDays(projects.averageCompletionDays)}`}
        </Txt>
      </Card>

      {/* ── Trends ── */}
      <SectionHeader title="Tendência" subtitle="Últimos meses" />

      <Card>
        <TrendChart data={data.trends} />
      </Card>

      {/* ── Strategy ── */}
      {data.byStrategy.length > 0 ? (
        <>
          <SectionHeader title="Por diretriz estratégica" />

          <Card>
            <CategoryBarChart
              data={data.byStrategy.map((s) => ({ label: s.title, value: s.projectCount }))}
              emptyMessage="Nenhuma diretriz gerou projetos ainda."
              color={theme.colors.chart[4]}
            />

            <Divider />

            {data.byStrategy.slice(0, 5).map((strategy) => (
              <Card
                key={strategy.guidelineId}
                style={styles.strategyRow}
                onPress={() =>
                  navigation.navigate('StrategyDetail', { guidelineId: strategy.guidelineId })
                }
              >
                <Txt variant="body" numberOfLines={2}>
                  {strategy.title}
                </Txt>
                <Txt variant="caption" color={theme.colors.text.muted} style={styles.strategyMeta}>
                  {`${strategy.ideaCount} ideias · ${strategy.projectCount} projetos · ${strategy.completedProjectCount} concluídos`}
                  {strategy.isCurrent ? '' : ' · encerrada'}
                </Txt>
              </Card>
            ))}
          </Card>
        </>
      ) : null}

      {data.byCampaign.length > 0 ? (
        <>
          <SectionHeader title="Por campanha" />
          <Card>
            <CategoryBarChart
              data={data.byCampaign.map((c) => ({ label: c.campaign, value: c.projectCount }))}
              emptyMessage="Nenhuma campanha gerou projetos ainda."
              color={theme.colors.chart[1]}
            />
          </Card>
        </>
      ) : null}

      {/* ── Rankings ── */}
      {data.topIdeas.length > 0 ? (
        <>
          <SectionHeader title="Ideias mais bem avaliadas" />
          <Card>
            {data.topIdeas.map((idea, index) => (
              <View key={idea.ideaId} style={styles.rankRow}>
                <Txt variant="label" color={theme.colors.text.muted} style={styles.rankIndex}>
                  {index + 1}
                </Txt>
                <View style={styles.flex}>
                  <Txt variant="body" numberOfLines={1}>
                    {idea.title}
                  </Txt>
                  <Txt variant="caption" color={theme.colors.text.muted}>
                    {idea.submittedByName}
                  </Txt>
                </View>
                <Txt variant="label" color={theme.colors.text.brand}>
                  {idea.flowScore ?? idea.score ?? '—'}
                </Txt>
              </View>
            ))}
          </Card>
        </>
      ) : null}

      {data.topProjects.length > 0 ? (
        <>
          <SectionHeader title="Projetos de maior retorno" />
          <Card>
            {data.topProjects.map((project, index) => (
              <View key={project.projectId} style={styles.rankRow}>
                <Txt variant="label" color={theme.colors.text.muted} style={styles.rankIndex}>
                  {index + 1}
                </Txt>
                <View style={styles.flex}>
                  <Txt variant="body" numberOfLines={1}>
                    {project.title}
                  </Txt>
                  <ProjectStatusBadge status={project.status} />
                </View>
                <Txt variant="label" color={theme.colors.success}>
                  {formatCompactCurrency(project.netValue)}
                </Txt>
              </View>
            ))}
          </Card>
        </>
      ) : null}

      <View style={styles.actions}>
        <Button title="Gerar insights executivos" onPress={() => navigation.navigate('Insights')} />
        <Button
          title="Ver todos os projetos"
          onPress={() => navigation.navigate('LeadershipProjects')}
          variant="secondary"
        />
      </View>
    </Screen>
  );
}

function FunnelCell({ label, value }: { label: string; value: number }) {
  return (
    <View style={styles.funnelCell}>
      <Txt variant="kpiSmall">{value}</Txt>
      <Txt variant="caption" color={theme.colors.text.muted} numberOfLines={2} align="center">
        {label}
      </Txt>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  greeting: { marginTop: theme.spacing.lg, marginBottom: theme.spacing.md },
  spaced: { marginTop: theme.spacing.md },
  blockGap: { marginTop: theme.spacing.sm },
  blockTitle: { marginBottom: theme.spacing.md },

  riskRow: { marginTop: theme.spacing.md },
  riskHeader: { flexDirection: 'row', alignItems: 'flex-start', gap: theme.spacing.sm },
  riskReason: { marginTop: theme.spacing.xs },
  riskOwner: { marginTop: theme.spacing.sm },

  funnelRow: { flexDirection: 'row', justifyContent: 'space-between', gap: theme.spacing.sm },
  funnelCell: { flex: 1, alignItems: 'center', gap: theme.spacing.xxs },

  strategyRow: { marginTop: theme.spacing.sm },
  strategyMeta: { marginTop: theme.spacing.xxs },

  rankRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: theme.spacing.md,
    paddingVertical: theme.spacing.sm,
  },
  rankIndex: { width: 20 },

  actions: { gap: theme.spacing.sm, marginTop: theme.spacing.xxl },
});

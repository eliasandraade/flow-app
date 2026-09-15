import React, { useCallback, useMemo, useState } from 'react';
import { FlatList, StyleSheet, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useProjects } from '../../api/queries';
import { useAuthStore } from '../../store/authStore';
import { Button, Chip, ChipRow, Screen, Txt } from '../../components/primitives';
import { EmptyState, ErrorState, SkeletonList } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { ProjectCard } from '../../components/domain';
import { projectStageLabel, projectStatusLabel } from '../../i18n/labels';
import { theme } from '../../theme';
import type { ProjectStage, ProjectStatus, ProjectSummary } from '../../api/types';
import type { ManagerProjectsStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<ManagerProjectsStackParams>;

const STATUSES: (ProjectStatus | null)[] = [
  null,
  'InProgress',
  'Blocked',
  'Planned',
  'Completed',
  'Cancelled',
];

const STAGES: ProjectStage[] = ['Discovery', 'Planning', 'Execution', 'Validation', 'Rollout'];

export function ProjectListScreen({ readOnly = false }: { readOnly?: boolean }) {
  const navigation = useNavigation<Nav>();
  const role = useAuthStore((state) => state.session?.role);
  const canCreate = !readOnly && role === 'Manager';

  const [status, setStatus] = useState<ProjectStatus | null>(null);
  const [stage, setStage] = useState<ProjectStage | null>(null);
  const [attentionOnly, setAttentionOnly] = useState(false);

  const query = useProjects({
    status: status ?? undefined,
    stage: stage ?? undefined,
    take: 100,
  });

  const refreshControl = useRefreshControl(query);

  // "Needs attention" is a client-side view over data the API already flags, so it costs
  // nothing extra and answers the question a manager opens this screen with.
  const projects = useMemo(() => {
    const all = query.data ?? [];
    if (!attentionOnly) return all;

    return all.filter((p) => p.status === 'Blocked' || p.isOverdue || p.isAtRisk);
  }, [query.data, attentionOnly]);

  const attentionCount = useMemo(
    () =>
      (query.data ?? []).filter((p) => p.status === 'Blocked' || p.isOverdue || p.isAtRisk).length,
    [query.data]
  );

  const renderItem = useCallback(
    ({ item }: { item: ProjectSummary }) => (
      <ProjectCard
        project={item}
        onPress={() => navigation.navigate('ProjectDetail', { projectId: item.id })}
      />
    ),
    [navigation]
  );

  if (query.isPending) {
    return (
      <Screen>
        <SkeletonList count={5} />
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

  return (
    <Screen padded={false}>
      <View style={styles.filters}>
        <ChipRow>
          <Chip
            label={attentionCount > 0 ? `Precisam de atenção (${attentionCount})` : 'Precisam de atenção'}
            selected={attentionOnly}
            onPress={() => setAttentionOnly(!attentionOnly)}
            tone={theme.colors.status.risk}
          />
        </ChipRow>

        <ChipRow>
          {STATUSES.map((value) => (
            <Chip
              key={value ?? 'all'}
              label={value ? projectStatusLabel[value] : 'Todos'}
              selected={status === value}
              onPress={() => setStatus(value)}
            />
          ))}
        </ChipRow>

        <ChipRow>
          <Chip label="Toda etapa" selected={stage === null} onPress={() => setStage(null)} />
          {STAGES.map((value) => (
            <Chip
              key={value}
              label={projectStageLabel[value]}
              selected={stage === value}
              onPress={() => setStage(stage === value ? null : value)}
            />
          ))}
        </ChipRow>
      </View>

      <FlatList
        data={projects}
        keyExtractor={(item) => item.id}
        renderItem={renderItem}
        refreshControl={refreshControl}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        initialNumToRender={8}
        maxToRenderPerBatch={8}
        windowSize={9}
        removeClippedSubviews
        ListEmptyComponent={
          <EmptyState
            icon="▤"
            title={attentionOnly ? 'Nada travado' : 'Nenhum projeto'}
            message={
              attentionOnly
                ? 'Nenhum projeto bloqueado, atrasado ou em risco. Bom sinal.'
                : canCreate
                  ? 'Projetos nascem de ideias aprovadas ou podem ser criados diretamente.'
                  : 'Ainda não há projetos em execução.'
            }
            actionLabel={canCreate && !attentionOnly ? 'Criar projeto' : undefined}
            onAction={canCreate && !attentionOnly ? () => navigation.navigate('ProjectForm') : undefined}
          />
        }
      />

      {canCreate ? (
        <View style={styles.footer}>
          <Button title="Novo projeto" onPress={() => navigation.navigate('ProjectForm')} />
        </View>
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  filters: {
    paddingHorizontal: theme.spacing.lg,
    paddingBottom: theme.spacing.sm,
    gap: theme.spacing.xxs,
  },
  list: { paddingHorizontal: theme.spacing.lg, paddingBottom: theme.spacing.lg, flexGrow: 1 },
  separator: { height: theme.spacing.md },
  footer: {
    paddingHorizontal: theme.spacing.lg,
    paddingTop: theme.spacing.md,
    paddingBottom: theme.spacing.lg,
    borderTopWidth: 1,
    borderTopColor: theme.colors.surface.border,
    backgroundColor: theme.colors.surface.card,
  },
});

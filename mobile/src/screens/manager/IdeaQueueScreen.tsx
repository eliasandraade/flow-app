import React, { useCallback, useState } from 'react';
import { FlatList, StyleSheet, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useIdeas } from '../../api/queries';
import { Button, Chip, ChipRow, Screen, Txt } from '../../components/primitives';
import { EmptyState, ErrorState, SkeletonList } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { IdeaCard } from '../../components/domain';
import { ideaStatusLabel } from '../../i18n/labels';
import { theme } from '../../theme';
import type { IdeaStatus, IdeaSummary } from '../../api/types';
import type { ManagerIdeasStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<ManagerIdeasStackParams>;
type Sort = 'flowScore' | 'score' | 'priority' | 'createdAt';

const STATUSES: (IdeaStatus | null)[] = [null, 'UnderReview', 'Approved', 'Rejected', 'Draft'];

const SORT_LABEL: Record<Sort, string> = {
  flowScore: 'FlowScore',
  score: 'Nota',
  priority: 'Prioridade',
  createdAt: 'Mais recentes',
};

const MAX_COMPARE = 5;

/**
 * The manager's review queue.
 *
 * Defaults to ideas awaiting a decision, sorted by FlowScore, because that is the question
 * the screen exists to answer: what should I look at first.
 */
export function IdeaQueueScreen() {
  const navigation = useNavigation<Nav>();

  const [status, setStatus] = useState<IdeaStatus | null>('UnderReview');
  const [sortBy, setSortBy] = useState<Sort>('flowScore');
  const [selection, setSelection] = useState<string[]>([]);

  const query = useIdeas({
    status: status ?? undefined,
    sortBy: sortBy === 'createdAt' ? undefined : sortBy,
    take: 100,
  });

  const refreshControl = useRefreshControl(query);
  const selecting = selection.length > 0;

  const toggleSelection = useCallback((id: string) => {
    setSelection((current) => {
      if (current.includes(id)) return current.filter((value) => value !== id);
      if (current.length >= MAX_COMPARE) return current;
      return [...current, id];
    });
  }, []);

  const renderItem = useCallback(
    ({ item }: { item: IdeaSummary }) => (
      <IdeaCard
        idea={item}
        selected={selection.includes(item.id)}
        onPress={() => {
          if (selecting) toggleSelection(item.id);
          else navigation.navigate('ManagerIdeaDetail', { ideaId: item.id });
        }}
      />
    ),
    [navigation, selecting, selection, toggleSelection]
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
          {STATUSES.map((value) => (
            <Chip
              key={value ?? 'all'}
              label={value ? ideaStatusLabel[value] : 'Todas'}
              selected={status === value}
              onPress={() => setStatus(value)}
            />
          ))}
        </ChipRow>

        <ChipRow>
          {(Object.keys(SORT_LABEL) as Sort[]).map((value) => (
            <Chip
              key={value}
              label={SORT_LABEL[value]}
              selected={sortBy === value}
              onPress={() => setSortBy(value)}
            />
          ))}
        </ChipRow>
      </View>

      <FlatList
        data={query.data}
        keyExtractor={(item) => item.id}
        renderItem={renderItem}
        refreshControl={refreshControl}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        initialNumToRender={10}
        maxToRenderPerBatch={10}
        windowSize={9}
        removeClippedSubviews
        ListHeaderComponent={
          !selecting && (query.data?.length ?? 0) > 1 ? (
            <Button
              title="Comparar ideias"
              onPress={() => query.data && setSelection([query.data[0].id])}
              variant="secondary"
              compact
              style={styles.compareCta}
            />
          ) : null
        }
        ListEmptyComponent={
          <EmptyState
            icon="◔"
            title={status === 'UnderReview' ? 'Fila vazia' : 'Nada aqui'}
            message={
              status === 'UnderReview'
                ? 'Nenhuma ideia aguardando decisão. Bom sinal.'
                : 'Nenhuma ideia nesta situação no momento.'
            }
          />
        }
      />

      {selecting ? (
        <View style={styles.selectionBar}>
          <View style={styles.flex}>
            <Txt variant="label">
              {selection.length === 1
                ? 'Selecione ao menos mais uma ideia'
                : `${selection.length} ideias selecionadas`}
            </Txt>
            <Txt variant="caption" color={theme.colors.text.muted}>
              {`Toque para selecionar (máximo ${MAX_COMPARE})`}
            </Txt>
          </View>

          <Button
            title="Cancelar"
            onPress={() => setSelection([])}
            variant="ghost"
            compact
            fullWidth={false}
          />
          <Button
            title="Comparar"
            onPress={() => navigation.navigate('IdeaCompare', { ideaIds: selection })}
            compact
            fullWidth={false}
            disabled={selection.length < 2}
          />
        </View>
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  filters: { paddingHorizontal: theme.spacing.lg, paddingBottom: theme.spacing.sm, gap: theme.spacing.xxs },
  list: { paddingHorizontal: theme.spacing.lg, paddingBottom: theme.spacing.xxxl, flexGrow: 1 },
  separator: { height: theme.spacing.md },
  compareCta: { marginBottom: theme.spacing.md, alignSelf: 'flex-start' },
  selectionBar: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: theme.spacing.sm,
    paddingHorizontal: theme.spacing.lg,
    paddingVertical: theme.spacing.md,
    borderTopWidth: 1,
    borderTopColor: theme.colors.surface.border,
    backgroundColor: theme.colors.surface.card,
  },
});

import React, { useCallback, useState } from 'react';
import { FlatList, StyleSheet, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useIdeas } from '../../api/queries';
import { Button, Chip, ChipRow, Screen } from '../../components/primitives';
import { EmptyState, ErrorState, SkeletonList } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { IdeaCard } from '../../components/domain';
import { ideaStatusLabel } from '../../i18n/labels';
import { theme } from '../../theme';
import type { IdeaStatus, IdeaSummary } from '../../api/types';
import type { OperatorStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<OperatorStackParams>;

const FILTERS: (IdeaStatus | null)[] = [null, 'Draft', 'UnderReview', 'Approved', 'Rejected'];

export function MyIdeasScreen() {
  const navigation = useNavigation<Nav>();
  const [status, setStatus] = useState<IdeaStatus | null>(null);

  // The server already scopes an Operator to their own ideas, so no author filter is sent.
  const query = useIdeas({ status: status ?? undefined, take: 100 });
  const refreshControl = useRefreshControl(query);

  const renderItem = useCallback(
    ({ item }: { item: IdeaSummary }) => (
      <IdeaCard idea={item} onPress={() => navigation.navigate('IdeaDetail', { ideaId: item.id })} />
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
          {FILTERS.map((value) => (
            <Chip
              key={value ?? 'all'}
              label={value ? ideaStatusLabel[value] : 'Todas'}
              selected={status === value}
              onPress={() => setStatus(value)}
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
        ListEmptyComponent={
          <EmptyState
            icon="✎"
            title={status ? 'Nada nesta situação' : 'Nenhuma ideia ainda'}
            message={
              status
                ? 'Nenhuma das suas ideias está nesta situação no momento.'
                : 'Um problema que te incomoda no dia a dia costuma ser a melhor primeira ideia.'
            }
            actionLabel={status ? undefined : 'Enviar primeira ideia'}
            onAction={status ? undefined : () => navigation.navigate('IdeaForm')}
          />
        }
      />

      <View style={styles.footer}>
        <Button title="Nova ideia" onPress={() => navigation.navigate('IdeaForm')} />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  filters: { paddingHorizontal: theme.spacing.lg, paddingBottom: theme.spacing.sm },
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

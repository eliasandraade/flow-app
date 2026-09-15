import React, { useMemo, useState } from 'react';
import { FlatList, StyleSheet, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useGuidelines } from '../../api/queries';
import { useAuthStore } from '../../store/authStore';
import { Button, Card, Chip, ChipRow, Screen, Txt } from '../../components/primitives';
import { EmptyState, ErrorState, SkeletonList } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { StatusBadge } from '../../components/domain';
import { guidelineCategoryLabel } from '../../i18n/labels';
import { formatDate } from '../../utils/format';
import { theme } from '../../theme';
import type { Guideline, GuidelineCategory } from '../../api/types';
import type { StrategyStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<StrategyStackParams>;
type Filter = 'current' | 'all';

export function StrategiesScreen() {
  const navigation = useNavigation<Nav>();
  const role = useAuthStore((state) => state.session?.role);
  const canManage = role === 'Leadership';

  const [filter, setFilter] = useState<Filter>('current');
  const [category, setCategory] = useState<GuidelineCategory | null>(null);

  const query = useGuidelines({
    currentOnly: filter === 'current',
    category: category ?? undefined,
  });

  const refreshControl = useRefreshControl(query);

  // Campaigns come from the data rather than a fixed list: leadership invents them.
  const campaigns = useMemo(() => {
    const names = new Set((query.data ?? []).map((g) => g.campaign).filter(Boolean) as string[]);
    return Array.from(names);
  }, [query.data]);

  if (query.isPending) {
    return (
      <Screen>
        <SkeletonList count={4} />
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
          <Chip label="Vigentes" selected={filter === 'current'} onPress={() => setFilter('current')} />
          <Chip label="Todas" selected={filter === 'all'} onPress={() => setFilter('all')} />
        </ChipRow>

        <ChipRow>
          <Chip label="Toda categoria" selected={category === null} onPress={() => setCategory(null)} />
          {(Object.keys(guidelineCategoryLabel) as GuidelineCategory[]).map((key) => (
            <Chip
              key={key}
              label={guidelineCategoryLabel[key]}
              selected={category === key}
              onPress={() => setCategory(category === key ? null : key)}
            />
          ))}
        </ChipRow>

        {campaigns.length > 0 ? (
          <Txt variant="caption" color={theme.colors.text.muted}>
            {`Campanhas ativas: ${campaigns.join(' · ')}`}
          </Txt>
        ) : null}
      </View>

      <FlatList
        data={query.data}
        keyExtractor={(item) => item.id}
        refreshControl={refreshControl}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        initialNumToRender={8}
        renderItem={({ item }) => (
          <GuidelineCard
            guideline={item}
            onPress={() => navigation.navigate('StrategyDetail', { guidelineId: item.id })}
          />
        )}
        ListEmptyComponent={
          <EmptyState
            icon="◎"
            title={filter === 'current' ? 'Nenhuma diretriz vigente' : 'Nenhuma diretriz'}
            message={
              canManage
                ? 'Defina a direção estratégica para orientar as ideias do time.'
                : 'A liderança ainda não publicou diretrizes para este período.'
            }
            actionLabel={canManage ? 'Criar diretriz' : undefined}
            onAction={canManage ? () => navigation.navigate('StrategyForm') : undefined}
          />
        }
        ListFooterComponent={
          canManage && (query.data?.length ?? 0) > 0 ? (
            <Button
              title="Nova diretriz"
              onPress={() => navigation.navigate('StrategyForm')}
              variant="secondary"
              style={styles.footerAction}
            />
          ) : null
        }
      />
    </Screen>
  );
}

function GuidelineCard({ guideline, onPress }: { guideline: Guideline; onPress: () => void }) {
  return (
    <Card
      onPress={onPress}
      accent={guideline.isCurrent ? theme.colors.brand : theme.colors.text.muted}
      accessibilityLabel={`Diretriz ${guideline.title}`}
    >
      <View style={styles.cardHeader}>
        <StatusBadge
          status={guideline.isCurrent ? 'approved' : 'cancelled'}
          label={guideline.isCurrent ? 'Vigente' : 'Encerrada'}
        />
        <StatusBadge status="strategy" label={guidelineCategoryLabel[guideline.category]} />
      </View>

      <Txt variant="title" numberOfLines={2} style={styles.cardTitle}>
        {guideline.title}
      </Txt>

      <Txt variant="body" color={theme.colors.text.secondary} numberOfLines={2}>
        {guideline.description}
      </Txt>

      <View style={styles.cardFooter}>
        <Txt variant="caption" color={theme.colors.text.muted}>
          {guideline.validUntil
            ? `${formatDate(guideline.validFrom)} → ${formatDate(guideline.validUntil)}`
            : `Desde ${formatDate(guideline.validFrom)}`}
        </Txt>

        {guideline.campaign ? (
          <Txt variant="caption" color={theme.colors.text.brand} numberOfLines={1}>
            {guideline.campaign}
          </Txt>
        ) : null}
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  filters: { paddingHorizontal: theme.spacing.lg, gap: theme.spacing.xs, paddingBottom: theme.spacing.sm },
  list: { paddingHorizontal: theme.spacing.lg, paddingBottom: theme.spacing.xxxl },
  separator: { height: theme.spacing.md },
  cardHeader: { flexDirection: 'row', gap: theme.spacing.sm, marginBottom: theme.spacing.sm },
  cardTitle: { marginBottom: theme.spacing.xs },
  cardFooter: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: theme.spacing.md,
    gap: theme.spacing.sm,
  },
  footerAction: { marginTop: theme.spacing.lg },
});

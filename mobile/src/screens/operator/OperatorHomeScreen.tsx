import React from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useCurrentGuidelines, useIdeas, useMyPoints } from '../../api/queries';
import { useAuthStore } from '../../store/authStore';
import { Button, Card, Screen, SectionHeader, Txt } from '../../components/primitives';
import { Skeleton } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { IdeaCard, KpiGrid, KpiTile, StatusBadge } from '../../components/domain';
import { guidelineCategoryLabel } from '../../i18n/labels';
import { theme } from '../../theme';
import type { OperatorStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<OperatorStackParams>;

/**
 * The operator's landing screen.
 *
 * It leads with the strategy in force, because an idea aligned to a live guideline is worth
 * more to the company and scores higher — telling people that up front is more useful than
 * letting them discover it after a rejection.
 */
export function OperatorHomeScreen() {
  const navigation = useNavigation<Nav>();
  const session = useAuthStore((state) => state.session);

  const guidelines = useCurrentGuidelines();
  const ideas = useIdeas({ take: 3 });
  const points = useMyPoints();
  const refreshControl = useRefreshControl(ideas);

  const drafts = (ideas.data ?? []).filter((idea) => idea.status === 'Draft').length;
  const underReview = (ideas.data ?? []).filter((idea) => idea.status === 'UnderReview').length;

  return (
    <Screen scroll refreshControl={refreshControl}>
      <View style={styles.greeting}>
        <Txt variant="caption" color={theme.colors.text.secondary}>
          Olá,
        </Txt>
        <Txt variant="heading">{session?.name?.split(' ')[0] ?? 'bem-vindo'}</Txt>
      </View>

      <KpiGrid>
        <KpiTile label="Meus pontos" value={String(points.data?.points ?? 0)} hint="Ideias aprovadas" />
        <KpiTile label="Em análise" value={String(underReview)} hint="Aguardando o gestor" />
      </KpiGrid>

      <Button
        title="Enviar nova ideia"
        onPress={() => navigation.navigate('IdeaForm')}
        style={styles.cta}
      />

      <SectionHeader
        title="Estratégia vigente"
        subtitle="Ideias ligadas a uma diretriz vigente pontuam mais alto"
      />

      {guidelines.isPending ? (
        <Card>
          <Skeleton width="40%" height={12} />
          <Skeleton width="90%" height={18} style={styles.skeletonGap} />
        </Card>
      ) : guidelines.data && guidelines.data.length > 0 ? (
        <View style={styles.guidelines}>
          {guidelines.data.slice(0, 3).map((guideline) => (
            <Card key={guideline.id}>
              <View style={styles.guidelineHeader}>
                <StatusBadge status="strategy" label={guidelineCategoryLabel[guideline.category]} />
                {guideline.campaign ? (
                  <Txt variant="caption" color={theme.colors.text.brand} numberOfLines={1}>
                    {guideline.campaign}
                  </Txt>
                ) : null}
              </View>

              <Txt variant="title" numberOfLines={2} style={styles.guidelineTitle}>
                {guideline.title}
              </Txt>

              <Txt variant="body" color={theme.colors.text.secondary} numberOfLines={3}>
                {guideline.description}
              </Txt>
            </Card>
          ))}
        </View>
      ) : (
        <Card>
          <Txt variant="body" color={theme.colors.text.secondary}>
            A liderança ainda não publicou diretrizes vigentes. Você pode enviar ideias mesmo
            assim — elas serão avaliadas pelo impacto.
          </Txt>
        </Card>
      )}

      <SectionHeader
        title="Minhas ideias recentes"
        action={
          <Button
            title="Ver todas"
            onPress={() => navigation.navigate('MyIdeas')}
            variant="ghost"
            compact
            fullWidth={false}
          />
        }
      />

      {ideas.isPending ? (
        <Card>
          <Skeleton width="60%" height={16} />
          <Skeleton width="90%" height={12} style={styles.skeletonGap} />
        </Card>
      ) : ideas.data && ideas.data.length > 0 ? (
        <View style={styles.ideas}>
          {ideas.data.map((idea) => (
            <IdeaCard
              key={idea.id}
              idea={idea}
              onPress={() => navigation.navigate('IdeaDetail', { ideaId: idea.id })}
            />
          ))}
        </View>
      ) : (
        <Card>
          <Txt variant="body" color={theme.colors.text.secondary}>
            Você ainda não enviou nenhuma ideia. Um problema que te incomoda no dia a dia
            costuma ser o melhor ponto de partida.
          </Txt>
        </Card>
      )}

      {drafts > 0 ? (
        <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.draftNote}>
          {drafts === 1
            ? 'Você tem 1 rascunho ainda não enviado para análise.'
            : `Você tem ${drafts} rascunhos ainda não enviados para análise.`}
        </Txt>
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  greeting: { marginTop: theme.spacing.lg, marginBottom: theme.spacing.lg },
  cta: { marginTop: theme.spacing.lg },
  guidelines: { gap: theme.spacing.md },
  guidelineHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: theme.spacing.sm,
    marginBottom: theme.spacing.sm,
  },
  guidelineTitle: { marginBottom: theme.spacing.xs },
  ideas: { gap: theme.spacing.md },
  skeletonGap: { marginTop: theme.spacing.sm },
  draftNote: { marginTop: theme.spacing.xl },
});

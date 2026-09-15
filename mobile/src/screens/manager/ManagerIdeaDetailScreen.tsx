import React, { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import {
  useAddIdeaComment,
  useDecideIdea,
  useIdea,
  useIdeaComments,
  useSetFlowScore,
  useSetIdeaPriority,
  useSetIdeaScore,
} from '../../api/queries';
import { Button, Card, Chip, ChipRow, Divider, Field, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ConfirmDialog, ErrorBanner, ErrorState, SkeletonList, SuccessBanner } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { FlowScoreCard, IdeaStatusBadge, PriorityBadge, StatusBadge } from '../../components/domain';
import { flowScoreDimensionLabel, ideaPriorityLabel } from '../../i18n/labels';
import { formatDateTime, formatRelative } from '../../utils/format';
import { toApiError } from '../../api/errors';
import { theme } from '../../theme';
import type { IdeaPriority } from '../../api/types';
import type { ManagerIdeasStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<ManagerIdeasStackParams>;
type Dimension = keyof typeof flowScoreDimensionLabel;

const DIMENSIONS: Dimension[] = [
  'strategicAlignment',
  'impact',
  'feasibility',
  'urgency',
  'confidence',
];

export function ManagerIdeaDetailScreen() {
  const navigation = useNavigation<Nav>();
  const { ideaId } = useRoute<RouteProp<ManagerIdeasStackParams, 'ManagerIdeaDetail'>>().params;

  const query = useIdea(ideaId);
  const comments = useIdeaComments(ideaId);
  const decide = useDecideIdea();
  const setPriority = useSetIdeaPriority();
  const setScore = useSetIdeaScore();
  const setFlowScore = useSetFlowScore();
  const addComment = useAddIdeaComment();
  const refreshControl = useRefreshControl(query);

  const [deciding, setDeciding] = useState<'approve' | 'reject' | null>(null);
  const [decisionComment, setDecisionComment] = useState('');
  const [commentBody, setCommentBody] = useState('');
  const [scoreInput, setScoreInput] = useState('');
  const [components, setComponents] = useState<Record<Dimension, number>>({
    strategicAlignment: 5,
    impact: 5,
    feasibility: 5,
    urgency: 5,
    confidence: 5,
  });
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

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

  const idea = query.data;
  const pendingDecision = idea.status === 'UnderReview';

  async function run(action: () => Promise<unknown>, message: string) {
    setError(null);
    setSuccess(null);

    try {
      await action();
      setSuccess(message);
    } catch (caught) {
      setError(toApiError(caught).message);
    }
  }

  async function confirmDecision() {
    if (!deciding) return;

    setError(null);

    try {
      await decide.mutateAsync({ id: ideaId, decision: deciding, comment: decisionComment.trim() });
      setSuccess(
        deciding === 'approve'
          ? 'Ideia aprovada. O autor foi notificado e ganhou 50 pontos.'
          : 'Ideia não aprovada. O autor foi notificado com a justificativa.'
      );
      setDeciding(null);
      setDecisionComment('');
    } catch (caught) {
      setError(toApiError(caught).message);
      setDeciding(null);
    }
  }

  return (
    <Screen scroll refreshControl={refreshControl}>
      {error ? <ErrorBanner message={error} onDismiss={() => setError(null)} /> : null}
      {success ? <SuccessBanner message={success} /> : null}

      <Card style={styles.first}>
        <View style={styles.badges}>
          <IdeaStatusBadge status={idea.status} />
          <PriorityBadge priority={idea.priority} kind="idea" />
        </View>

        <Txt variant="heading" style={styles.title}>
          {idea.title}
        </Txt>

        <Txt variant="caption" color={theme.colors.text.muted}>
          {`${idea.submittedByName} · ${formatRelative(idea.createdAt)}`}
        </Txt>

        <Divider />

        <Txt variant="overline" color={theme.colors.text.muted}>
          O problema
        </Txt>
        <Txt variant="body" color={theme.colors.text.secondary} style={styles.paragraph}>
          {idea.problem}
        </Txt>

        <Txt variant="overline" color={theme.colors.text.muted} style={styles.blockGap}>
          Como resolver
        </Txt>
        <Txt variant="body" color={theme.colors.text.secondary} style={styles.paragraph}>
          {idea.description}
        </Txt>

        {idea.linkedGuidelineTitle ? (
          <>
            <Divider />
            <StatusBadge status="strategy" label={idea.linkedGuidelineTitle} />
          </>
        ) : null}
      </Card>

      {idea.flowScore ? (
        <>
          <SectionHeader title="FlowScore atual" />
          <FlowScoreCard score={idea.flowScore} />
        </>
      ) : null}

      {pendingDecision ? (
        <>
          <SectionHeader
            title="Avaliar"
            subtitle="Cada dimensão vale de 0 a 10; o total é calculado pelo Flow"
          />

          <Card>
            {DIMENSIONS.map((dimension) => (
              <View key={dimension} style={styles.dimension}>
                <View style={styles.dimensionHeader}>
                  <Txt variant="label" color={theme.colors.text.secondary}>
                    {flowScoreDimensionLabel[dimension]}
                  </Txt>
                  <Txt variant="label" color={theme.colors.text.brand}>
                    {components[dimension]}
                  </Txt>
                </View>

                <ChipRow>
                  {Array.from({ length: 11 }).map((_, value) => (
                    <Chip
                      key={value}
                      label={String(value)}
                      selected={components[dimension] === value}
                      onPress={() => setComponents((c) => ({ ...c, [dimension]: value }))}
                    />
                  ))}
                </ChipRow>
              </View>
            ))}

            <Button
              title="Calcular FlowScore"
              onPress={() =>
                run(
                  () =>
                    setFlowScore.mutateAsync({
                      id: ideaId,
                      components: {
                        strategicAlignment: components.strategicAlignment,
                        impact: components.impact,
                        feasibility: components.feasibility,
                        urgency: components.urgency,
                        confidence: components.confidence,
                      },
                    }),
                  'FlowScore calculado.'
                )
              }
              loading={setFlowScore.isPending}
              variant="secondary"
              style={styles.blockGap}
            />
          </Card>

          <SectionHeader title="Nota manual" subtitle="Sua avaliação direta, soberana sobre o cálculo" />

          <Card>
            <View style={styles.scoreRow}>
              <View style={styles.flex}>
                <Field
                  label="Nota de 0 a 100"
                  value={scoreInput}
                  onChangeText={setScoreInput}
                  placeholder={idea.score !== null ? String(idea.score) : '0 a 100'}
                  keyboardType="number-pad"
                  maxLength={3}
                />
              </View>
            </View>

            <Button
              title="Salvar nota"
              onPress={() => {
                const value = Number(scoreInput);
                if (!Number.isFinite(value) || value < 0 || value > 100) {
                  setError('A nota precisa estar entre 0 e 100.');
                  return;
                }
                void run(() => setScore.mutateAsync({ id: ideaId, score: value }), 'Nota registrada.');
              }}
              loading={setScore.isPending}
              disabled={scoreInput.trim().length === 0}
              variant="secondary"
            />
          </Card>

          <SectionHeader title="Prioridade" />

          <ChipRow>
            {(Object.keys(ideaPriorityLabel) as IdeaPriority[]).map((priority) => (
              <Chip
                key={priority}
                label={ideaPriorityLabel[priority]}
                selected={idea.priority === priority}
                onPress={() =>
                  run(
                    () => setPriority.mutateAsync({ id: ideaId, priority }),
                    `Prioridade definida como ${ideaPriorityLabel[priority].toLowerCase()}.`
                  )
                }
              />
            ))}
          </ChipRow>
        </>
      ) : null}

      <SectionHeader title="Comentários" />

      {comments.data && comments.data.length > 0 ? (
        <View style={styles.comments}>
          {comments.data.map((comment) => (
            <Card key={comment.id}>
              <View style={styles.commentHeader}>
                <Txt variant="label">{comment.authorName}</Txt>
                <Txt variant="caption" color={theme.colors.text.muted}>
                  {formatDateTime(comment.createdAt)}
                </Txt>
              </View>
              <Txt variant="body" color={theme.colors.text.secondary} style={styles.paragraph}>
                {comment.body}
              </Txt>
            </Card>
          ))}
        </View>
      ) : null}

      <Card style={styles.blockGap}>
        <Field
          label="Novo comentário"
          value={commentBody}
          onChangeText={setCommentBody}
          placeholder="Peça um detalhe, registre uma dúvida ou explique o encaminhamento."
          multiline
          maxLength={2000}
        />
        <Button
          title="Comentar"
          onPress={() =>
            run(async () => {
              await addComment.mutateAsync({ id: ideaId, body: commentBody.trim() });
              setCommentBody('');
            }, 'Comentário publicado. O autor foi notificado.')
          }
          loading={addComment.isPending}
          disabled={commentBody.trim().length === 0}
          variant="secondary"
        />
      </Card>

      {pendingDecision ? (
        <View style={styles.actions}>
          <Button title="Aprovar ideia" onPress={() => setDeciding('approve')} />
          <Button title="Não aprovar" onPress={() => setDeciding('reject')} variant="danger" />
        </View>
      ) : null}

      {idea.status === 'Approved' ? (
        <View style={styles.actions}>
          <Button
            title="Pedir rascunho de projeto ao copiloto"
            onPress={() => navigation.navigate('ProjectDraftReview', { ideaId })}
            variant="secondary"
          />
        </View>
      ) : null}

      <ConfirmDialog
        visible={deciding !== null}
        title={deciding === 'approve' ? 'Aprovar ideia' : 'Não aprovar ideia'}
        message={
          deciding === 'approve'
            ? 'O autor será notificado e receberá 50 pontos. A decisão fica registrada na auditoria.'
            : 'Explique por que a ideia não segue. A justificativa vai para o autor e fica registrada.'
        }
        confirmLabel={deciding === 'approve' ? 'Aprovar' : 'Não aprovar'}
        destructive={deciding === 'reject'}
        loading={decide.isPending}
        onConfirm={confirmDecision}
        onCancel={() => {
          setDeciding(null);
          setDecisionComment('');
        }}
      >
        <Field
          label={deciding === 'approve' ? 'Comentário (opcional)' : 'Justificativa'}
          value={decisionComment}
          onChangeText={setDecisionComment}
          placeholder={
            deciding === 'approve'
              ? 'O que fez esta ideia avançar.'
              : 'Um "não" sem motivo faz as pessoas pararem de enviar ideias.'
          }
          multiline
          required={deciding === 'reject'}
          maxLength={2000}
        />
      </ConfirmDialog>
    </Screen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  first: { marginTop: theme.spacing.lg },
  badges: { flexDirection: 'row', gap: theme.spacing.sm, marginBottom: theme.spacing.md },
  title: { marginBottom: theme.spacing.xs },
  paragraph: { marginTop: theme.spacing.xs },
  blockGap: { marginTop: theme.spacing.lg },
  dimension: { marginBottom: theme.spacing.lg },
  dimensionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: theme.spacing.xs,
  },
  scoreRow: { flexDirection: 'row', gap: theme.spacing.md },
  comments: { gap: theme.spacing.md },
  commentHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: theme.spacing.sm,
  },
  actions: { gap: theme.spacing.sm, marginTop: theme.spacing.xl },
});

import React, { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useDeleteIdea, useIdea, useIdeaComments, useSubmitIdea } from '../../api/queries';
import { Button, Card, Divider, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ConfirmDialog, ErrorBanner, ErrorState, SkeletonList, SuccessBanner } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { FlowScoreCard, IdeaStatusBadge, PriorityBadge, StatusBadge } from '../../components/domain';
import { formatDateTime, formatRelative } from '../../utils/format';
import { toApiError } from '../../api/errors';
import { theme } from '../../theme';
import type { OperatorStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<OperatorStackParams>;

export function IdeaDetailScreen() {
  const navigation = useNavigation<Nav>();
  const { ideaId } = useRoute<RouteProp<OperatorStackParams, 'IdeaDetail'>>().params;

  const query = useIdea(ideaId);
  const comments = useIdeaComments(ideaId);
  const submit = useSubmitIdea();
  const remove = useDeleteIdea();
  const refreshControl = useRefreshControl(query);

  const [confirming, setConfirming] = useState<'submit' | 'delete' | null>(null);
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

  async function confirmAction() {
    setError(null);
    setSuccess(null);

    try {
      if (confirming === 'submit') {
        await submit.mutateAsync(ideaId);
        setSuccess('Ideia enviada para análise. Você será avisado quando houver decisão.');
        setConfirming(null);
      } else if (confirming === 'delete') {
        await remove.mutateAsync(ideaId);
        setConfirming(null);
        navigation.goBack();
      }
    } catch (caught) {
      setError(toApiError(caught).message);
      setConfirming(null);
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
          {`Criada ${formatRelative(idea.createdAt)}`}
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
            <Txt variant="overline" color={theme.colors.text.muted}>
              Diretriz vinculada
            </Txt>
            <View style={styles.guidelineRow}>
              <StatusBadge status="strategy" label={idea.linkedGuidelineTitle} />
            </View>
          </>
        ) : null}
      </Card>

      {idea.managerComment ? (
        <Card
          style={[
            styles.decision,
            {
              backgroundColor:
                idea.status === 'Approved'
                  ? theme.colors.successSurface
                  : theme.colors.dangerSurface,
            },
          ]}
        >
          <Txt variant="overline" color={theme.colors.text.muted}>
            {idea.status === 'Approved' ? 'Retorno do gestor' : 'Por que não foi aprovada'}
          </Txt>
          <Txt variant="body" style={styles.paragraph}>
            {idea.managerComment}
          </Txt>
        </Card>
      ) : null}

      {idea.flowScore ? (
        <>
          <SectionHeader
            title="Como esta ideia foi avaliada"
            subtitle="O FlowScore é calculado a partir de dimensões visíveis"
          />
          <FlowScoreCard score={idea.flowScore} />
        </>
      ) : null}

      <SectionHeader title="Comentários" />

      {comments.isPending ? (
        <SkeletonList count={2} />
      ) : comments.data && comments.data.length > 0 ? (
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
      ) : (
        <Card>
          <Txt variant="caption" color={theme.colors.text.secondary}>
            Ainda não há comentários do gestor nesta ideia.
          </Txt>
        </Card>
      )}

      {idea.canEdit || idea.canDelete || idea.status === 'Draft' ? (
        <View style={styles.actions}>
          {idea.status === 'Draft' ? (
            <Button title="Enviar para análise" onPress={() => setConfirming('submit')} />
          ) : null}

          {idea.canEdit ? (
            <Button
              title="Editar rascunho"
              onPress={() => navigation.navigate('IdeaForm', { ideaId })}
              variant="secondary"
            />
          ) : null}

          {idea.canDelete ? (
            <Button title="Excluir rascunho" onPress={() => setConfirming('delete')} variant="danger" />
          ) : null}
        </View>
      ) : null}

      <ConfirmDialog
        visible={confirming !== null}
        title={confirming === 'submit' ? 'Enviar para análise' : 'Excluir rascunho'}
        message={
          confirming === 'submit'
            ? 'Depois de enviada, a ideia não pode mais ser editada. O gestor será notificado.'
            : 'Esta ação é permanente. O registro de que a ideia existiu permanece na auditoria.'
        }
        confirmLabel={confirming === 'submit' ? 'Enviar' : 'Excluir'}
        destructive={confirming === 'delete'}
        loading={submit.isPending || remove.isPending}
        onConfirm={confirmAction}
        onCancel={() => setConfirming(null)}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  first: { marginTop: theme.spacing.lg },
  badges: { flexDirection: 'row', gap: theme.spacing.sm, marginBottom: theme.spacing.md },
  title: { marginBottom: theme.spacing.xs },
  paragraph: { marginTop: theme.spacing.xs },
  blockGap: { marginTop: theme.spacing.lg },
  guidelineRow: { marginTop: theme.spacing.sm },
  decision: { marginTop: theme.spacing.md },
  comments: { gap: theme.spacing.md },
  commentHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: theme.spacing.sm,
  },
  actions: { gap: theme.spacing.sm, marginTop: theme.spacing.xl },
});

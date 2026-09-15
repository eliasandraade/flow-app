import React, { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useConvertIdea, useIdea, useProjectDraft } from '../../api/queries';
import { useAuthStore } from '../../store/authStore';
import { Button, Card, Chip, ChipRow, Divider, Field, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ErrorBanner, ErrorState, LoadingScreen } from '../../components/feedback';
import { projectPriorityLabel } from '../../i18n/labels';
import { formatCurrency, parseDecimalInput } from '../../utils/format';
import { toApiError } from '../../api/errors';
import { theme } from '../../theme';
import type { ProjectPriority } from '../../api/types';
import type { ManagerIdeasStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<ManagerIdeasStackParams>;

/**
 * Editable preview of an assistant-generated project proposal.
 *
 * Nothing exists until the manager presses create. The draft is a suggestion; the project
 * is created by the ordinary command, with authorisation, validation, a snapshot and an
 * audit entry — and the assistant run is then marked as accepted, closing the governance
 * loop.
 */
export function ProjectDraftReviewScreen() {
  const navigation = useNavigation<Nav>();
  const { ideaId } = useRoute<RouteProp<ManagerIdeasStackParams, 'ProjectDraftReview'>>().params;

  const session = useAuthStore((state) => state.session);
  const idea = useIdea(ideaId);
  const draft = useProjectDraft();
  const convert = useConvertIdea();

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState<ProjectPriority>('Medium');
  const [cost, setCost] = useState('');
  const [deadlineDays, setDeadlineDays] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    draft.mutate(ideaId);
    // Requested once per idea.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ideaId]);

  // The draft only seeds the form; every field stays editable.
  useEffect(() => {
    const proposal = draft.data?.draft;
    if (!proposal) return;

    setTitle(proposal.title);
    setDescription(proposal.description);
    setPriority(proposal.suggestedPriority);
    setCost(proposal.suggestedEstimatedCost ? String(proposal.suggestedEstimatedCost) : '');
    setDeadlineDays(proposal.suggestedDurationDays ? String(proposal.suggestedDurationDays) : '');
  }, [draft.data]);

  async function create() {
    setError(null);

    if (!session) return;

    const deadline =
      deadlineDays.trim().length > 0
        ? new Date(Date.now() + Number(deadlineDays) * 86_400_000).toISOString()
        : null;

    try {
      const project = await convert.mutateAsync({
        ideaId,
        input: {
          title: title.trim(),
          description: description.trim(),
          priority,
          ownerId: session.userId,
          estimatedCost: parseDecimalInput(cost),
          deadline,
          // Links the created project back to the suggestion that produced it.
          assistantRunId: draft.data?.assistantRunId ?? null,
        },
      });

      navigation.getParent()?.navigate('ManagerProjects', {
        screen: 'ProjectDetail',
        params: { projectId: project.id },
      });
    } catch (caught) {
      setError(toApiError(caught).message);
    }
  }

  if (draft.isPending || draft.isIdle) {
    return (
      <Screen>
        <LoadingScreen label="O copiloto está montando o rascunho…" />
      </Screen>
    );
  }

  if (draft.isError) {
    return (
      <Screen scroll>
        <ErrorState error={draft.error} onRetry={() => draft.mutate(ideaId)} />

        <Card style={styles.fallback}>
          <Txt variant="body" color={theme.colors.text.secondary}>
            O copiloto está indisponível, mas isso não impede nada: você pode criar o projeto
            manualmente a qualquer momento.
          </Txt>
        </Card>
      </Screen>
    );
  }

  const proposal = draft.data.draft;

  return (
    <Screen scroll>
      {error ? <ErrorBanner message={error} onDismiss={() => setError(null)} /> : null}

      <Card style={styles.notice}>
        <Txt variant="label" color={theme.colors.text.brand}>
          Rascunho, não projeto
        </Txt>
        <Txt variant="caption" color={theme.colors.text.secondary} style={styles.paragraph}>
          Nada foi criado ainda. Revise, ajuste o que quiser e só então crie o projeto.
        </Txt>
      </Card>

      {!proposal.evidenceWasSufficient ? (
        <Card style={styles.warning}>
          <Txt variant="label" color={theme.colors.warning}>
            Base histórica insuficiente
          </Txt>
          <Txt variant="caption" color={theme.colors.text.secondary} style={styles.paragraph}>
            Não havia projetos anteriores comparáveis, então duração e custo não foram
            estimados. Preencha com o que você sabe.
          </Txt>
        </Card>
      ) : null}

      {idea.data ? (
        <Card style={styles.sourceCard}>
          <Txt variant="overline" color={theme.colors.text.muted}>
            Ideia de origem
          </Txt>
          <Txt variant="body" style={styles.paragraph}>
            {idea.data.title}
          </Txt>
        </Card>
      ) : null}

      <SectionHeader title="Projeto proposto" />

      <Card>
        <Field label="Título" value={title} onChangeText={setTitle} required maxLength={200} />
        <Field
          label="Descrição"
          value={description}
          onChangeText={setDescription}
          multiline
          required
          maxLength={4000}
        />

        <Txt variant="label" color={theme.colors.text.secondary} style={styles.fieldLabel}>
          Prioridade
        </Txt>
        <ChipRow>
          {(Object.keys(projectPriorityLabel) as ProjectPriority[]).map((value) => (
            <Chip
              key={value}
              label={projectPriorityLabel[value]}
              selected={priority === value}
              onPress={() => setPriority(value)}
            />
          ))}
        </ChipRow>

        <View style={styles.row}>
          <View style={styles.flex}>
            <Field
              label="Custo estimado"
              value={cost}
              onChangeText={setCost}
              placeholder="0"
              keyboardType="decimal-pad"
              hint={
                proposal.suggestedEstimatedCost
                  ? `Sugerido: ${formatCurrency(proposal.suggestedEstimatedCost)}`
                  : 'Sem base histórica'
              }
            />
          </View>

          <View style={styles.flex}>
            <Field
              label="Prazo (dias)"
              value={deadlineDays}
              onChangeText={setDeadlineDays}
              placeholder="90"
              keyboardType="number-pad"
              hint={
                proposal.suggestedDurationDays
                  ? `Sugerido: ${proposal.suggestedDurationDays} dias`
                  : 'Sem base histórica'
              }
            />
          </View>
        </View>
      </Card>

      {proposal.milestones.length > 0 ? (
        <>
          <SectionHeader title="Marcos sugeridos" />
          <Card>
            {proposal.milestones.map((item, index) => (
              <View key={index} style={styles.milestone}>
                <View style={styles.milestoneIndex}>
                  <Txt variant="caption" color={theme.colors.text.brand}>
                    {index + 1}
                  </Txt>
                </View>
                <Txt variant="body" color={theme.colors.text.secondary} style={styles.flex}>
                  {item}
                </Txt>
              </View>
            ))}
          </Card>
        </>
      ) : null}

      {proposal.risks.length > 0 ? (
        <>
          <SectionHeader title="Riscos apontados" />
          <Card>
            {proposal.risks.map((item, index) => (
              <Txt key={index} variant="body" color={theme.colors.text.secondary} style={styles.bullet}>
                {`• ${item}`}
              </Txt>
            ))}
          </Card>
        </>
      ) : null}

      {proposal.successCriteria.length > 0 ? (
        <>
          <SectionHeader title="Critérios de sucesso" />
          <Card>
            {proposal.successCriteria.map((item, index) => (
              <Txt key={index} variant="body" color={theme.colors.text.secondary} style={styles.bullet}>
                {`• ${item}`}
              </Txt>
            ))}
          </Card>
        </>
      ) : null}

      <SectionHeader title="Justificativa" />
      <Card>
        <Txt variant="body" color={theme.colors.text.secondary}>
          {proposal.rationale}
        </Txt>

        {proposal.evidence.length > 0 ? (
          <>
            <Divider spacing={theme.spacing.md} />
            <Txt variant="overline" color={theme.colors.text.muted}>
              Evidência utilizada
            </Txt>
            {proposal.evidence.map((item, index) => (
              <Txt key={index} variant="caption" color={theme.colors.text.secondary} style={styles.bullet}>
                {`• ${item}`}
              </Txt>
            ))}
          </>
        ) : null}
      </Card>

      <View style={styles.actions}>
        <Button
          title="Criar projeto"
          onPress={create}
          loading={convert.isPending}
          disabled={title.trim().length === 0 || description.trim().length === 0}
        />
        <Button title="Descartar" onPress={() => navigation.goBack()} variant="ghost" />
      </View>

      <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.footer}>
        {`${draft.data.model} · ${draft.data.latencyMs}ms · a criação passa pelas regras normais do Flow`}
      </Txt>
    </Screen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  notice: { marginTop: theme.spacing.lg, backgroundColor: theme.colors.brandSurface },
  warning: { marginTop: theme.spacing.md, backgroundColor: theme.colors.warningSurface },
  sourceCard: { marginTop: theme.spacing.md },
  fallback: { marginTop: theme.spacing.lg },
  paragraph: { marginTop: theme.spacing.xs },
  bullet: { marginTop: theme.spacing.xs },
  fieldLabel: { marginBottom: theme.spacing.xs },
  row: { flexDirection: 'row', gap: theme.spacing.md, marginTop: theme.spacing.lg },
  milestone: { flexDirection: 'row', gap: theme.spacing.md, marginBottom: theme.spacing.md },
  milestoneIndex: {
    width: 24,
    height: 24,
    borderRadius: 12,
    backgroundColor: theme.colors.brandSurface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  actions: { gap: theme.spacing.sm, marginTop: theme.spacing.xl },
  footer: { marginTop: theme.spacing.lg },
});

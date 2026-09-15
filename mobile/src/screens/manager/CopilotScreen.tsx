import React, { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useAssistantComparison, useIdeas } from '../../api/queries';
import { Button, Card, Chip, ChipRow, Divider, Field, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ErrorState, LoadingScreen, SkeletonList } from '../../components/feedback';
import { theme } from '../../theme';
import type { ManagerIdeasStackParams } from '../../navigation/types';

type Nav = NativeStackNavigationProp<ManagerIdeasStackParams>;

const MAX_IDEAS = 5;

const SUGGESTED_QUESTIONS = [
  'Qual tem maior alinhamento estratégico?',
  'Quais são os principais riscos de cada uma?',
  'Qual entrega valor mais rápido?',
  'Onde estão os trade-offs entre elas?',
];

/**
 * The manager's copilot.
 *
 * Deliberately not a chat box: the manager picks the ideas, optionally asks a specific
 * question, and gets back a structured reading — assessment per idea, trade-offs and a
 * recommendation, each with the evidence behind it.
 */
export function CopilotScreen() {
  const navigation = useNavigation<Nav>();
  const params = useRoute<RouteProp<ManagerIdeasStackParams, 'Copilot'>>().params;

  const [selection, setSelection] = useState<string[]>(params?.ideaIds ?? []);
  const [question, setQuestion] = useState('');

  const ideas = useIdeas({ status: 'UnderReview', sortBy: 'flowScore', take: 30 });
  const analyse = useAssistantComparison();

  function toggle(id: string) {
    setSelection((current) => {
      if (current.includes(id)) return current.filter((value) => value !== id);
      if (current.length >= MAX_IDEAS) return current;
      return [...current, id];
    });
  }

  if (ideas.isPending) {
    return (
      <Screen>
        <SkeletonList count={4} />
      </Screen>
    );
  }

  if (ideas.isError) {
    return (
      <Screen>
        <ErrorState error={ideas.error} onRetry={() => void ideas.refetch()} />
      </Screen>
    );
  }

  const insight = analyse.data?.insight;

  return (
    <Screen scroll>
      <Card style={styles.first}>
        <Txt variant="subheading">Copiloto Flow</Txt>
        <Txt variant="body" color={theme.colors.text.secondary} style={styles.paragraph}>
          Selecione as ideias que está avaliando. O copiloto analisa alinhamento, riscos e
          trade-offs a partir dos dados do Flow e mostra a evidência de cada conclusão.
        </Txt>
        <Txt variant="caption" color={theme.colors.text.muted} style={styles.paragraph}>
          Ele recomenda. Quem decide é você.
        </Txt>
      </Card>

      <SectionHeader
        title="Ideias em análise"
        subtitle={`Selecione de 2 a ${MAX_IDEAS}`}
      />

      {ideas.data.length === 0 ? (
        <Card>
          <Txt variant="body" color={theme.colors.text.secondary}>
            Não há ideias aguardando análise no momento.
          </Txt>
        </Card>
      ) : (
        <View style={styles.ideaList}>
          {ideas.data.map((idea) => {
            const selected = selection.includes(idea.id);

            return (
              <Card
                key={idea.id}
                onPress={() => toggle(idea.id)}
                style={selected ? styles.selected : undefined}
                accent={selected ? theme.colors.brand : undefined}
              >
                <View style={styles.ideaRow}>
                  <Txt variant="body" numberOfLines={2} style={styles.flex}>
                    {idea.title}
                  </Txt>
                  <Txt variant="label" color={theme.colors.text.brand}>
                    {idea.flowScore ?? '—'}
                  </Txt>
                </View>
              </Card>
            );
          })}
        </View>
      )}

      <SectionHeader title="Sua pergunta" subtitle="Opcional" />

      <ChipRow>
        {SUGGESTED_QUESTIONS.map((suggestion) => (
          <Chip
            key={suggestion}
            label={suggestion}
            selected={question === suggestion}
            onPress={() => setQuestion(question === suggestion ? '' : suggestion)}
          />
        ))}
      </ChipRow>

      <Card style={styles.questionCard}>
        <Field
          label="Pergunta"
          value={question}
          onChangeText={setQuestion}
          placeholder="O que você quer entender sobre estas ideias?"
          multiline
          maxLength={500}
        />
      </Card>

      <Button
        title="Analisar"
        onPress={() => analyse.mutate({ ideaIds: selection, question: question.trim() || null })}
        loading={analyse.isPending}
        disabled={selection.length < 2}
      />

      {analyse.isPending ? <LoadingScreen label="O copiloto está analisando…" /> : null}

      {analyse.isError ? (
        <View style={styles.result}>
          <ErrorState
            error={analyse.error}
            onRetry={() => analyse.mutate({ ideaIds: selection, question: question.trim() || null })}
          />
        </View>
      ) : null}

      {insight ? (
        <View style={styles.result}>
          {!insight.evidenceWasSufficient ? (
            <Card style={styles.warningCard}>
              <Txt variant="label" color={theme.colors.warning}>
                Evidência insuficiente
              </Txt>
              <Txt variant="caption" color={theme.colors.text.secondary} style={styles.paragraph}>
                O copiloto avisou que os dados disponíveis não sustentam uma conclusão firme.
                Trate o que segue como hipótese, não como recomendação.
              </Txt>
            </Card>
          ) : null}

          <SectionHeader title="Leitura geral" />
          <Card>
            <Txt variant="body">{insight.summary}</Txt>
          </Card>

          <SectionHeader title="Por ideia" />
          <View style={styles.ideaList}>
            {insight.assessments.map((assessment) => (
              <Card key={assessment.ideaId}>
                <Txt variant="title" numberOfLines={2}>
                  {assessment.title}
                </Txt>

                <Txt variant="caption" color={theme.colors.text.secondary} style={styles.paragraph}>
                  {assessment.strategicFit}
                </Txt>

                <Divider spacing={theme.spacing.md} />

                <Txt variant="overline" color={theme.colors.success}>
                  Pontos fortes
                </Txt>
                {assessment.strengths.map((item, index) => (
                  <Txt key={index} variant="caption" color={theme.colors.text.secondary} style={styles.bullet}>
                    {`• ${item}`}
                  </Txt>
                ))}

                <Txt variant="overline" color={theme.colors.warning} style={styles.blockGap}>
                  Riscos
                </Txt>
                {assessment.risks.map((item, index) => (
                  <Txt key={index} variant="caption" color={theme.colors.text.secondary} style={styles.bullet}>
                    {`• ${item}`}
                  </Txt>
                ))}

                <Divider spacing={theme.spacing.md} />

                <Txt variant="caption" color={theme.colors.text.primary}>
                  {assessment.recommendation}
                </Txt>

                <Button
                  title="Abrir ideia"
                  onPress={() =>
                    navigation.navigate('ManagerIdeaDetail', { ideaId: assessment.ideaId })
                  }
                  variant="ghost"
                  compact
                  style={styles.blockGap}
                />
              </Card>
            ))}
          </View>

          {insight.tradeOffs.length > 0 ? (
            <>
              <SectionHeader title="Trade-offs" />
              <Card>
                {insight.tradeOffs.map((item, index) => (
                  <Txt key={index} variant="body" color={theme.colors.text.secondary} style={styles.bullet}>
                    {`• ${item}`}
                  </Txt>
                ))}
              </Card>
            </>
          ) : null}

          {insight.suggestedPriorityRationale ? (
            <>
              <SectionHeader title="Sugestão de prioridade" />
              <Card style={styles.suggestionCard}>
                <Txt variant="body">{insight.suggestedPriorityRationale}</Txt>
                <Txt variant="caption" color={theme.colors.text.muted} style={styles.paragraph}>
                  Sugestão do copiloto. A decisão e o registro de auditoria continuam sendo seus.
                </Txt>
              </Card>
            </>
          ) : null}

          {insight.evidence.length > 0 ? (
            <>
              <SectionHeader title="Evidência utilizada" />
              <Card>
                {insight.evidence.map((item, index) => (
                  <Txt key={index} variant="caption" color={theme.colors.text.secondary} style={styles.bullet}>
                    {`• ${item}`}
                  </Txt>
                ))}
              </Card>
            </>
          ) : null}

          <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.footer}>
            {`${analyse.data?.model} · ${analyse.data?.latencyMs}ms · execução registrada para auditoria`}
          </Txt>
        </View>
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  first: { marginTop: theme.spacing.lg },
  paragraph: { marginTop: theme.spacing.xs },
  bullet: { marginTop: theme.spacing.xs },
  blockGap: { marginTop: theme.spacing.md },
  ideaList: { gap: theme.spacing.sm },
  ideaRow: { flexDirection: 'row', alignItems: 'center', gap: theme.spacing.md },
  selected: { borderColor: theme.colors.brand, borderWidth: 1.5 },
  questionCard: { marginTop: theme.spacing.md, marginBottom: theme.spacing.lg },
  result: { marginTop: theme.spacing.lg },
  warningCard: { backgroundColor: theme.colors.warningSurface },
  suggestionCard: { backgroundColor: theme.colors.brandSurface },
  footer: { marginTop: theme.spacing.xl },
});

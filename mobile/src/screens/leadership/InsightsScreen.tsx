import React, { useEffect } from 'react';
import { StyleSheet, View } from 'react-native';
import { useExecutiveInsights } from '../../api/queries';
import { Button, Card, Divider, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ErrorState, LoadingScreen } from '../../components/feedback';
import { formatDateTime } from '../../utils/format';
import { theme } from '../../theme';
import type { InsightItem } from '../../api/types';

/**
 * Executive insights.
 *
 * The analysis is built strictly from the dashboard payload the API already computed, so
 * it cannot cite a number that is not on the dashboard. When the programme has too little
 * data, the model is instructed to say so — and this screen surfaces that plainly rather
 * than dressing it up.
 */
export function InsightsScreen() {
  const insights = useExecutiveInsights();

  useEffect(() => {
    insights.mutate();
    // Generated once when the screen opens; regenerating is an explicit action.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (insights.isPending || insights.isIdle) {
    return (
      <Screen>
        <LoadingScreen label="Analisando o painel executivo…" />
      </Screen>
    );
  }

  if (insights.isError) {
    return (
      <Screen scroll>
        <ErrorState error={insights.error} onRetry={() => insights.mutate()} />

        <Card style={styles.fallback}>
          <Txt variant="body" color={theme.colors.text.secondary}>
            Os insights são uma camada opcional. Todo o restante do painel executivo continua
            disponível e atualizado.
          </Txt>
        </Card>
      </Screen>
    );
  }

  const { insight, model, latencyMs } = insights.data;

  return (
    <Screen scroll>
      {!insight.evidenceWasSufficient ? (
        <Card style={styles.warning}>
          <Txt variant="label" color={theme.colors.warning}>
            Dados insuficientes para conclusões
          </Txt>
          <Txt variant="caption" color={theme.colors.text.secondary} style={styles.paragraph}>
            O programa ainda não gerou dados suficientes para sustentar uma análise. O que
            segue deve ser lido como leitura preliminar.
          </Txt>
        </Card>
      ) : null}

      <Card style={styles.summary}>
        <Txt variant="overline" color={theme.colors.text.muted}>
          Resumo executivo
        </Txt>
        <Txt variant="body" style={styles.summaryText}>
          {insight.executiveSummary}
        </Txt>
      </Card>

      <InsightSection
        title="Destaques"
        items={insight.highlights}
        accent={theme.colors.success}
        emptyMessage="Nenhum destaque identificado neste ciclo."
      />

      <InsightSection
        title="Riscos"
        items={insight.risks}
        accent={theme.colors.danger}
        emptyMessage="Nenhum risco relevante identificado."
      />

      <InsightSection
        title="Oportunidades"
        items={insight.opportunities}
        accent={theme.colors.warning}
        emptyMessage="Nenhuma oportunidade identificada."
      />

      <InsightSection
        title="Recomendações"
        items={insight.recommendations}
        accent={theme.colors.brand}
        emptyMessage="Nenhuma recomendação no momento."
      />

      {insight.evidence.length > 0 ? (
        <>
          <SectionHeader
            title="Evidência utilizada"
            subtitle="Cada conclusão acima veio destes números"
          />
          <Card>
            {insight.evidence.map((item, index) => (
              <Txt key={index} variant="caption" color={theme.colors.text.secondary} style={styles.bullet}>
                {`• ${item}`}
              </Txt>
            ))}
          </Card>
        </>
      ) : null}

      <Button
        title="Gerar novamente"
        onPress={() => insights.mutate()}
        variant="secondary"
        style={styles.regenerate}
      />

      <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.footer}>
        {`${model} · ${latencyMs}ms · ${formatDateTime(insight.generatedAt)}`}
      </Txt>

      <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.disclaimer}>
        Análise gerada sobre os dados do painel. Execução registrada para auditoria.
      </Txt>
    </Screen>
  );
}

function InsightSection({
  title,
  items,
  accent,
  emptyMessage,
}: {
  title: string;
  items: InsightItem[];
  accent: string;
  emptyMessage: string;
}) {
  return (
    <>
      <SectionHeader title={title} />

      {items.length === 0 ? (
        <Card>
          <Txt variant="caption" color={theme.colors.text.muted}>
            {emptyMessage}
          </Txt>
        </Card>
      ) : (
        <View style={styles.items}>
          {items.map((item, index) => (
            <Card key={index} accent={accent}>
              <Txt variant="title">{item.title}</Txt>
              <Txt variant="body" color={theme.colors.text.secondary} style={styles.paragraph}>
                {item.detail}
              </Txt>

              {item.evidence.length > 0 ? (
                <>
                  <Divider spacing={theme.spacing.md} />
                  {item.evidence.map((evidence, evidenceIndex) => (
                    <Txt
                      key={evidenceIndex}
                      variant="caption"
                      color={theme.colors.text.muted}
                      style={styles.bullet}
                    >
                      {`• ${evidence}`}
                    </Txt>
                  ))}
                </>
              ) : null}
            </Card>
          ))}
        </View>
      )}
    </>
  );
}

const styles = StyleSheet.create({
  warning: { marginTop: theme.spacing.lg, backgroundColor: theme.colors.warningSurface },
  summary: { marginTop: theme.spacing.lg, backgroundColor: theme.colors.brandSurface },
  summaryText: { marginTop: theme.spacing.sm },
  fallback: { marginTop: theme.spacing.lg },
  items: { gap: theme.spacing.md },
  paragraph: { marginTop: theme.spacing.xs },
  bullet: { marginTop: theme.spacing.xxs },
  regenerate: { marginTop: theme.spacing.xxl },
  footer: { marginTop: theme.spacing.lg },
  disclaimer: { marginTop: theme.spacing.xs },
});

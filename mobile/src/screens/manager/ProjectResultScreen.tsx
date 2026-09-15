import React, { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { useProject, useProjectResult, useRecordResult } from '../../api/queries';
import { useAuthStore } from '../../store/authStore';
import { Button, Card, Divider, Field, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ErrorBanner, SkeletonList, SuccessBanner } from '../../components/feedback';
import { MetricRow, MoneyRow } from '../../components/domain';
import { formatCompactCurrency, formatDate, formatPercent, parseDecimalInput } from '../../utils/format';
import { toApiError, type ApiError } from '../../api/errors';
import { theme } from '../../theme';

/**
 * Estimated and realised outcomes.
 *
 * The two groups are edited and saved separately on purpose: a planning figure must never
 * be silently promoted into an achieved result, which is the single easiest way for an
 * innovation programme to start reporting numbers it did not deliver.
 */
export function ProjectResultScreen() {
  const navigation = useNavigation();
  const { projectId } = useRoute<RouteProp<{ p: { projectId: string } }, 'p'>>().params;

  const role = useAuthStore((state) => state.session?.role);
  const canEdit = role === 'Manager';

  const project = useProject(projectId);
  const result = useProjectResult(projectId);
  const record = useRecordResult();

  const [estimated, setEstimated] = useState({ revenue: '', savings: '', cost: '' });
  const [actual, setActual] = useState({ revenue: '', savings: '', cost: '' });
  const [impact, setImpact] = useState({ productivity: '', hours: '', quality: '' });
  const [payback, setPayback] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    const data = result.data;
    if (!data) return;

    setEstimated({
      revenue: data.estimated?.revenue != null ? String(data.estimated.revenue) : '',
      savings: data.estimated?.savings != null ? String(data.estimated.savings) : '',
      cost: data.estimated?.cost != null ? String(data.estimated.cost) : '',
    });

    setActual({
      revenue: data.actual?.revenue != null ? String(data.actual.revenue) : '',
      savings: data.actual?.savings != null ? String(data.actual.savings) : '',
      cost: data.actual?.cost != null ? String(data.actual.cost) : '',
    });

    setImpact({
      productivity: data.productivityGainPercent != null ? String(data.productivityGainPercent) : '',
      hours: data.timeSavedHours != null ? String(data.timeSavedHours) : '',
      quality: data.qualityGainPercent != null ? String(data.qualityGainPercent) : '',
    });

    setPayback(data.paybackPeriodMonths != null ? String(data.paybackPeriodMonths) : '');
    setNotes(data.notes ?? '');
  }, [result.data]);

  async function save(group: 'estimated' | 'actual' | 'impact') {
    setError(null);
    setSuccess(null);

    // Only the group being saved carries values; the others go as null so the server
    // leaves them exactly as they were.
    const payload = {
      estimatedRevenue: group === 'estimated' ? parseDecimalInput(estimated.revenue) : null,
      estimatedSavings: group === 'estimated' ? parseDecimalInput(estimated.savings) : null,
      estimatedCost: group === 'estimated' ? parseDecimalInput(estimated.cost) : null,
      actualRevenue: group === 'actual' ? parseDecimalInput(actual.revenue) : null,
      actualSavings: group === 'actual' ? parseDecimalInput(actual.savings) : null,
      actualCost: group === 'actual' ? parseDecimalInput(actual.cost) : null,
      productivityGainPercent: group === 'impact' ? parseDecimalInput(impact.productivity) : null,
      timeSavedHours: group === 'impact' ? parseDecimalInput(impact.hours) : null,
      qualityGainPercent: group === 'impact' ? parseDecimalInput(impact.quality) : null,
      paybackPeriodMonths: group === 'actual' && payback ? Number(payback) : null,
      notes: group === 'actual' ? notes.trim() || null : null,
    };

    try {
      await record.mutateAsync({ projectId, input: payload });
      setSuccess(SUCCESS[group]);
    } catch (caught) {
      setError(toApiError(caught));
    }
  }

  if (project.isPending) {
    return (
      <Screen>
        <SkeletonList count={3} />
      </Screen>
    );
  }

  const data = result.data;
  const completed = project.data?.status === 'Completed';

  return (
    <Screen scroll>
      {error ? <ErrorBanner message={error.message} onDismiss={() => setError(null)} /> : null}
      {success ? <SuccessBanner message={success} /> : null}

      {data ? (
        <Card style={styles.first}>
          <Txt variant="overline" color={theme.colors.text.muted}>
            Resumo
          </Txt>

          <MoneyRow label="Valor estimado" value={data.estimated?.netValue ?? null} />
          <MoneyRow label="Valor realizado" value={data.actual?.netValue ?? null} tone="positive" />

          <Divider spacing={theme.spacing.sm} />

          <MetricRow
            label="ROI estimado"
            value={data.estimated?.roi != null ? formatPercent(data.estimated.roi, 1) : '—'}
          />
          <MetricRow
            label="ROI realizado"
            value={data.actual?.roi != null ? formatPercent(data.actual.roi, 1) : '—'}
            hint={data.actual ? `Registrado em ${formatDate(data.actual.recordedAt)}` : undefined}
          />

          {data.productivityGainPercent != null ||
          data.timeSavedHours != null ||
          data.qualityGainPercent != null ? (
            <>
              <Divider spacing={theme.spacing.sm} />
              <MetricRow
                label="Ganho de produtividade"
                value={formatPercent(data.productivityGainPercent, 1)}
              />
              <MetricRow
                label="Horas economizadas"
                value={data.timeSavedHours != null ? `${data.timeSavedHours} h` : '—'}
              />
              <MetricRow label="Ganho de qualidade" value={formatPercent(data.qualityGainPercent, 1)} />
            </>
          ) : null}
        </Card>
      ) : (
        <Card style={styles.first}>
          <Txt variant="body" color={theme.colors.text.secondary}>
            Nenhum resultado registrado ainda. Comece pela estimativa; os valores realizados
            entram depois da conclusão.
          </Txt>
        </Card>
      )}

      {!canEdit ? null : (
        <>
          <SectionHeader
            title="Estimativa"
            subtitle="Planejamento. Nunca é sobrescrita pelos valores reais."
          />

          <Card>
            <Field
              label="Receita estimada"
              value={estimated.revenue}
              onChangeText={(value) => setEstimated({ ...estimated, revenue: value })}
              placeholder="0"
              keyboardType="decimal-pad"
            />
            <Field
              label="Economia estimada"
              value={estimated.savings}
              onChangeText={(value) => setEstimated({ ...estimated, savings: value })}
              placeholder="0"
              keyboardType="decimal-pad"
            />
            <Field
              label="Custo estimado"
              value={estimated.cost}
              onChangeText={(value) => setEstimated({ ...estimated, cost: value })}
              placeholder="0"
              keyboardType="decimal-pad"
              hint="O ROI é calculado pelo Flow a partir destes três valores."
            />

            <Button
              title="Salvar estimativa"
              onPress={() => save('estimated')}
              loading={record.isPending}
              variant="secondary"
            />
          </Card>

          <SectionHeader
            title="Resultado realizado"
            subtitle={
              completed
                ? 'Medição após a entrega'
                : 'Disponível, mas normalmente preenchido após a conclusão'
            }
          />

          <Card>
            <Field
              label="Receita realizada"
              value={actual.revenue}
              onChangeText={(value) => setActual({ ...actual, revenue: value })}
              placeholder="0"
              keyboardType="decimal-pad"
            />
            <Field
              label="Economia realizada"
              value={actual.savings}
              onChangeText={(value) => setActual({ ...actual, savings: value })}
              placeholder="0"
              keyboardType="decimal-pad"
            />
            <Field
              label="Custo real"
              value={actual.cost}
              onChangeText={(value) => setActual({ ...actual, cost: value })}
              placeholder="0"
              keyboardType="decimal-pad"
            />
            <Field
              label="Payback (meses)"
              value={payback}
              onChangeText={setPayback}
              placeholder="12"
              keyboardType="number-pad"
            />
            <Field
              label="Observações"
              value={notes}
              onChangeText={setNotes}
              placeholder="O que explica a diferença entre o estimado e o realizado."
              multiline
              maxLength={4000}
            />

            <Button
              title="Salvar resultado realizado"
              onPress={() => save('actual')}
              loading={record.isPending}
            />
          </Card>

          <SectionHeader
            title="Impacto não financeiro"
            subtitle="Um projeto de processo pode valer muito e mover pouco a receita"
          />

          <Card>
            <Field
              label="Ganho de produtividade (%)"
              value={impact.productivity}
              onChangeText={(value) => setImpact({ ...impact, productivity: value })}
              placeholder="0"
              keyboardType="numbers-and-punctuation"
            />
            <Field
              label="Horas economizadas"
              value={impact.hours}
              onChangeText={(value) => setImpact({ ...impact, hours: value })}
              placeholder="0"
              keyboardType="decimal-pad"
            />
            <Field
              label="Ganho de qualidade (%)"
              value={impact.quality}
              onChangeText={(value) => setImpact({ ...impact, quality: value })}
              placeholder="0"
              keyboardType="numbers-and-punctuation"
            />

            <Button
              title="Salvar impacto"
              onPress={() => save('impact')}
              loading={record.isPending}
              variant="secondary"
            />
          </Card>
        </>
      )}

      <View style={styles.footer}>
        <Button title="Voltar ao projeto" onPress={() => navigation.goBack()} variant="ghost" />
      </View>
    </Screen>
  );
}

const SUCCESS = {
  estimated: 'Estimativa salva. Os valores realizados permanecem intactos.',
  actual: 'Resultado realizado registrado. A liderança foi notificada.',
  impact: 'Métricas de impacto salvas.',
};

const styles = StyleSheet.create({
  first: { marginTop: theme.spacing.lg },
  footer: { marginTop: theme.spacing.xl },
});

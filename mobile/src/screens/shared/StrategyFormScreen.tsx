import React, { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { useGuideline, useSaveGuideline } from '../../api/queries';
import { Button, Card, Chip, ChipRow, Field, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ErrorBanner, SkeletonList } from '../../components/feedback';
import { guidelineCategoryLabel } from '../../i18n/labels';
import { toApiError, type ApiError } from '../../api/errors';
import { theme } from '../../theme';
import type { GuidelineCategory } from '../../api/types';
import type { StrategyStackParams } from '../../navigation/types';

/** Accepts DD/MM/AAAA, which is what a Brazilian user will type without being told. */
function parseDate(input: string): string | null {
  const match = input.trim().match(/^(\d{2})\/(\d{2})\/(\d{4})$/);
  if (!match) return null;

  const [, day, month, year] = match;
  const date = new Date(Number(year), Number(month) - 1, Number(day));

  if (
    date.getFullYear() !== Number(year) ||
    date.getMonth() !== Number(month) - 1 ||
    date.getDate() !== Number(day)
  ) {
    return null;
  }

  return date.toISOString();
}

function toInputDate(iso: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';

  return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

export function StrategyFormScreen() {
  const navigation = useNavigation();
  const params = useRoute<RouteProp<StrategyStackParams, 'StrategyForm'>>().params;
  const guidelineId = params?.guidelineId;
  const isEditing = Boolean(guidelineId);

  const existing = useGuideline(guidelineId ?? '', { enabled: isEditing });
  const save = useSaveGuideline();

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState<GuidelineCategory>('OperationalEfficiency');
  const [campaign, setCampaign] = useState('');
  const [validFrom, setValidFrom] = useState(toInputDate(new Date().toISOString()));
  const [validUntil, setValidUntil] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [dateError, setDateError] = useState<{ from?: string; until?: string }>({});

  useEffect(() => {
    if (!existing.data) return;

    setTitle(existing.data.title);
    setDescription(existing.data.description);
    setCategory(existing.data.category);
    setCampaign(existing.data.campaign ?? '');
    setValidFrom(toInputDate(existing.data.validFrom));
    setValidUntil(toInputDate(existing.data.validUntil));
  }, [existing.data]);

  async function submit() {
    setError(null);
    setDateError({});

    const from = parseDate(validFrom);
    if (!from) {
      setDateError({ from: 'Use o formato DD/MM/AAAA.' });
      return;
    }

    let until: string | null = null;
    if (validUntil.trim().length > 0) {
      until = parseDate(validUntil);
      if (!until) {
        setDateError({ until: 'Use o formato DD/MM/AAAA ou deixe em branco.' });
        return;
      }

      if (new Date(until) <= new Date(from)) {
        setDateError({ until: 'O encerramento precisa ser depois do início.' });
        return;
      }
    }

    try {
      await save.mutateAsync({
        id: guidelineId,
        input: {
          title: title.trim(),
          description: description.trim(),
          category,
          campaign: campaign.trim() || null,
          validFrom: from,
          validUntil: until,
        },
      });

      navigation.goBack();
    } catch (caught) {
      setError(toApiError(caught));
    }
  }

  if (isEditing && existing.isPending) {
    return (
      <Screen>
        <SkeletonList count={3} />
      </Screen>
    );
  }

  const canSubmit = title.trim().length > 0 && description.trim().length > 0;

  return (
    <Screen scroll>
      {error ? <ErrorBanner message={error.message} onDismiss={() => setError(null)} /> : null}

      <Card style={styles.first}>
        <Field
          label="Título"
          value={title}
          onChangeText={setTitle}
          placeholder="Reduzir o retrabalho na linha de montagem"
          required
          maxLength={200}
          error={error?.fieldError('Title')}
        />

        <Field
          label="Descrição"
          value={description}
          onChangeText={setDescription}
          placeholder="O que esta diretriz busca alcançar e como o resultado será medido."
          multiline
          required
          maxLength={4000}
          error={error?.fieldError('Description')}
        />

        <Field
          label="Campanha"
          value={campaign}
          onChangeText={setCampaign}
          placeholder="Onda Operacional 2026"
          hint="Opcional. Agrupa diretrizes relacionadas no painel executivo."
          maxLength={120}
          error={error?.fieldError('Campaign')}
        />
      </Card>

      <SectionHeader title="Categoria" />

      <ChipRow>
        {(Object.keys(guidelineCategoryLabel) as GuidelineCategory[]).map((key) => (
          <Chip
            key={key}
            label={guidelineCategoryLabel[key]}
            selected={category === key}
            onPress={() => setCategory(key)}
          />
        ))}
      </ChipRow>

      <SectionHeader
        title="Vigência"
        subtitle="A vigência é derivada do período: não existe um interruptor para ligar ou desligar."
      />

      <Card>
        <Field
          label="Início"
          value={validFrom}
          onChangeText={setValidFrom}
          placeholder="01/01/2026"
          keyboardType="numbers-and-punctuation"
          required
          error={dateError.from ?? error?.fieldError('ValidFrom')}
        />

        <Field
          label="Encerramento"
          value={validUntil}
          onChangeText={setValidUntil}
          placeholder="31/12/2026"
          keyboardType="numbers-and-punctuation"
          hint="Deixe em branco para uma diretriz sem prazo definido."
          error={dateError.until ?? error?.fieldError('ValidUntil')}
        />
      </Card>

      <View style={styles.actions}>
        <Button
          title={isEditing ? 'Salvar alterações' : 'Publicar diretriz'}
          onPress={submit}
          loading={save.isPending}
          disabled={!canSubmit}
        />
        <Button title="Cancelar" onPress={() => navigation.goBack()} variant="ghost" />
      </View>

      <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.note}>
        Toda alteração fica registrada no histórico da diretriz.
      </Txt>
    </Screen>
  );
}

const styles = StyleSheet.create({
  first: { marginTop: theme.spacing.lg },
  actions: { gap: theme.spacing.sm, marginTop: theme.spacing.xl },
  note: { marginTop: theme.spacing.lg },
});

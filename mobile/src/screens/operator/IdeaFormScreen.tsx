import React, { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { useCreateIdea, useCurrentGuidelines, useIdea, useUpdateIdea } from '../../api/queries';
import { Button, Card, Chip, ChipRow, Field, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ErrorBanner, SkeletonList } from '../../components/feedback';
import { guidelineCategoryLabel } from '../../i18n/labels';
import { toApiError, type ApiError } from '../../api/errors';
import { theme } from '../../theme';
import type { OperatorStackParams } from '../../navigation/types';

export function IdeaFormScreen() {
  const navigation = useNavigation();
  const params = useRoute<RouteProp<OperatorStackParams, 'IdeaForm'>>().params;
  const ideaId = params?.ideaId;
  const isEditing = Boolean(ideaId);

  const existing = useIdea(ideaId ?? '', { enabled: isEditing });
  const guidelines = useCurrentGuidelines();
  const create = useCreateIdea();
  const update = useUpdateIdea();

  const [title, setTitle] = useState('');
  const [problem, setProblem] = useState('');
  const [description, setDescription] = useState('');
  const [guidelineId, setGuidelineId] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    if (!existing.data) return;

    setTitle(existing.data.title);
    setProblem(existing.data.problem);
    setDescription(existing.data.description);
    setGuidelineId(existing.data.linkedGuidelineId);
  }, [existing.data]);

  async function submit() {
    setError(null);

    const input = {
      title: title.trim(),
      problem: problem.trim(),
      description: description.trim(),
      linkedGuidelineId: guidelineId,
    };

    try {
      if (isEditing && ideaId) {
        await update.mutateAsync({ id: ideaId, input });
      } else {
        await create.mutateAsync(input);
      }

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

  const canSubmit =
    title.trim().length > 0 && problem.trim().length > 0 && description.trim().length > 0;

  const saving = create.isPending || update.isPending;

  return (
    <Screen scroll>
      {error ? <ErrorBanner message={error.message} onDismiss={() => setError(null)} /> : null}

      <Card style={styles.first}>
        <Field
          label="O problema"
          value={problem}
          onChangeText={setProblem}
          placeholder="O que acontece hoje que não deveria acontecer?"
          multiline
          required
          maxLength={4000}
          hint="Descreva o problema real, com o que você observa no dia a dia."
          error={error?.fieldError('Problem')}
        />

        <Field
          label="Título da ideia"
          value={title}
          onChangeText={setTitle}
          placeholder="Sensor de vibração para manutenção preditiva"
          required
          maxLength={200}
          error={error?.fieldError('Title')}
        />

        <Field
          label="Como resolver"
          value={description}
          onChangeText={setDescription}
          placeholder="O que você propõe fazer, de forma concreta."
          multiline
          required
          maxLength={4000}
          error={error?.fieldError('Description')}
        />
      </Card>

      <SectionHeader
        title="Diretriz estratégica"
        subtitle="Opcional, mas ideias alinhadas a uma diretriz vigente pontuam mais alto"
      />

      {guidelines.data && guidelines.data.length > 0 ? (
        <ChipRow>
          <Chip
            label="Nenhuma"
            selected={guidelineId === null}
            onPress={() => setGuidelineId(null)}
          />
          {guidelines.data.map((guideline) => (
            <Chip
              key={guideline.id}
              label={guideline.title.length > 28 ? `${guideline.title.slice(0, 27)}…` : guideline.title}
              selected={guidelineId === guideline.id}
              onPress={() => setGuidelineId(guidelineId === guideline.id ? null : guideline.id)}
            />
          ))}
        </ChipRow>
      ) : (
        <Card>
          <Txt variant="caption" color={theme.colors.text.secondary}>
            Não há diretrizes vigentes no momento.
          </Txt>
        </Card>
      )}

      {guidelineId && guidelines.data ? (
        <Card style={styles.selectedGuideline}>
          {(() => {
            const selected = guidelines.data.find((g) => g.id === guidelineId);
            if (!selected) return null;

            return (
              <>
                <Txt variant="overline" color={theme.colors.text.muted}>
                  {guidelineCategoryLabel[selected.category]}
                </Txt>
                <Txt variant="body" color={theme.colors.text.secondary} style={styles.selectedText}>
                  {selected.description}
                </Txt>
              </>
            );
          })()}
        </Card>
      ) : null}

      <View style={styles.actions}>
        <Button
          title={isEditing ? 'Salvar rascunho' : 'Criar rascunho'}
          onPress={submit}
          loading={saving}
          disabled={!canSubmit}
        />
        <Button title="Cancelar" onPress={() => navigation.goBack()} variant="ghost" />
      </View>

      <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.note}>
        A ideia fica como rascunho até você enviá-la para análise. Enquanto for rascunho,
        você pode editar ou excluir.
      </Txt>
    </Screen>
  );
}

const styles = StyleSheet.create({
  first: { marginTop: theme.spacing.lg },
  selectedGuideline: { marginTop: theme.spacing.md, backgroundColor: theme.colors.brandSurface },
  selectedText: { marginTop: theme.spacing.xs },
  actions: { gap: theme.spacing.sm, marginTop: theme.spacing.xl },
  note: { marginTop: theme.spacing.lg },
});

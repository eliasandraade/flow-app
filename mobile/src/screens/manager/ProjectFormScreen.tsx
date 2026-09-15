import React, { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { useCreateProject, useCurrentGuidelines, useProject, useUpdateProject } from '../../api/queries';
import { useAuthStore } from '../../store/authStore';
import { Button, Card, Chip, ChipRow, Field, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ErrorBanner, SkeletonList } from '../../components/feedback';
import { projectPriorityLabel } from '../../i18n/labels';
import { parseDecimalInput } from '../../utils/format';
import { toApiError, type ApiError } from '../../api/errors';
import { theme } from '../../theme';
import type { ProjectPriority } from '../../api/types';
import type { ManagerProjectsStackParams } from '../../navigation/types';

export function ProjectFormScreen() {
  const navigation = useNavigation();
  const params = useRoute<RouteProp<ManagerProjectsStackParams, 'ProjectForm'>>().params;
  const projectId = params?.projectId;
  const isEditing = Boolean(projectId);

  const session = useAuthStore((state) => state.session);
  const existing = useProject(projectId ?? '', { enabled: isEditing });
  const guidelines = useCurrentGuidelines();
  const create = useCreateProject();
  const update = useUpdateProject();

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState<ProjectPriority>('Medium');
  const [guidelineId, setGuidelineId] = useState<string | null>(null);
  const [estimatedCost, setEstimatedCost] = useState('');
  const [actualCost, setActualCost] = useState('');
  const [deadlineDays, setDeadlineDays] = useState('');
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    if (!existing.data) return;

    setTitle(existing.data.title);
    setDescription(existing.data.description);
    setPriority(existing.data.priority);
    setGuidelineId(existing.data.linkedGuidelineId);
    setEstimatedCost(existing.data.estimatedCost ? String(existing.data.estimatedCost) : '');
    setActualCost(existing.data.actualCost ? String(existing.data.actualCost) : '');
  }, [existing.data]);

  async function submit() {
    setError(null);
    if (!session) return;

    const deadline =
      deadlineDays.trim().length > 0
        ? new Date(Date.now() + Number(deadlineDays) * 86_400_000).toISOString()
        : (existing.data?.deadline ?? null);

    try {
      if (isEditing && projectId) {
        await update.mutateAsync({
          id: projectId,
          input: {
            title: title.trim(),
            description: description.trim(),
            priority,
            ownerId: existing.data?.ownerId ?? session.userId,
            estimatedCost: parseDecimalInput(estimatedCost),
            actualCost: parseDecimalInput(actualCost),
            deadline,
          },
        });
      } else {
        await create.mutateAsync({
          title: title.trim(),
          description: description.trim(),
          priority,
          ownerId: session.userId,
          linkedGuidelineId: guidelineId,
          estimatedCost: parseDecimalInput(estimatedCost),
          deadline,
        });
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

  const canSubmit = title.trim().length > 0 && description.trim().length > 0;
  const saving = create.isPending || update.isPending;

  return (
    <Screen scroll>
      {error ? <ErrorBanner message={error.message} onDismiss={() => setError(null)} /> : null}

      <Card style={styles.first}>
        <Field
          label="Título"
          value={title}
          onChangeText={setTitle}
          placeholder="Piloto de manutenção preditiva na linha 3"
          required
          maxLength={200}
          error={error?.fieldError('Title')}
        />

        <Field
          label="Descrição"
          value={description}
          onChangeText={setDescription}
          placeholder="O que será entregue e como."
          multiline
          required
          maxLength={4000}
          error={error?.fieldError('Description')}
        />
      </Card>

      <SectionHeader title="Prioridade" />

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

      {!isEditing ? (
        <>
          <SectionHeader
            title="Diretriz estratégica"
            subtitle="Liga o projeto à estratégia e alimenta o painel executivo"
          />

          {guidelines.data && guidelines.data.length > 0 ? (
            <ChipRow>
              <Chip label="Nenhuma" selected={guidelineId === null} onPress={() => setGuidelineId(null)} />
              {guidelines.data.map((guideline) => (
                <Chip
                  key={guideline.id}
                  label={
                    guideline.title.length > 28 ? `${guideline.title.slice(0, 27)}…` : guideline.title
                  }
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
        </>
      ) : null}

      <SectionHeader title="Custos e prazo" />

      <Card>
        <View style={styles.row}>
          <View style={styles.flex}>
            <Field
              label="Custo estimado"
              value={estimatedCost}
              onChangeText={setEstimatedCost}
              placeholder="0"
              keyboardType="decimal-pad"
              error={error?.fieldError('EstimatedCost')}
            />
          </View>

          {isEditing ? (
            <View style={styles.flex}>
              <Field
                label="Custo real"
                value={actualCost}
                onChangeText={setActualCost}
                placeholder="0"
                keyboardType="decimal-pad"
                error={error?.fieldError('ActualCost')}
              />
            </View>
          ) : null}
        </View>

        <Field
          label="Prazo em dias"
          value={deadlineDays}
          onChangeText={setDeadlineDays}
          placeholder="90"
          keyboardType="number-pad"
          hint={
            isEditing
              ? 'Deixe em branco para manter o prazo atual.'
              : 'A partir de hoje. Deixe em branco para não definir prazo.'
          }
        />
      </Card>

      <View style={styles.actions}>
        <Button
          title={isEditing ? 'Salvar alterações' : 'Criar projeto'}
          onPress={submit}
          loading={saving}
          disabled={!canSubmit}
        />
        <Button title="Cancelar" onPress={() => navigation.goBack()} variant="ghost" />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  first: { marginTop: theme.spacing.lg },
  row: { flexDirection: 'row', gap: theme.spacing.md },
  actions: { gap: theme.spacing.sm, marginTop: theme.spacing.xl },
});

import React, { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { useProject, useProjectResult, useProjectTransition } from '../../api/queries';
import { useAuthStore } from '../../store/authStore';
import {
  Button,
  Card,
  Chip,
  ChipRow,
  Divider,
  Field,
  ProgressBar,
  Screen,
  SectionHeader,
  Txt,
} from '../../components/primitives';
import { ConfirmDialog, ErrorBanner, ErrorState, SkeletonList, SuccessBanner } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { DeadlineBadge, MoneyRow, ProjectStatusBadge, PriorityBadge, StatusBadge } from '../../components/domain';
import { projectStageLabel } from '../../i18n/labels';
import { formatCurrency, formatDate, formatDays, formatPercent } from '../../utils/format';
import { toApiError } from '../../api/errors';
import { theme } from '../../theme';
import type { ProjectStage } from '../../api/types';

type TransitionKind = 'start' | 'complete' | 'unblock' | 'block' | 'cancel';

const STAGES: ProjectStage[] = ['Discovery', 'Planning', 'Execution', 'Validation', 'Rollout'];
const PROGRESS_STEPS = [0, 10, 25, 40, 50, 60, 75, 90, 95];

export function ProjectDetailScreen() {
  const navigation = useNavigation<any>();
  const { projectId } = useRoute<RouteProp<{ p: { projectId: string } }, 'p'>>().params;

  const role = useAuthStore((state) => state.session?.role);
  const canManage = role === 'Manager';

  const query = useProject(projectId);
  const result = useProjectResult(projectId);
  const transition = useProjectTransition();
  const refreshControl = useRefreshControl(query);

  const [pending, setPending] = useState<TransitionKind | null>(null);
  const [reason, setReason] = useState('');
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

  const project = query.data;

  const daysToDeadline = project.deadline
    ? Math.floor((new Date(project.deadline).getTime() - Date.now()) / 86_400_000)
    : null;

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

  async function confirmTransition() {
    if (!pending) return;

    setError(null);

    try {
      if (pending === 'block' || pending === 'cancel') {
        await transition.mutateAsync({ id: projectId, action: pending, reason: reason.trim() });
      } else {
        await transition.mutateAsync({ id: projectId, action: pending });
      }

      setSuccess(SUCCESS_MESSAGES[pending]);
      setPending(null);
      setReason('');
    } catch (caught) {
      setError(toApiError(caught).message);
      setPending(null);
    }
  }

  return (
    <Screen scroll refreshControl={refreshControl}>
      {error ? <ErrorBanner message={error} onDismiss={() => setError(null)} /> : null}
      {success ? <SuccessBanner message={success} /> : null}

      <Card style={styles.first} accent={project.status === 'Blocked' ? theme.colors.status.blocked.text : undefined}>
        <View style={styles.badges}>
          <ProjectStatusBadge status={project.status} />
          <StatusBadge status="neutral" label={projectStageLabel[project.stage]} />
          <PriorityBadge priority={project.priority} kind="project" />
        </View>

        <Txt variant="heading" style={styles.title}>
          {project.title}
        </Txt>

        <Txt variant="body" color={theme.colors.text.secondary}>
          {project.description}
        </Txt>

        <View style={styles.progressBlock}>
          <View style={styles.progressHeader}>
            <Txt variant="label" color={theme.colors.text.secondary}>
              Progresso
            </Txt>
            <Txt variant="label">{formatPercent(project.progressPercentage)}</Txt>
          </View>
          <ProgressBar
            value={project.progressPercentage}
            color={project.status === 'Blocked' ? theme.colors.status.blocked.text : undefined}
          />
        </View>

        <Divider />

        <View style={styles.metaRow}>
          <Txt variant="caption" color={theme.colors.text.muted}>
            Responsável
          </Txt>
          <Txt variant="label">{project.ownerName}</Txt>
        </View>

        <View style={styles.metaRow}>
          <Txt variant="caption" color={theme.colors.text.muted}>
            Prazo
          </Txt>
          <View style={styles.deadlineCell}>
            <Txt variant="label">{formatDate(project.deadline)}</Txt>
            <DeadlineBadge
              daysToDeadline={daysToDeadline}
              isOverdue={project.isOverdue}
              isAtRisk={project.isAtRisk}
            />
          </View>
        </View>

        {project.startDate ? (
          <View style={styles.metaRow}>
            <Txt variant="caption" color={theme.colors.text.muted}>
              Início
            </Txt>
            <Txt variant="label">{formatDate(project.startDate)}</Txt>
          </View>
        ) : null}

        {project.completedAt ? (
          <View style={styles.metaRow}>
            <Txt variant="caption" color={theme.colors.text.muted}>
              Conclusão
            </Txt>
            <Txt variant="label">{formatDate(project.completedAt)}</Txt>
          </View>
        ) : null}
      </Card>

      {project.blockedReason ? (
        <Card style={styles.blockedCard}>
          <Txt variant="overline" color={theme.colors.status.blocked.text}>
            {`Bloqueado há ${formatDays(project.daysBlocked)}`}
          </Txt>
          <Txt variant="body" style={styles.paragraph}>
            {project.blockedReason}
          </Txt>
        </Card>
      ) : null}

      {project.cancelledReason ? (
        <Card style={styles.cancelledCard}>
          <Txt variant="overline" color={theme.colors.text.muted}>
            Motivo do cancelamento
          </Txt>
          <Txt variant="body" style={styles.paragraph}>
            {project.cancelledReason}
          </Txt>
        </Card>
      ) : null}

      {project.sourceIdeaTitle || project.linkedGuidelineTitle ? (
        <>
          <SectionHeader title="Origem e estratégia" />
          <Card>
            {project.linkedGuidelineTitle ? (
              <View style={styles.originRow}>
                <Txt variant="caption" color={theme.colors.text.muted}>
                  Diretriz
                </Txt>
                <StatusBadge status="strategy" label={project.linkedGuidelineTitle} />
              </View>
            ) : null}

            {project.sourceIdeaTitle ? (
              <View style={styles.originRow}>
                <Txt variant="caption" color={theme.colors.text.muted}>
                  Ideia de origem
                </Txt>
                <Txt variant="body" numberOfLines={2}>
                  {project.sourceIdeaTitle}
                </Txt>
              </View>
            ) : null}
          </Card>
        </>
      ) : null}

      <SectionHeader title="Custos e resultado" />

      <Card>
        <MoneyRow label="Custo estimado" value={project.estimatedCost} />
        <MoneyRow label="Custo real" value={project.actualCost} />

        {result.data?.actual ? (
          <>
            <Divider spacing={theme.spacing.sm} />
            <MoneyRow label="Valor realizado" value={result.data.actual.netValue} tone="positive" />
            {result.data.actual.roi !== null ? (
              <View style={styles.metaRow}>
                <Txt variant="body" color={theme.colors.text.secondary}>
                  ROI realizado
                </Txt>
                <Txt variant="bodyStrong" color={theme.colors.success}>
                  {formatPercent(result.data.actual.roi, 1)}
                </Txt>
              </View>
            ) : null}
          </>
        ) : null}

        <Button
          title={result.data ? 'Ver e editar resultado' : 'Registrar resultado'}
          onPress={() => navigation.navigate('ProjectResult', { projectId })}
          variant="secondary"
          style={styles.blockGap}
        />
      </Card>

      {canManage && project.status !== 'Completed' && project.status !== 'Cancelled' ? (
        <>
          <SectionHeader title="Etapa de execução" subtitle="Independente da situação de governança" />

          <ChipRow>
            {STAGES.map((value) => (
              <Chip
                key={value}
                label={projectStageLabel[value]}
                selected={project.stage === value}
                onPress={() =>
                  run(
                    () => transition.mutateAsync({ id: projectId, action: 'stage', stage: value }),
                    `Etapa alterada para ${projectStageLabel[value].toLowerCase()}.`
                  )
                }
              />
            ))}
          </ChipRow>

          {project.status === 'InProgress' || project.status === 'Blocked' ? (
            <>
              <SectionHeader
                title="Atualizar progresso"
                subtitle="100% só é atingido concluindo o projeto"
              />

              <ChipRow>
                {PROGRESS_STEPS.map((value) => (
                  <Chip
                    key={value}
                    label={`${value}%`}
                    selected={project.progressPercentage === value}
                    onPress={() =>
                      run(
                        () =>
                          transition.mutateAsync({
                            id: projectId,
                            action: 'progress',
                            progressPercentage: value,
                          }),
                        `Progresso atualizado para ${value}%.`
                      )
                    }
                  />
                ))}
              </ChipRow>
            </>
          ) : null}
        </>
      ) : null}

      <SectionHeader title="Governança" />

      <Button
        title="Ver linha do tempo e auditoria"
        onPress={() => navigation.navigate('ProjectTimeline', { projectId })}
        variant="secondary"
      />

      {canManage ? (
        <View style={styles.actions}>
          {project.status === 'Planned' ? (
            <Button title="Iniciar projeto" onPress={() => setPending('start')} />
          ) : null}

          {project.status === 'InProgress' ? (
            <>
              <Button title="Concluir projeto" onPress={() => setPending('complete')} />
              <Button title="Bloquear" onPress={() => setPending('block')} variant="secondary" />
            </>
          ) : null}

          {project.status === 'Blocked' ? (
            <Button title="Desbloquear" onPress={() => setPending('unblock')} />
          ) : null}

          {project.status === 'Planned' ? (
            <Button title="Bloquear" onPress={() => setPending('block')} variant="secondary" />
          ) : null}

          {project.status === 'InProgress' || project.status === 'Blocked' ? (
            <Button title="Cancelar projeto" onPress={() => setPending('cancel')} variant="danger" />
          ) : null}

          {project.status !== 'Completed' && project.status !== 'Cancelled' ? (
            <Button
              title="Editar projeto"
              onPress={() => navigation.navigate('ProjectForm', { projectId })}
              variant="ghost"
            />
          ) : null}
        </View>
      ) : null}

      <ConfirmDialog
        visible={pending !== null}
        title={pending ? DIALOG_TITLES[pending] : ''}
        message={pending ? DIALOG_MESSAGES[pending] : ''}
        confirmLabel={pending ? DIALOG_CONFIRM[pending] : 'Confirmar'}
        destructive={pending === 'cancel' || pending === 'block'}
        loading={transition.isPending}
        onConfirm={confirmTransition}
        onCancel={() => {
          setPending(null);
          setReason('');
        }}
      >
        {pending === 'block' || pending === 'cancel' ? (
          <Field
            label="Motivo"
            value={reason}
            onChangeText={setReason}
            placeholder={
              pending === 'block'
                ? 'O que está impedindo o projeto de andar?'
                : 'Por que o projeto não continua?'
            }
            multiline
            required
            maxLength={2000}
          />
        ) : null}
      </ConfirmDialog>
    </Screen>
  );
}

const DIALOG_TITLES: Record<TransitionKind, string> = {
  start: 'Iniciar projeto',
  complete: 'Concluir projeto',
  unblock: 'Desbloquear projeto',
  block: 'Bloquear projeto',
  cancel: 'Cancelar projeto',
};

const DIALOG_MESSAGES: Record<TransitionKind, string> = {
  start: 'O projeto passa a Em execução e a contagem de prazo começa a valer.',
  complete: 'A conclusão marca o progresso em 100% e habilita o registro de resultados reais.',
  unblock: 'O projeto volta a Em execução e o tempo bloqueado é registrado na auditoria.',
  block: 'O bloqueio aparece no painel executivo e a liderança é notificada. O motivo é obrigatório.',
  cancel: 'O cancelamento é definitivo e o projeto não pode mais ser editado. O motivo é obrigatório.',
};

const DIALOG_CONFIRM: Record<TransitionKind, string> = {
  start: 'Iniciar',
  complete: 'Concluir',
  unblock: 'Desbloquear',
  block: 'Bloquear',
  cancel: 'Cancelar projeto',
};

const SUCCESS_MESSAGES: Record<TransitionKind, string> = {
  start: 'Projeto iniciado.',
  complete: 'Projeto concluído. Registre os resultados realizados.',
  unblock: 'Projeto desbloqueado.',
  block: 'Projeto bloqueado. A liderança foi notificada.',
  cancel: 'Projeto cancelado.',
};

const styles = StyleSheet.create({
  first: { marginTop: theme.spacing.lg },
  badges: { flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm, marginBottom: theme.spacing.md },
  title: { marginBottom: theme.spacing.xs },
  paragraph: { marginTop: theme.spacing.xs },
  blockGap: { marginTop: theme.spacing.lg },
  progressBlock: { marginTop: theme.spacing.lg },
  progressHeader: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: theme.spacing.xs },
  metaRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: theme.spacing.xs,
    gap: theme.spacing.md,
  },
  deadlineCell: { alignItems: 'flex-end', gap: theme.spacing.xs },
  blockedCard: { marginTop: theme.spacing.md, backgroundColor: theme.colors.status.blocked.bg },
  cancelledCard: { marginTop: theme.spacing.md, backgroundColor: theme.colors.surface.sunken },
  originRow: { paddingVertical: theme.spacing.sm, gap: theme.spacing.xs },
  actions: { gap: theme.spacing.sm, marginTop: theme.spacing.md },
});

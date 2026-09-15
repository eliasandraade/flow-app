import type {
  GuidelineCategory,
  IdeaPriority,
  IdeaStatus,
  ProjectPriority,
  ProjectStage,
  ProjectStatus,
  UserRole,
} from '../api/types';

/**
 * Portuguese labels for the domain vocabulary.
 *
 * The API speaks the domain language — stable English identifiers, shared by every client;
 * the interface speaks the user's. Keeping the translation in one table means a screen
 * never shows a raw enum by accident.
 */

export const ideaStatusLabel: Record<IdeaStatus, string> = {
  Draft: 'Rascunho',
  UnderReview: 'Em análise',
  Approved: 'Aprovada',
  Rejected: 'Não aprovada',
};

export const ideaPriorityLabel: Record<IdeaPriority, string> = {
  Low: 'Baixa',
  Medium: 'Média',
  High: 'Alta',
};

export const projectStatusLabel: Record<ProjectStatus, string> = {
  Planned: 'Planejado',
  InProgress: 'Em execução',
  Blocked: 'Bloqueado',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
};

export const projectStageLabel: Record<ProjectStage, string> = {
  Discovery: 'Descoberta',
  Planning: 'Planejamento',
  Execution: 'Execução',
  Validation: 'Validação',
  Rollout: 'Implantação',
};

export const projectPriorityLabel: Record<ProjectPriority, string> = {
  Low: 'Baixa',
  Medium: 'Média',
  High: 'Alta',
  Critical: 'Crítica',
};

export const guidelineCategoryLabel: Record<GuidelineCategory, string> = {
  OperationalEfficiency: 'Eficiência operacional',
  CostReduction: 'Redução de custo',
  CustomerExperience: 'Experiência do cliente',
  Quality: 'Qualidade',
  Safety: 'Segurança',
  Sustainability: 'Sustentabilidade',
  DigitalTransformation: 'Transformação digital',
  People: 'Pessoas',
};

export const roleLabel: Record<UserRole, string> = {
  Operator: 'Operador',
  Manager: 'Gestor',
  Leadership: 'Liderança',
};

/** Audit actions, as they appear on the project timeline. */
export const auditActionLabel: Record<string, string> = {
  Created: 'Criado',
  Updated: 'Atualizado',
  Deleted: 'Excluído',
  Started: 'Iniciado',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
  Blocked: 'Bloqueado',
  Unblocked: 'Desbloqueado',
  ProgressUpdated: 'Progresso atualizado',
  StageChanged: 'Etapa alterada',
  Submitted: 'Enviada para análise',
  Approved: 'Aprovada',
  Rejected: 'Não aprovada',
  Commented: 'Comentada',
  PrioritySet: 'Prioridade definida',
  ScoreSet: 'Nota definida',
  FlowScoreSet: 'FlowScore calculado',
  ResultRecorded: 'Resultado registrado',
  Closed: 'Encerrada',
};

export const notificationTypeLabel: Record<string, string> = {
  IdeaSubmitted: 'Ideia enviada',
  IdeaCommented: 'Novo comentário',
  IdeaApproved: 'Ideia aprovada',
  IdeaRejected: 'Ideia não aprovada',
  IdeaAwaitingReview: 'Aguardando análise',
  ProjectCreated: 'Projeto criado',
  ProjectBlocked: 'Projeto bloqueado',
  ProjectUnblocked: 'Projeto desbloqueado',
  ProjectCompleted: 'Projeto concluído',
  ProjectCancelled: 'Projeto cancelado',
  ProjectDeadlineAtRisk: 'Prazo em risco',
  ProjectProgressUpdated: 'Progresso atualizado',
  ResultRecorded: 'Resultado registrado',
};

/** FlowScore dimensions, named as the manager sees them. */
export const flowScoreDimensionLabel = {
  strategicAlignment: 'Alinhamento estratégico',
  impact: 'Impacto',
  feasibility: 'Viabilidade',
  urgency: 'Urgência',
  confidence: 'Confiança',
} as const;

export function labelFor(map: Record<string, string>, key: string): string {
  return map[key] ?? key;
}

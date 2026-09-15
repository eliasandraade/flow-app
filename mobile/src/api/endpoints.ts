import { api } from './client';
import type * as T from './types';

/**
 * Typed surface of the Flow API.
 *
 * Every call lives here so no screen ever builds a URL by hand, and a contract change
 * breaks at compile time in one file rather than at runtime in ten.
 */

function query(params: Record<string, string | number | boolean | null | undefined>): string {
  const entries = Object.entries(params).filter(
    ([, value]) => value !== null && value !== undefined && value !== ''
  );

  if (entries.length === 0) return '';
  return `?${entries.map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`).join('&')}`;
}

export const authApi = {
  login: (email: string, password: string) =>
    api.post<T.AuthResult>('/auth/login', { email, password }),

  register: (name: string, email: string, password: string) =>
    api.post<T.AuthResult>('/auth/register', { name, email, password }),

  logout: (refreshToken: string) => api.post<void>('/auth/logout', { refreshToken }),
};

export const guidelinesApi = {
  list: (
    filters: { category?: T.GuidelineCategory; campaign?: string; currentOnly?: boolean } = {},
    signal?: AbortSignal
  ) => api.get<T.Guideline[]>(`/guidelines${query(filters)}`, signal),

  current: (signal?: AbortSignal) => api.get<T.Guideline[]>('/guidelines/current', signal),

  byId: (id: string, signal?: AbortSignal) => api.get<T.Guideline>(`/guidelines/${id}`, signal),

  history: (id: string, signal?: AbortSignal) =>
    api.get<T.GuidelineHistoryEntry[]>(`/guidelines/${id}/history`, signal),

  create: (input: T.GuidelineInput) => api.post<T.Guideline>('/guidelines', input),

  update: (id: string, input: T.GuidelineInput) => api.put<void>(`/guidelines/${id}`, input),

  close: (id: string) => api.post<void>(`/guidelines/${id}/close`),

  remove: (id: string) => api.delete<void>(`/guidelines/${id}`),
};

export const ideasApi = {
  list: (
    filters: {
      submittedById?: string;
      status?: T.IdeaStatus;
      priority?: T.IdeaPriority;
      linkedGuidelineId?: string;
      minScore?: number;
      sortBy?: 'score' | 'flowScore' | 'priority';
      skip?: number;
      take?: number;
    } = {},
    signal?: AbortSignal
  ) => api.get<T.IdeaSummary[]>(`/ideas${query(filters)}`, signal),

  byId: (id: string, signal?: AbortSignal) => api.get<T.IdeaDetail>(`/ideas/${id}`, signal),

  create: (input: T.IdeaInput) => api.post<T.IdeaSummary>('/ideas', input),

  update: (id: string, input: T.IdeaInput) => api.put<void>(`/ideas/${id}`, input),

  remove: (id: string) => api.delete<void>(`/ideas/${id}`),

  submit: (id: string) => api.post<void>(`/ideas/${id}/submit`),

  approve: (id: string, managerComment: string | null) =>
    api.post<void>(`/ideas/${id}/approve`, { managerComment }),

  reject: (id: string, managerComment: string) =>
    api.post<void>(`/ideas/${id}/reject`, { managerComment }),

  setPriority: (id: string, priority: T.IdeaPriority) =>
    api.patch<void>(`/ideas/${id}/priority`, { priority }),

  setScore: (id: string, score: number) => api.patch<void>(`/ideas/${id}/score`, { score }),

  setFlowScore: (
    id: string,
    components: {
      strategicAlignment: number | null;
      impact: number;
      feasibility: number;
      urgency: number;
      confidence: number;
    }
  ) => api.put<T.FlowScore>(`/ideas/${id}/flow-score`, components),

  comments: (id: string, signal?: AbortSignal) =>
    api.get<T.IdeaComment[]>(`/ideas/${id}/comments`, signal),

  addComment: (id: string, body: string) =>
    api.post<T.IdeaComment>(`/ideas/${id}/comments`, { body }),

  compare: (ideaIds: string[]) => api.post<T.IdeaComparison>('/ideas/compare', { ideaIds }),
};

export const projectsApi = {
  list: (
    filters: {
      ownerId?: string;
      status?: T.ProjectStatus;
      stage?: T.ProjectStage;
      linkedGuidelineId?: string;
      skip?: number;
      take?: number;
    } = {},
    signal?: AbortSignal
  ) => api.get<T.ProjectSummary[]>(`/projects${query(filters)}`, signal),

  byId: (id: string, signal?: AbortSignal) => api.get<T.ProjectDetail>(`/projects/${id}`, signal),

  create: (input: T.CreateProjectInput) => api.post<T.ProjectSummary>('/projects', input),

  convertFromIdea: (ideaId: string, input: T.ConvertIdeaInput) =>
    api.post<T.ProjectSummary>(`/ideas/${ideaId}/convert`, input),

  update: (
    id: string,
    input: {
      title: string;
      description: string;
      priority: T.ProjectPriority;
      ownerId: string;
      estimatedCost: number | null;
      actualCost: number | null;
      deadline: string | null;
    }
  ) => api.put<void>(`/projects/${id}`, input),

  updateProgress: (id: string, progressPercentage: number) =>
    api.patch<void>(`/projects/${id}/progress`, { progressPercentage }),

  advanceStage: (id: string, stage: T.ProjectStage) =>
    api.patch<void>(`/projects/${id}/stage`, { stage }),

  start: (id: string) => api.post<void>(`/projects/${id}/start`),
  complete: (id: string) => api.post<void>(`/projects/${id}/complete`),
  cancel: (id: string, reason: string) => api.post<void>(`/projects/${id}/cancel`, { reason }),
  block: (id: string, reason: string) => api.post<void>(`/projects/${id}/block`, { reason }),
  unblock: (id: string) => api.post<void>(`/projects/${id}/unblock`),

  timeline: (id: string, signal?: AbortSignal) =>
    api.get<T.TimelineEntry[]>(`/projects/${id}/timeline`, signal),

  snapshots: (id: string, signal?: AbortSignal) =>
    api.get<T.ProjectSnapshot[]>(`/projects/${id}/snapshots`, signal),
};

export const resultsApi = {
  byProject: (projectId: string, signal?: AbortSignal) =>
    api.get<T.ProjectResult>(`/projects/${projectId}/result`, signal),

  record: (projectId: string, input: T.RecordResultInput) =>
    api.put<T.ProjectResult>(`/projects/${projectId}/result`, input),
};

export const dashboardApi = {
  summary: (signal?: AbortSignal) => api.get<T.DashboardSummary>('/dashboard/summary', signal),

  project: (id: string, signal?: AbortSignal) =>
    api.get<T.ProjectDashboard>(`/dashboard/projects/${id}`, signal),

  strategy: (id: string, signal?: AbortSignal) =>
    api.get<T.StrategyDashboard>(`/dashboard/strategies/${id}`, signal),

  insights: () => api.post<T.ExecutiveInsightResponse>('/dashboard/insights'),
};

export const notificationsApi = {
  list: (filters: { unreadOnly?: boolean; skip?: number; take?: number } = {}, signal?: AbortSignal) =>
    api.get<T.NotificationPage>(`/notifications${query(filters)}`, signal),

  markRead: (id: string) => api.post<void>(`/notifications/${id}/read`),

  markAllRead: () => api.post<void>('/notifications/read-all'),
};

export const usersApi = {
  myPoints: (signal?: AbortSignal) => api.get<T.PointsSummary>('/users/me/points', signal),

  myLedger: (signal?: AbortSignal) =>
    api.get<T.PointsLedgerEntry[]>('/users/me/points/ledger', signal),

  points: (userId: string, signal?: AbortSignal) =>
    api.get<T.PointsSummary>(`/users/${userId}/points`, signal),
};

export const assistantApi = {
  compareIdeas: (ideaIds: string[], question: string | null) =>
    api.post<T.AssistantComparison>('/assistant/compare-ideas', { ideaIds, question }),

  projectDraft: (ideaId: string) =>
    api.post<T.ProjectDraftResponse>(`/assistant/ideas/${ideaId}/project-draft`),
};

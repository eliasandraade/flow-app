import {
  QueryClient,
  useMutation,
  useQuery,
  useQueryClient,
  type UseQueryOptions,
} from '@tanstack/react-query';
import { ApiError } from './errors';
import {
  assistantApi,
  dashboardApi,
  guidelinesApi,
  ideasApi,
  notificationsApi,
  projectsApi,
  resultsApi,
  usersApi,
} from './endpoints';
import type * as T from './types';

/**
 * Query keys, in one place so invalidation cannot drift from the keys it is meant to hit.
 */
export const keys = {
  guidelines: {
    all: ['guidelines'] as const,
    list: (filters?: unknown) => ['guidelines', 'list', filters ?? {}] as const,
    current: () => ['guidelines', 'current'] as const,
    byId: (id: string) => ['guidelines', 'detail', id] as const,
    history: (id: string) => ['guidelines', 'history', id] as const,
  },
  ideas: {
    all: ['ideas'] as const,
    list: (filters?: unknown) => ['ideas', 'list', filters ?? {}] as const,
    byId: (id: string) => ['ideas', 'detail', id] as const,
    comments: (id: string) => ['ideas', 'comments', id] as const,
  },
  projects: {
    all: ['projects'] as const,
    list: (filters?: unknown) => ['projects', 'list', filters ?? {}] as const,
    byId: (id: string) => ['projects', 'detail', id] as const,
    timeline: (id: string) => ['projects', 'timeline', id] as const,
    snapshots: (id: string) => ['projects', 'snapshots', id] as const,
  },
  results: {
    byProject: (projectId: string) => ['results', projectId] as const,
  },
  dashboard: {
    all: ['dashboard'] as const,
    summary: () => ['dashboard', 'summary'] as const,
    project: (id: string) => ['dashboard', 'project', id] as const,
    strategy: (id: string) => ['dashboard', 'strategy', id] as const,
  },
  notifications: {
    all: ['notifications'] as const,
    list: (unreadOnly: boolean) => ['notifications', 'list', unreadOnly] as const,
  },
  points: {
    mine: () => ['points', 'me'] as const,
    ledger: () => ['points', 'ledger'] as const,
  },
};

/**
 * Retries only what is genuinely worth retrying.
 *
 * A 403 or a 409 will never succeed on a second attempt; retrying them just delays the
 * error the user needs to see. Network blips and 5xx are a different story.
 */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (!(error instanceof ApiError)) return false;
  if (!error.retryable) return false;
  return failureCount < 2;
}

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: shouldRetry,
        retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 8000),
        staleTime: 30_000,
        gcTime: 5 * 60_000,
        refetchOnWindowFocus: false,
      },
      mutations: {
        // Mutations are never retried automatically: most are not idempotent and a silent
        // second attempt could approve an idea twice.
        retry: false,
      },
    },
  });
}

type Options<TData> = Omit<UseQueryOptions<TData, ApiError>, 'queryKey' | 'queryFn'>;

// ─── Strategy ───────────────────────────────────────────────────────────────

export function useGuidelines(
  filters: { category?: T.GuidelineCategory; campaign?: string; currentOnly?: boolean } = {},
  options?: Options<T.Guideline[]>
) {
  return useQuery<T.Guideline[], ApiError>({
    queryKey: keys.guidelines.list(filters),
    queryFn: ({ signal }) => guidelinesApi.list(filters, signal),
    ...options,
  });
}

export function useCurrentGuidelines(options?: Options<T.Guideline[]>) {
  return useQuery<T.Guideline[], ApiError>({
    queryKey: keys.guidelines.current(),
    queryFn: ({ signal }) => guidelinesApi.current(signal),
    ...options,
  });
}

export function useGuideline(id: string, options?: Options<T.Guideline>) {
  return useQuery<T.Guideline, ApiError>({
    queryKey: keys.guidelines.byId(id),
    queryFn: ({ signal }) => guidelinesApi.byId(id, signal),
    ...options,
  });
}

export function useGuidelineHistory(id: string, options?: Options<T.GuidelineHistoryEntry[]>) {
  return useQuery<T.GuidelineHistoryEntry[], ApiError>({
    queryKey: keys.guidelines.history(id),
    queryFn: ({ signal }) => guidelinesApi.history(id, signal),
    ...options,
  });
}

export function useSaveGuideline() {
  const client = useQueryClient();

  return useMutation<T.Guideline | void, ApiError, { id?: string; input: T.GuidelineInput }>({
    mutationFn: ({ id, input }) =>
      id ? guidelinesApi.update(id, input) : guidelinesApi.create(input),
    onSuccess: (_, { id }) => {
      client.invalidateQueries({ queryKey: keys.guidelines.all });
      // A strategy change moves the numbers the dashboard groups by.
      client.invalidateQueries({ queryKey: keys.dashboard.all });
      if (id) client.invalidateQueries({ queryKey: keys.guidelines.history(id) });
    },
  });
}

export function useCloseGuideline() {
  const client = useQueryClient();

  return useMutation<void, ApiError, string>({
    mutationFn: (id) => guidelinesApi.close(id),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: keys.guidelines.all });
      client.invalidateQueries({ queryKey: keys.dashboard.all });
    },
  });
}

export function useDeleteGuideline() {
  const client = useQueryClient();

  return useMutation<void, ApiError, string>({
    mutationFn: (id) => guidelinesApi.remove(id),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: keys.guidelines.all });
      client.invalidateQueries({ queryKey: keys.dashboard.all });
    },
  });
}

// ─── Ideas ──────────────────────────────────────────────────────────────────

export function useIdeas(
  filters: Parameters<typeof ideasApi.list>[0] = {},
  options?: Options<T.IdeaSummary[]>
) {
  return useQuery<T.IdeaSummary[], ApiError>({
    queryKey: keys.ideas.list(filters),
    queryFn: ({ signal }) => ideasApi.list(filters, signal),
    ...options,
  });
}

export function useIdea(id: string, options?: Options<T.IdeaDetail>) {
  return useQuery<T.IdeaDetail, ApiError>({
    queryKey: keys.ideas.byId(id),
    queryFn: ({ signal }) => ideasApi.byId(id, signal),
    ...options,
  });
}

export function useIdeaComments(id: string, options?: Options<T.IdeaComment[]>) {
  return useQuery<T.IdeaComment[], ApiError>({
    queryKey: keys.ideas.comments(id),
    queryFn: ({ signal }) => ideasApi.comments(id, signal),
    ...options,
  });
}

/** Shared invalidation for anything that changes an idea. */
function invalidateIdea(client: QueryClient, id?: string) {
  client.invalidateQueries({ queryKey: keys.ideas.all });
  client.invalidateQueries({ queryKey: keys.dashboard.all });
  client.invalidateQueries({ queryKey: keys.notifications.all });
  if (id) client.invalidateQueries({ queryKey: keys.ideas.byId(id) });
}

export function useCreateIdea() {
  const client = useQueryClient();

  return useMutation<T.IdeaSummary, ApiError, T.IdeaInput>({
    mutationFn: (input) => ideasApi.create(input),
    onSuccess: () => invalidateIdea(client),
  });
}

export function useUpdateIdea() {
  const client = useQueryClient();

  return useMutation<void, ApiError, { id: string; input: T.IdeaInput }>({
    mutationFn: ({ id, input }) => ideasApi.update(id, input),
    onSuccess: (_, { id }) => invalidateIdea(client, id),
  });
}

export function useDeleteIdea() {
  const client = useQueryClient();

  return useMutation<void, ApiError, string>({
    mutationFn: (id) => ideasApi.remove(id),
    onSuccess: () => invalidateIdea(client),
  });
}

export function useSubmitIdea() {
  const client = useQueryClient();

  return useMutation<void, ApiError, string>({
    mutationFn: (id) => ideasApi.submit(id),
    onSuccess: (_, id) => invalidateIdea(client, id),
  });
}

export function useDecideIdea() {
  const client = useQueryClient();

  return useMutation<
    void,
    ApiError,
    { id: string; decision: 'approve' | 'reject'; comment: string }
  >({
    mutationFn: ({ id, decision, comment }) =>
      decision === 'approve' ? ideasApi.approve(id, comment || null) : ideasApi.reject(id, comment),
    onSuccess: (_, { id }) => {
      invalidateIdea(client, id);
      client.invalidateQueries({ queryKey: keys.points.mine() });
    },
  });
}

export function useSetIdeaPriority() {
  const client = useQueryClient();

  return useMutation<void, ApiError, { id: string; priority: T.IdeaPriority }>({
    mutationFn: ({ id, priority }) => ideasApi.setPriority(id, priority),
    onSuccess: (_, { id }) => invalidateIdea(client, id),
  });
}

export function useSetIdeaScore() {
  const client = useQueryClient();

  return useMutation<void, ApiError, { id: string; score: number }>({
    mutationFn: ({ id, score }) => ideasApi.setScore(id, score),
    onSuccess: (_, { id }) => invalidateIdea(client, id),
  });
}

export function useSetFlowScore() {
  const client = useQueryClient();

  return useMutation<
    T.FlowScore,
    ApiError,
    { id: string; components: Parameters<typeof ideasApi.setFlowScore>[1] }
  >({
    mutationFn: ({ id, components }) => ideasApi.setFlowScore(id, components),
    onSuccess: (_, { id }) => invalidateIdea(client, id),
  });
}

export function useAddIdeaComment() {
  const client = useQueryClient();

  return useMutation<T.IdeaComment, ApiError, { id: string; body: string }>({
    mutationFn: ({ id, body }) => ideasApi.addComment(id, body),
    onSuccess: (_, { id }) => {
      client.invalidateQueries({ queryKey: keys.ideas.comments(id) });
      client.invalidateQueries({ queryKey: keys.notifications.all });
    },
  });
}

export function useCompareIdeas() {
  return useMutation<T.IdeaComparison, ApiError, string[]>({
    mutationFn: (ideaIds) => ideasApi.compare(ideaIds),
  });
}

// ─── Projects ───────────────────────────────────────────────────────────────

export function useProjects(
  filters: Parameters<typeof projectsApi.list>[0] = {},
  options?: Options<T.ProjectSummary[]>
) {
  return useQuery<T.ProjectSummary[], ApiError>({
    queryKey: keys.projects.list(filters),
    queryFn: ({ signal }) => projectsApi.list(filters, signal),
    ...options,
  });
}

export function useProject(id: string, options?: Options<T.ProjectDetail>) {
  return useQuery<T.ProjectDetail, ApiError>({
    queryKey: keys.projects.byId(id),
    queryFn: ({ signal }) => projectsApi.byId(id, signal),
    ...options,
  });
}

export function useProjectTimeline(id: string, options?: Options<T.TimelineEntry[]>) {
  return useQuery<T.TimelineEntry[], ApiError>({
    queryKey: keys.projects.timeline(id),
    queryFn: ({ signal }) => projectsApi.timeline(id, signal),
    ...options,
  });
}

export function useProjectSnapshots(id: string, options?: Options<T.ProjectSnapshot[]>) {
  return useQuery<T.ProjectSnapshot[], ApiError>({
    queryKey: keys.projects.snapshots(id),
    queryFn: ({ signal }) => projectsApi.snapshots(id, signal),
    ...options,
  });
}

/**
 * Every project transition writes an audit entry and a snapshot, so the timeline and the
 * snapshot list are invalidated alongside the project itself.
 */
function invalidateProject(client: QueryClient, id?: string) {
  client.invalidateQueries({ queryKey: keys.projects.all });
  client.invalidateQueries({ queryKey: keys.dashboard.all });
  client.invalidateQueries({ queryKey: keys.notifications.all });

  if (id) {
    client.invalidateQueries({ queryKey: keys.projects.byId(id) });
    client.invalidateQueries({ queryKey: keys.projects.timeline(id) });
    client.invalidateQueries({ queryKey: keys.projects.snapshots(id) });
  }
}

export function useCreateProject() {
  const client = useQueryClient();

  return useMutation<T.ProjectSummary, ApiError, T.CreateProjectInput>({
    mutationFn: (input) => projectsApi.create(input),
    onSuccess: () => invalidateProject(client),
  });
}

export function useConvertIdea() {
  const client = useQueryClient();

  return useMutation<T.ProjectSummary, ApiError, { ideaId: string; input: T.ConvertIdeaInput }>({
    mutationFn: ({ ideaId, input }) => projectsApi.convertFromIdea(ideaId, input),
    onSuccess: (_, { ideaId }) => {
      invalidateProject(client);
      invalidateIdea(client, ideaId);
    },
  });
}

export function useUpdateProject() {
  const client = useQueryClient();

  return useMutation<
    void,
    ApiError,
    { id: string; input: Parameters<typeof projectsApi.update>[1] }
  >({
    mutationFn: ({ id, input }) => projectsApi.update(id, input),
    onSuccess: (_, { id }) => invalidateProject(client, id),
  });
}

export function useProjectTransition() {
  const client = useQueryClient();

  return useMutation<
    void,
    ApiError,
    | { id: string; action: 'start' | 'complete' | 'unblock' }
    | { id: string; action: 'block' | 'cancel'; reason: string }
    | { id: string; action: 'progress'; progressPercentage: number }
    | { id: string; action: 'stage'; stage: T.ProjectStage }
  >({
    mutationFn: (params) => {
      switch (params.action) {
        case 'start':
          return projectsApi.start(params.id);
        case 'complete':
          return projectsApi.complete(params.id);
        case 'unblock':
          return projectsApi.unblock(params.id);
        case 'block':
          return projectsApi.block(params.id, params.reason);
        case 'cancel':
          return projectsApi.cancel(params.id, params.reason);
        case 'progress':
          return projectsApi.updateProgress(params.id, params.progressPercentage);
        case 'stage':
          return projectsApi.advanceStage(params.id, params.stage);
      }
    },
    onSuccess: (_, params) => invalidateProject(client, params.id),
  });
}

// ─── Results ────────────────────────────────────────────────────────────────

export function useProjectResult(projectId: string, options?: Options<T.ProjectResult>) {
  return useQuery<T.ProjectResult, ApiError>({
    queryKey: keys.results.byProject(projectId),
    queryFn: ({ signal }) => resultsApi.byProject(projectId, signal),
    // A project with no result yet is an expected state, not a failure to retry.
    retry: false,
    ...options,
  });
}

export function useRecordResult() {
  const client = useQueryClient();

  return useMutation<T.ProjectResult, ApiError, { projectId: string; input: T.RecordResultInput }>({
    mutationFn: ({ projectId, input }) => resultsApi.record(projectId, input),
    onSuccess: (_, { projectId }) => {
      client.invalidateQueries({ queryKey: keys.results.byProject(projectId) });
      client.invalidateQueries({ queryKey: keys.dashboard.all });
      client.invalidateQueries({ queryKey: keys.notifications.all });
    },
  });
}

// ─── Dashboard ──────────────────────────────────────────────────────────────

export function useDashboardSummary(options?: Options<T.DashboardSummary>) {
  return useQuery<T.DashboardSummary, ApiError>({
    queryKey: keys.dashboard.summary(),
    queryFn: ({ signal }) => dashboardApi.summary(signal),
    staleTime: 60_000,
    ...options,
  });
}

export function useProjectDashboard(id: string, options?: Options<T.ProjectDashboard>) {
  return useQuery<T.ProjectDashboard, ApiError>({
    queryKey: keys.dashboard.project(id),
    queryFn: ({ signal }) => dashboardApi.project(id, signal),
    ...options,
  });
}

export function useStrategyDashboard(id: string, options?: Options<T.StrategyDashboard>) {
  return useQuery<T.StrategyDashboard, ApiError>({
    queryKey: keys.dashboard.strategy(id),
    queryFn: ({ signal }) => dashboardApi.strategy(id, signal),
    ...options,
  });
}

export function useExecutiveInsights() {
  return useMutation<T.ExecutiveInsightResponse, ApiError, void>({
    mutationFn: () => dashboardApi.insights(),
  });
}

// ─── Notifications ──────────────────────────────────────────────────────────

export function useNotifications(unreadOnly = false, options?: Options<T.NotificationPage>) {
  return useQuery<T.NotificationPage, ApiError>({
    queryKey: keys.notifications.list(unreadOnly),
    queryFn: ({ signal }) => notificationsApi.list({ unreadOnly }, signal),
    staleTime: 15_000,
    ...options,
  });
}

export function useMarkNotificationRead() {
  const client = useQueryClient();

  return useMutation<void, ApiError, string>({
    mutationFn: (id) => notificationsApi.markRead(id),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.notifications.all }),
  });
}

export function useMarkAllNotificationsRead() {
  const client = useQueryClient();

  return useMutation<void, ApiError, void>({
    mutationFn: () => notificationsApi.markAllRead(),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.notifications.all }),
  });
}

// ─── Gamification ───────────────────────────────────────────────────────────

export function useMyPoints(options?: Options<T.PointsSummary>) {
  return useQuery<T.PointsSummary, ApiError>({
    queryKey: keys.points.mine(),
    queryFn: ({ signal }) => usersApi.myPoints(signal),
    ...options,
  });
}

export function useMyPointsLedger(options?: Options<T.PointsLedgerEntry[]>) {
  return useQuery<T.PointsLedgerEntry[], ApiError>({
    queryKey: keys.points.ledger(),
    queryFn: ({ signal }) => usersApi.myLedger(signal),
    ...options,
  });
}

// ─── Assistant ──────────────────────────────────────────────────────────────

export function useAssistantComparison() {
  return useMutation<T.AssistantComparison, ApiError, { ideaIds: string[]; question: string | null }>(
    {
      mutationFn: ({ ideaIds, question }) => assistantApi.compareIdeas(ideaIds, question),
    }
  );
}

export function useProjectDraft() {
  return useMutation<T.ProjectDraftResponse, ApiError, string>({
    mutationFn: (ideaId) => assistantApi.projectDraft(ideaId),
  });
}

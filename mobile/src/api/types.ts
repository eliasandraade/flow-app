/**
 * Wire contracts for the Flow API. These mirror the DTOs returned by the backend and are
 * the single source of truth for every screen.
 */

export type UserRole = 'Operator' | 'Manager' | 'Leadership';

export type IdeaStatus = 'Draft' | 'UnderReview' | 'Approved' | 'Rejected';
export type IdeaPriority = 'Low' | 'Medium' | 'High';

export type ProjectStatus =
  | 'Planned'
  | 'InProgress'
  | 'Blocked'
  | 'Completed'
  | 'Cancelled';

export type ProjectStage =
  | 'Discovery'
  | 'Planning'
  | 'Execution'
  | 'Validation'
  | 'Rollout';

export type ProjectPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export type GuidelineCategory =
  | 'OperationalEfficiency'
  | 'CostReduction'
  | 'CustomerExperience'
  | 'Quality'
  | 'Safety'
  | 'Sustainability'
  | 'DigitalTransformation'
  | 'People';

// ─── Auth ───────────────────────────────────────────────────────────────────

export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  userId: string;
  name: string;
  email: string;
  role: UserRole;
}

// ─── Strategy ───────────────────────────────────────────────────────────────

export interface Guideline {
  id: string;
  title: string;
  description: string;
  category: GuidelineCategory;
  campaign: string | null;
  validFrom: string;
  validUntil: string | null;
  isCurrent: boolean;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
}

export interface GuidelineHistoryEntry {
  id: string;
  guidelineId: string;
  changeType: 'Created' | 'Updated' | 'Closed';
  changedBy: string;
  changedByName: string;
  changedAt: string;
  title: string;
  description: string;
  category: GuidelineCategory;
  campaign: string | null;
  validFrom: string;
  validUntil: string | null;
}

export interface GuidelineInput {
  title: string;
  description: string;
  category: GuidelineCategory;
  campaign: string | null;
  validFrom: string;
  validUntil: string | null;
}

// ─── Ideas ──────────────────────────────────────────────────────────────────

export interface FlowScore {
  total: number;
  strategicAlignment: number;
  impact: number;
  feasibility: number;
  urgency: number;
  confidence: number;
  computedAt: string;
  formulaVersion: number;
}

export interface IdeaSummary {
  id: string;
  title: string;
  problem: string;
  status: IdeaStatus;
  priority: IdeaPriority;
  score: number | null;
  flowScore: number | null;
  submittedBy: string;
  submittedByName: string;
  linkedGuidelineId: string | null;
  createdAt: string;
}

export interface IdeaDetail {
  id: string;
  title: string;
  description: string;
  problem: string;
  status: IdeaStatus;
  priority: IdeaPriority;
  score: number | null;
  flowScore: FlowScore | null;
  submittedBy: string;
  submittedByName: string;
  managerComment: string | null;
  linkedGuidelineId: string | null;
  linkedGuidelineTitle: string | null;
  canEdit: boolean;
  canDelete: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface IdeaComment {
  id: string;
  authorId: string;
  authorName: string;
  body: string;
  createdAt: string;
}

export interface IdeaInput {
  title: string;
  description: string;
  problem: string;
  linkedGuidelineId: string | null;
}

export interface IdeaComparisonRow {
  id: string;
  title: string;
  status: IdeaStatus;
  priority: IdeaPriority;
  score: number | null;
  flowScore: number | null;
  flowScoreBreakdown: FlowScore | null;
  linkedGuidelineId: string | null;
  linkedGuidelineTitle: string | null;
  guidelineIsCurrent: boolean;
  commentCount: number;
  createdAt: string;
  daysUnderReview: number;
}

export interface IdeaComparison {
  ideas: IdeaComparisonRow[];
  highestFlowScoreId: string | null;
  highestScoreId: string | null;
  generatedAt: string;
}

// ─── Projects ───────────────────────────────────────────────────────────────

export interface ProjectSummary {
  id: string;
  title: string;
  status: ProjectStatus;
  stage: ProjectStage;
  progressPercentage: number;
  priority: ProjectPriority;
  ownerId: string;
  ownerName: string;
  sourceIdeaId: string | null;
  linkedGuidelineId: string | null;
  deadline: string | null;
  blockedReason: string | null;
  isOverdue: boolean;
  isAtRisk: boolean;
  createdAt: string;
}

export interface ProjectDetail {
  id: string;
  title: string;
  description: string;
  status: ProjectStatus;
  stage: ProjectStage;
  progressPercentage: number;
  priority: ProjectPriority;
  ownerId: string;
  ownerName: string;
  sourceIdeaId: string | null;
  sourceIdeaTitle: string | null;
  linkedGuidelineId: string | null;
  linkedGuidelineTitle: string | null;
  estimatedCost: number | null;
  actualCost: number | null;
  startDate: string | null;
  deadline: string | null;
  completedAt: string | null;
  blockedReason: string | null;
  blockedSince: string | null;
  daysBlocked: number | null;
  cancelledReason: string | null;
  isOverdue: boolean;
  isAtRisk: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface TimelineEntry {
  action: string;
  actorId: string;
  actorName: string;
  oldValue: string | null;
  newValue: string | null;
  reason: string | null;
  timestamp: string;
}

export interface ProjectSnapshot {
  id: string;
  projectId: string;
  title: string;
  status: ProjectStatus;
  stage: ProjectStage;
  progressPercentage: number;
  priority: ProjectPriority;
  ownerId: string;
  ownerName: string;
  linkedGuidelineId: string | null;
  estimatedCost: number | null;
  actualCost: number | null;
  startDate: string | null;
  deadline: string | null;
  completedAt: string | null;
  blockedReason: string | null;
  cancelledReason: string | null;
  triggerAction: string;
  triggeredByActorId: string;
  schemaVersion: number;
  takenAt: string;
}

export interface CreateProjectInput {
  title: string;
  description: string;
  priority: ProjectPriority;
  ownerId: string;
  linkedGuidelineId: string | null;
  estimatedCost: number | null;
  deadline: string | null;
}

export interface ConvertIdeaInput {
  title: string;
  description: string;
  priority: ProjectPriority;
  ownerId: string;
  estimatedCost: number | null;
  deadline: string | null;
  assistantRunId: string | null;
}

// ─── Results ────────────────────────────────────────────────────────────────

export interface ResultMeasurement {
  revenue: number | null;
  savings: number | null;
  cost: number | null;
  roi: number | null;
  netValue: number | null;
  recordedAt: string;
}

export interface ProjectResult {
  id: string;
  projectId: string;
  estimated: ResultMeasurement | null;
  actual: ResultMeasurement | null;
  paybackPeriodMonths: number | null;
  productivityGainPercent: number | null;
  timeSavedHours: number | null;
  qualityGainPercent: number | null;
  notes: string | null;
  recordedBy: string;
  recordedAt: string;
  updatedAt: string;
}

export interface RecordResultInput {
  estimatedRevenue: number | null;
  estimatedSavings: number | null;
  estimatedCost: number | null;
  actualRevenue: number | null;
  actualSavings: number | null;
  actualCost: number | null;
  paybackPeriodMonths: number | null;
  productivityGainPercent: number | null;
  timeSavedHours: number | null;
  qualityGainPercent: number | null;
  notes: string | null;
}

// ─── Dashboard ──────────────────────────────────────────────────────────────

export interface DistributionSlice {
  label: string;
  count: number;
  percentage: number;
}

export interface TrendPoint {
  period: string;
  ideas: number;
  projects: number;
  completed: number;
}

export interface IdeaFunnel {
  total: number;
  draft: number;
  underReview: number;
  approved: number;
  rejected: number;
  convertedToProjects: number;
  approvalRate: number;
  conversionRate: number;
}

export interface ProjectHealth {
  total: number;
  planned: number;
  inProgress: number;
  blocked: number;
  completed: number;
  cancelled: number;
  overdue: number;
  atRisk: number;
  averageProgress: number;
  averageCompletionDays: number;
  averageBlockedDays: number;
  bottleneckIndex: number;
  byStatus: DistributionSlice[];
  byStage: DistributionSlice[];
}

export interface FinancialOutcome {
  estimatedRevenue: number;
  estimatedSavings: number;
  estimatedCost: number;
  estimatedNetValue: number;
  estimatedRoiAverage: number | null;
  actualRevenue: number;
  actualSavings: number;
  actualCost: number;
  actualNetValue: number;
  actualRoiAverage: number | null;
  averagePaybackMonths: number | null;
  projectsWithEstimated: number;
  projectsWithActual: number;
}

export interface ImpactOutcome {
  averageProductivityGainPercent: number | null;
  totalTimeSavedHours: number;
  averageQualityGainPercent: number | null;
  projectsReporting: number;
}

export interface BlockedProjectRow {
  projectId: string;
  title: string;
  ownerId: string;
  ownerName: string;
  reason: string;
  daysBlocked: number;
}

export interface RiskProjectRow {
  projectId: string;
  title: string;
  ownerId: string;
  ownerName: string;
  status: ProjectStatus;
  stage: ProjectStage;
  progressPercentage: number;
  deadline: string | null;
  daysToDeadline: number;
  isOverdue: boolean;
}

export interface RankedIdea {
  ideaId: string;
  title: string;
  status: IdeaStatus;
  score: number | null;
  flowScore: number | null;
  submittedByName: string;
}

export interface RankedProject {
  projectId: string;
  title: string;
  status: ProjectStatus;
  progressPercentage: number;
  actualRoi: number | null;
  netValue: number | null;
}

export interface StrategyPerformance {
  guidelineId: string;
  title: string;
  category: GuidelineCategory;
  campaign: string | null;
  isCurrent: boolean;
  ideaCount: number;
  projectCount: number;
  completedProjectCount: number;
  actualNetValue: number | null;
}

export interface CampaignPerformance {
  campaign: string;
  guidelineCount: number;
  ideaCount: number;
  projectCount: number;
  completedProjectCount: number;
  actualNetValue: number | null;
}

export interface DashboardSummary {
  ideas: IdeaFunnel;
  projects: ProjectHealth;
  financial: FinancialOutcome;
  impact: ImpactOutcome;
  blockedProjects: BlockedProjectRow[];
  projectsAtRisk: RiskProjectRow[];
  topIdeas: RankedIdea[];
  topProjects: RankedProject[];
  byStrategy: StrategyPerformance[];
  byCampaign: CampaignPerformance[];
  trends: TrendPoint[];
  generatedAt: string;
}

export interface ProjectDashboard {
  projectId: string;
  title: string;
  status: ProjectStatus;
  stage: ProjectStage;
  progressPercentage: number;
  ownerId: string;
  ownerName: string;
  sourceIdeaId: string | null;
  sourceIdeaTitle: string | null;
  linkedGuidelineId: string | null;
  linkedGuidelineTitle: string | null;
  deadline: string | null;
  daysToDeadline: number | null;
  isOverdue: boolean;
  isAtRisk: boolean;
  timesBlocked: number;
  totalDaysBlocked: number;
  snapshotCount: number;
  auditEntryCount: number;
  result: {
    estimatedNetValue: number | null;
    estimatedRoi: number | null;
    actualNetValue: number | null;
    actualRoi: number | null;
    paybackPeriodMonths: number | null;
    productivityGainPercent: number | null;
    timeSavedHours: number | null;
    qualityGainPercent: number | null;
  } | null;
  generatedAt: string;
}

export interface StrategyDashboard {
  guidelineId: string;
  title: string;
  description: string;
  category: GuidelineCategory;
  campaign: string | null;
  validFrom: string;
  validUntil: string | null;
  isCurrent: boolean;
  ideaCount: number;
  ideasByStatus: DistributionSlice[];
  projectCount: number;
  projectsByStatus: DistributionSlice[];
  estimatedNetValue: number;
  actualNetValue: number;
  generatedAt: string;
}

// ─── Notifications ──────────────────────────────────────────────────────────

export interface Notification {
  id: string;
  type: string;
  title: string;
  body: string;
  deepLink: string | null;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
}

export interface NotificationPage {
  items: Notification[];
  unreadCount: number;
}

// ─── Gamification ───────────────────────────────────────────────────────────

export interface PointsSummary {
  userId: string;
  userName: string;
  points: number;
}

export interface PointsLedgerEntry {
  id: string;
  points: number;
  reason: string;
  referenceType: string;
  referenceId: string;
  awardedAt: string;
}

// ─── Assistant ──────────────────────────────────────────────────────────────

export interface IdeaAssessment {
  ideaId: string;
  title: string;
  strategicFit: string;
  strengths: string[];
  risks: string[];
  recommendation: string;
}

export interface IdeaComparisonInsight {
  summary: string;
  assessments: IdeaAssessment[];
  tradeOffs: string[];
  suggestedPriorityIdeaId: string | null;
  suggestedPriorityRationale: string;
  evidence: string[];
  evidenceWasSufficient: boolean;
}

export interface AssistantComparison {
  assistantRunId: string;
  model: string;
  latencyMs: number;
  insight: IdeaComparisonInsight;
}

export interface ProjectDraft {
  title: string;
  description: string;
  suggestedPriority: ProjectPriority;
  suggestedStage: ProjectStage;
  suggestedDurationDays: number | null;
  suggestedEstimatedCost: number | null;
  milestones: string[];
  risks: string[];
  successCriteria: string[];
  rationale: string;
  evidence: string[];
  evidenceWasSufficient: boolean;
}

export interface ProjectDraftResponse {
  assistantRunId: string;
  ideaId: string;
  model: string;
  latencyMs: number;
  draft: ProjectDraft;
}

export interface InsightItem {
  title: string;
  detail: string;
  evidence: string[];
}

export interface ExecutiveInsight {
  executiveSummary: string;
  highlights: InsightItem[];
  risks: InsightItem[];
  opportunities: InsightItem[];
  recommendations: InsightItem[];
  evidence: string[];
  evidenceWasSufficient: boolean;
  generatedAt: string;
}

export interface ExecutiveInsightResponse {
  assistantRunId: string;
  model: string;
  latencyMs: number;
  insight: ExecutiveInsight;
}

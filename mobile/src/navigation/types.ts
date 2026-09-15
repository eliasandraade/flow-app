import type { NavigatorScreenParams } from '@react-navigation/native';

/**
 * Navigation contract.
 *
 * Typed end to end so a route rename breaks at compile time rather than at the moment a
 * user taps something.
 */

export type OperatorStackParams = {
  OperatorHome: undefined;
  MyIdeas: undefined;
  IdeaForm: { ideaId?: string } | undefined;
  IdeaDetail: { ideaId: string };
  Points: undefined;
};

export type ManagerIdeasStackParams = {
  IdeaQueue: undefined;
  ManagerIdeaDetail: { ideaId: string };
  IdeaCompare: { ideaIds: string[] };
  Copilot: { ideaIds?: string[] } | undefined;
  ProjectDraftReview: { ideaId: string };
};

export type ManagerProjectsStackParams = {
  ProjectList: undefined;
  ProjectDetail: { projectId: string };
  ProjectForm: { projectId?: string } | undefined;
  ProjectResult: { projectId: string };
  ProjectTimeline: { projectId: string };
};

export type LeadershipStackParams = {
  Dashboard: undefined;
  Insights: undefined;
  LeadershipProjects: undefined;
  ProjectDetail: { projectId: string };
  ProjectResult: { projectId: string };
  ProjectTimeline: { projectId: string };
  StrategyDetail: { guidelineId: string };
};

export type StrategyStackParams = {
  Strategies: undefined;
  StrategyDetail: { guidelineId: string };
  StrategyForm: { guidelineId?: string } | undefined;
  StrategyHistory: { guidelineId: string };
};

export type SharedStackParams = {
  Notifications: undefined;
  Profile: undefined;
};

export type OperatorTabsParams = {
  OperatorIdeas: NavigatorScreenParams<OperatorStackParams>;
  OperatorStrategies: NavigatorScreenParams<StrategyStackParams>;
  OperatorNotifications: NavigatorScreenParams<SharedStackParams>;
  OperatorProfile: NavigatorScreenParams<SharedStackParams>;
};

export type ManagerTabsParams = {
  ManagerIdeas: NavigatorScreenParams<ManagerIdeasStackParams>;
  ManagerProjects: NavigatorScreenParams<ManagerProjectsStackParams>;
  ManagerStrategies: NavigatorScreenParams<StrategyStackParams>;
  ManagerNotifications: NavigatorScreenParams<SharedStackParams>;
  ManagerProfile: NavigatorScreenParams<SharedStackParams>;
};

export type LeadershipTabsParams = {
  LeadershipDashboard: NavigatorScreenParams<LeadershipStackParams>;
  LeadershipStrategies: NavigatorScreenParams<StrategyStackParams>;
  LeadershipNotifications: NavigatorScreenParams<SharedStackParams>;
  LeadershipProfile: NavigatorScreenParams<SharedStackParams>;
};

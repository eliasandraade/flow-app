import React from 'react';
import { View, StyleSheet } from 'react-native';
import { NavigationContainer, DefaultTheme } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Ionicons } from '@expo/vector-icons';
import { useAuthStore } from '../store/authStore';
import { useNotifications } from '../api/queries';
import { theme } from '../theme';
import { Txt } from '../components/primitives';

import { LoginScreen } from '../screens/auth/LoginScreen';
import { NotificationsScreen } from '../screens/shared/NotificationsScreen';
import { ProfileScreen } from '../screens/shared/ProfileScreen';
import { StrategiesScreen } from '../screens/shared/StrategiesScreen';
import { StrategyDetailScreen } from '../screens/shared/StrategyDetailScreen';
import { StrategyFormScreen } from '../screens/shared/StrategyFormScreen';
import { StrategyHistoryScreen } from '../screens/shared/StrategyHistoryScreen';
import { OperatorHomeScreen } from '../screens/operator/OperatorHomeScreen';
import { MyIdeasScreen } from '../screens/operator/MyIdeasScreen';
import { IdeaFormScreen } from '../screens/operator/IdeaFormScreen';
import { IdeaDetailScreen } from '../screens/operator/IdeaDetailScreen';
import { IdeaQueueScreen } from '../screens/manager/IdeaQueueScreen';
import { ManagerIdeaDetailScreen } from '../screens/manager/ManagerIdeaDetailScreen';
import { IdeaCompareScreen } from '../screens/manager/IdeaCompareScreen';
import { CopilotScreen } from '../screens/manager/CopilotScreen';
import { ProjectDraftReviewScreen } from '../screens/manager/ProjectDraftReviewScreen';
import { ProjectListScreen } from '../screens/manager/ProjectListScreen';
import { ProjectDetailScreen } from '../screens/manager/ProjectDetailScreen';
import { ProjectFormScreen } from '../screens/manager/ProjectFormScreen';
import { ProjectResultScreen } from '../screens/manager/ProjectResultScreen';
import { ProjectTimelineScreen } from '../screens/manager/ProjectTimelineScreen';
import { DashboardScreen } from '../screens/leadership/DashboardScreen';
import { InsightsScreen } from '../screens/leadership/InsightsScreen';

const navigationTheme = {
  ...DefaultTheme,
  colors: {
    ...DefaultTheme.colors,
    primary: theme.colors.brand,
    background: theme.colors.surface.background,
    card: theme.colors.surface.card,
    text: theme.colors.text.primary,
    border: theme.colors.surface.border,
  },
};

const stackOptions = {
  headerStyle: { backgroundColor: theme.colors.surface.card },
  headerTitleStyle: { fontSize: 17, fontWeight: '600' as const },
  headerTintColor: theme.colors.text.brand,
  headerShadowVisible: false,
  contentStyle: { backgroundColor: theme.colors.surface.background },
};

// ─── Shared stacks ──────────────────────────────────────────────────────────

const StrategyStack = createNativeStackNavigator();

function StrategyNavigator() {
  return (
    <StrategyStack.Navigator screenOptions={stackOptions}>
      <StrategyStack.Screen
        name="Strategies"
        component={StrategiesScreen}
        options={{ title: 'Estratégia' }}
      />
      <StrategyStack.Screen
        name="StrategyDetail"
        component={StrategyDetailScreen}
        options={{ title: 'Diretriz' }}
      />
      <StrategyStack.Screen
        name="StrategyForm"
        component={StrategyFormScreen}
        options={{ title: 'Diretriz', presentation: 'modal' }}
      />
      <StrategyStack.Screen
        name="StrategyHistory"
        component={StrategyHistoryScreen}
        options={{ title: 'Histórico' }}
      />
    </StrategyStack.Navigator>
  );
}

const NotificationsStack = createNativeStackNavigator();

function NotificationsNavigator() {
  return (
    <NotificationsStack.Navigator screenOptions={stackOptions}>
      <NotificationsStack.Screen
        name="Notifications"
        component={NotificationsScreen}
        options={{ title: 'Notificações' }}
      />
    </NotificationsStack.Navigator>
  );
}

const ProfileStack = createNativeStackNavigator();

function ProfileNavigator() {
  return (
    <ProfileStack.Navigator screenOptions={stackOptions}>
      <ProfileStack.Screen name="Profile" component={ProfileScreen} options={{ title: 'Perfil' }} />
    </ProfileStack.Navigator>
  );
}

// ─── Operator ───────────────────────────────────────────────────────────────

const OperatorStack = createNativeStackNavigator();

function OperatorIdeasNavigator() {
  return (
    <OperatorStack.Navigator screenOptions={stackOptions}>
      <OperatorStack.Screen
        name="OperatorHome"
        component={OperatorHomeScreen}
        options={{ title: 'Flow' }}
      />
      <OperatorStack.Screen
        name="MyIdeas"
        component={MyIdeasScreen}
        options={{ title: 'Minhas ideias' }}
      />
      <OperatorStack.Screen
        name="IdeaForm"
        component={IdeaFormScreen}
        options={{ title: 'Nova ideia', presentation: 'modal' }}
      />
      <OperatorStack.Screen
        name="IdeaDetail"
        component={IdeaDetailScreen}
        options={{ title: 'Ideia' }}
      />
    </OperatorStack.Navigator>
  );
}

// ─── Manager ────────────────────────────────────────────────────────────────

const ManagerIdeasStack = createNativeStackNavigator();

function ManagerIdeasNavigator() {
  return (
    <ManagerIdeasStack.Navigator screenOptions={stackOptions}>
      <ManagerIdeasStack.Screen
        name="IdeaQueue"
        component={IdeaQueueScreen}
        options={{ title: 'Fila de ideias' }}
      />
      <ManagerIdeasStack.Screen
        name="ManagerIdeaDetail"
        component={ManagerIdeaDetailScreen}
        options={{ title: 'Avaliar ideia' }}
      />
      <ManagerIdeasStack.Screen
        name="IdeaCompare"
        component={IdeaCompareScreen}
        options={{ title: 'Comparar' }}
      />
      <ManagerIdeasStack.Screen
        name="Copilot"
        component={CopilotScreen}
        options={{ title: 'Copiloto Flow' }}
      />
      <ManagerIdeasStack.Screen
        name="ProjectDraftReview"
        component={ProjectDraftReviewScreen}
        options={{ title: 'Rascunho de projeto' }}
      />
    </ManagerIdeasStack.Navigator>
  );
}

const ManagerProjectsStack = createNativeStackNavigator();

function ManagerProjectsNavigator() {
  return (
    <ManagerProjectsStack.Navigator screenOptions={stackOptions}>
      <ManagerProjectsStack.Screen
        name="ProjectList"
        component={ProjectListScreen}
        options={{ title: 'Projetos' }}
      />
      <ManagerProjectsStack.Screen
        name="ProjectDetail"
        component={ProjectDetailScreen}
        options={{ title: 'Projeto' }}
      />
      <ManagerProjectsStack.Screen
        name="ProjectForm"
        component={ProjectFormScreen}
        options={{ title: 'Projeto', presentation: 'modal' }}
      />
      <ManagerProjectsStack.Screen
        name="ProjectResult"
        component={ProjectResultScreen}
        options={{ title: 'Resultado' }}
      />
      <ManagerProjectsStack.Screen
        name="ProjectTimeline"
        component={ProjectTimelineScreen}
        options={{ title: 'Governança' }}
      />
    </ManagerProjectsStack.Navigator>
  );
}

// ─── Leadership ─────────────────────────────────────────────────────────────

const LeadershipStack = createNativeStackNavigator();

/** Leadership sees the same project screens as the manager, without the write actions. */
function LeadershipProjectList() {
  return <ProjectListScreen readOnly />;
}

function LeadershipNavigator() {
  return (
    <LeadershipStack.Navigator screenOptions={stackOptions}>
      <LeadershipStack.Screen
        name="Dashboard"
        component={DashboardScreen}
        options={{ title: 'Painel executivo' }}
      />
      <LeadershipStack.Screen
        name="Insights"
        component={InsightsScreen}
        options={{ title: 'Insights executivos' }}
      />
      <LeadershipStack.Screen
        name="LeadershipProjects"
        component={LeadershipProjectList}
        options={{ title: 'Projetos' }}
      />
      <LeadershipStack.Screen
        name="ProjectDetail"
        component={ProjectDetailScreen}
        options={{ title: 'Projeto' }}
      />
      <LeadershipStack.Screen
        name="ProjectResult"
        component={ProjectResultScreen}
        options={{ title: 'Resultado' }}
      />
      <LeadershipStack.Screen
        name="ProjectTimeline"
        component={ProjectTimelineScreen}
        options={{ title: 'Governança' }}
      />
      <LeadershipStack.Screen
        name="StrategyDetail"
        component={StrategyDetailScreen}
        options={{ title: 'Diretriz' }}
      />
    </LeadershipStack.Navigator>
  );
}

// ─── Tabs ───────────────────────────────────────────────────────────────────

const Tabs = createBottomTabNavigator();

type IconName = keyof typeof Ionicons.glyphMap;

function tabIcon(name: IconName, focused: boolean, color: string) {
  return <Ionicons name={name} size={focused ? 25 : 23} color={color} />;
}

/** Unread badge on the notifications tab, so a decision does not wait for someone to look. */
function NotificationsIcon({ focused, color }: { focused: boolean; color: string }) {
  const { data } = useNotifications(true);
  const unread = data?.unreadCount ?? 0;

  return (
    <View>
      {tabIcon(focused ? 'notifications' : 'notifications-outline', focused, color)}
      {unread > 0 ? (
        <View style={styles.badge}>
          <Txt variant="caption" color={theme.colors.text.inverse} style={styles.badgeText}>
            {unread > 9 ? '9+' : String(unread)}
          </Txt>
        </View>
      ) : null}
    </View>
  );
}

const tabOptions = {
  headerShown: false,
  tabBarActiveTintColor: theme.colors.brand,
  tabBarInactiveTintColor: theme.colors.text.muted,
  tabBarStyle: {
    backgroundColor: theme.colors.surface.card,
    borderTopColor: theme.colors.surface.border,
    height: 60,
    paddingBottom: 6,
    paddingTop: 6,
  },
  tabBarLabelStyle: { fontSize: 11, fontWeight: '600' as const },
};

function OperatorTabs() {
  return (
    <Tabs.Navigator screenOptions={tabOptions}>
      <Tabs.Screen
        name="OperatorIdeas"
        component={OperatorIdeasNavigator}
        options={{
          title: 'Ideias',
          tabBarIcon: ({ focused, color }) => tabIcon(focused ? 'bulb' : 'bulb-outline', focused, color),
        }}
      />
      <Tabs.Screen
        name="OperatorStrategies"
        component={StrategyNavigator}
        options={{
          title: 'Estratégia',
          tabBarIcon: ({ focused, color }) =>
            tabIcon(focused ? 'compass' : 'compass-outline', focused, color),
        }}
      />
      <Tabs.Screen
        name="OperatorNotifications"
        component={NotificationsNavigator}
        options={{ title: 'Avisos', tabBarIcon: NotificationsIcon }}
      />
      <Tabs.Screen
        name="OperatorProfile"
        component={ProfileNavigator}
        options={{
          title: 'Perfil',
          tabBarIcon: ({ focused, color }) =>
            tabIcon(focused ? 'person' : 'person-outline', focused, color),
        }}
      />
    </Tabs.Navigator>
  );
}

function ManagerTabs() {
  return (
    <Tabs.Navigator screenOptions={tabOptions}>
      <Tabs.Screen
        name="ManagerIdeas"
        component={ManagerIdeasNavigator}
        options={{
          title: 'Ideias',
          tabBarIcon: ({ focused, color }) => tabIcon(focused ? 'bulb' : 'bulb-outline', focused, color),
        }}
      />
      <Tabs.Screen
        name="ManagerProjects"
        component={ManagerProjectsNavigator}
        options={{
          title: 'Projetos',
          tabBarIcon: ({ focused, color }) =>
            tabIcon(focused ? 'briefcase' : 'briefcase-outline', focused, color),
        }}
      />
      <Tabs.Screen
        name="ManagerStrategies"
        component={StrategyNavigator}
        options={{
          title: 'Estratégia',
          tabBarIcon: ({ focused, color }) =>
            tabIcon(focused ? 'compass' : 'compass-outline', focused, color),
        }}
      />
      <Tabs.Screen
        name="ManagerNotifications"
        component={NotificationsNavigator}
        options={{ title: 'Avisos', tabBarIcon: NotificationsIcon }}
      />
      <Tabs.Screen
        name="ManagerProfile"
        component={ProfileNavigator}
        options={{
          title: 'Perfil',
          tabBarIcon: ({ focused, color }) =>
            tabIcon(focused ? 'person' : 'person-outline', focused, color),
        }}
      />
    </Tabs.Navigator>
  );
}

function LeadershipTabs() {
  return (
    <Tabs.Navigator screenOptions={tabOptions}>
      <Tabs.Screen
        name="LeadershipDashboard"
        component={LeadershipNavigator}
        options={{
          title: 'Painel',
          tabBarIcon: ({ focused, color }) =>
            tabIcon(focused ? 'stats-chart' : 'stats-chart-outline', focused, color),
        }}
      />
      <Tabs.Screen
        name="LeadershipStrategies"
        component={StrategyNavigator}
        options={{
          title: 'Estratégia',
          tabBarIcon: ({ focused, color }) =>
            tabIcon(focused ? 'compass' : 'compass-outline', focused, color),
        }}
      />
      <Tabs.Screen
        name="LeadershipNotifications"
        component={NotificationsNavigator}
        options={{ title: 'Avisos', tabBarIcon: NotificationsIcon }}
      />
      <Tabs.Screen
        name="LeadershipProfile"
        component={ProfileNavigator}
        options={{
          title: 'Perfil',
          tabBarIcon: ({ focused, color }) =>
            tabIcon(focused ? 'person' : 'person-outline', focused, color),
        }}
      />
    </Tabs.Navigator>
  );
}

const AuthStack = createNativeStackNavigator();

export function AppNavigator() {
  const session = useAuthStore((state) => state.session);

  return (
    <NavigationContainer theme={navigationTheme}>
      {!session ? (
        <AuthStack.Navigator screenOptions={{ headerShown: false }}>
          <AuthStack.Screen name="Login" component={LoginScreen} />
        </AuthStack.Navigator>
      ) : session.role === 'Operator' ? (
        <OperatorTabs />
      ) : session.role === 'Manager' ? (
        <ManagerTabs />
      ) : (
        <LeadershipTabs />
      )}
    </NavigationContainer>
  );
}

const styles = StyleSheet.create({
  badge: {
    position: 'absolute',
    top: -4,
    right: -8,
    minWidth: 18,
    height: 18,
    borderRadius: 9,
    paddingHorizontal: 4,
    backgroundColor: theme.colors.danger,
    alignItems: 'center',
    justifyContent: 'center',
  },
  badgeText: { fontSize: 10, fontWeight: '700' },
});

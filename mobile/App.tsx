import React, { useEffect } from 'react';
import { StatusBar } from 'expo-status-bar';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { QueryClientProvider } from '@tanstack/react-query';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { createQueryClient } from './src/api/queries';
import { setSessionExpiredHandler } from './src/api/client';
import { useAuthStore } from './src/store/authStore';
import { AppNavigator } from './src/navigation/AppNavigator';
import { LoadingScreen } from './src/components/feedback';
import { theme } from './src/theme';

const queryClient = createQueryClient();

export default function App() {
  const hydrated = useAuthStore((state) => state.hydrated);
  const hydrate = useAuthStore((state) => state.hydrate);
  const clearSession = useAuthStore((state) => state.clearSession);

  useEffect(() => {
    void hydrate();
  }, [hydrate]);

  useEffect(() => {
    // The API client owns the refresh flow; when it finally gives up, the app returns to
    // the login screen and every cached query is discarded so no stale data leaks into the
    // next session.
    setSessionExpiredHandler(() => {
      clearSession();
      queryClient.clear();
    });

    return () => setSessionExpiredHandler(null);
  }, [clearSession]);

  return (
    <GestureHandlerRootView style={{ flex: 1, backgroundColor: theme.colors.surface.background }}>
      <SafeAreaProvider>
        <QueryClientProvider client={queryClient}>
          <StatusBar style="dark" />
          {hydrated ? <AppNavigator /> : <LoadingScreen label="Abrindo o Flow…" />}
        </QueryClientProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}

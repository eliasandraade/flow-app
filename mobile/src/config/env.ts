import Constants from 'expo-constants';
import { Platform } from 'react-native';

export type Environment = 'local' | 'development' | 'production';

interface FlowConfig {
  environment: Environment;
  apiBaseUrl: string;
  oneSignalAppId: string | null;
  isProduction: boolean;
}

/**
 * Resolves the API base URL for the current build.
 *
 * The previous version hardcoded `http://10.0.2.2:5153`, which only ever worked on an
 * Android emulator: an iOS simulator, a physical device and any deployed build all failed
 * against it. Resolution now goes, in order:
 *
 *   1. EXPO_PUBLIC_API_URL, set per EAS build profile — the only path production uses;
 *   2. the dev server's own host, discovered from the Expo manifest, so a physical device
 *      on the same network reaches the machine running the API without editing anything;
 *   3. a per-platform localhost fallback for simulators.
 */
function resolveApiBaseUrl(): string {
  const configured =
    process.env.EXPO_PUBLIC_API_URL ??
    (Constants.expoConfig?.extra?.apiBaseUrl as string | undefined);

  if (configured && configured.length > 0) return normalise(configured);

  // In development Expo exposes the host serving the bundle, which is the same machine
  // running the API in the normal local setup.
  const hostUri = Constants.expoConfig?.hostUri ?? Constants.expoGoConfig?.debuggerHost;
  const host = hostUri?.split(':')[0];

  if (host && host !== 'localhost' && host !== '127.0.0.1') {
    return normalise(`http://${host}:5153`);
  }

  // Android emulators reach the host machine through 10.0.2.2, never through localhost.
  const fallbackHost = Platform.OS === 'android' ? '10.0.2.2' : 'localhost';
  return normalise(`http://${fallbackHost}:5153`);
}

function normalise(baseUrl: string): string {
  const trimmed = baseUrl.replace(/\/+$/, '');
  return trimmed.endsWith('/api/v1') ? trimmed : `${trimmed}/api/v1`;
}

function resolveEnvironment(): Environment {
  const declared = (process.env.EXPO_PUBLIC_ENV ??
    Constants.expoConfig?.extra?.environment) as Environment | undefined;

  if (declared) return declared;
  return __DEV__ ? 'local' : 'production';
}

const environment = resolveEnvironment();

export const config: FlowConfig = {
  environment,
  apiBaseUrl: resolveApiBaseUrl(),
  oneSignalAppId:
    process.env.EXPO_PUBLIC_ONESIGNAL_APP_ID ??
    (Constants.expoConfig?.extra?.oneSignalAppId as string | undefined) ??
    null,
  isProduction: environment === 'production',
};

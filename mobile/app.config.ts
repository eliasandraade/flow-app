import type { ExpoConfig, ConfigContext } from 'expo/config';

/**
 * Dynamic Expo configuration.
 *
 * Replaces the static app.json so that the API base URL, the environment name and the
 * OneSignal app id come from the build profile rather than being hardcoded — which is what
 * makes one codebase produce a local, a development and a production build.
 *
 * Only the OneSignal *app id* appears here. It is a public identifier. The REST API key is
 * a server secret and never reaches the app.
 */
export default ({ config }: ConfigContext): ExpoConfig => {
  const environment = process.env.EXPO_PUBLIC_ENV ?? 'local';
  const oneSignalAppId = process.env.EXPO_PUBLIC_ONESIGNAL_APP_ID ?? '';

  const plugins: ExpoConfig['plugins'] = [
    'expo-secure-store',
    [
      'expo-build-properties',
      {
        android: {
          // A local API is reached over plain HTTP during development; production builds
          // point at HTTPS and get no such exemption.
          usesCleartextTraffic: environment !== 'production',
        },
      },
    ],
  ];

  // The plugin requires a real app id; adding it empty breaks the native build, so push is
  // simply absent from builds that were not given credentials.
  if (oneSignalAppId) {
    plugins.push([
      'onesignal-expo-plugin',
      { mode: environment === 'production' ? 'production' : 'development' },
    ]);
  }

  return {
    ...config,
    name: environment === 'production' ? 'Flow' : `Flow (${environment})`,
    slug: 'flow',
    scheme: 'flow',
    version: '2.0.0',
    orientation: 'portrait',
    icon: './assets/icon.png',
    userInterfaceStyle: 'light',
    newArchEnabled: true,
    splash: {
      image: './assets/splash-icon.png',
      resizeMode: 'contain',
      backgroundColor: '#FFFFFF',
    },
    ios: {
      supportsTablet: true,
      bundleIdentifier: 'com.flow.innovation',
    },
    android: {
      package: 'com.flow.innovation',
      versionCode: 2,
      adaptiveIcon: {
        foregroundImage: './assets/adaptive-icon.png',
        backgroundColor: '#FFFFFF',
      },
      edgeToEdgeEnabled: true,
      predictiveBackGestureEnabled: false,
    },
    web: { favicon: './assets/favicon.png' },
    plugins,
    extra: {
      environment,
      apiBaseUrl: process.env.EXPO_PUBLIC_API_URL ?? '',
      oneSignalAppId: oneSignalAppId || null,
      eas: { projectId: process.env.EAS_PROJECT_ID ?? undefined },
    },
  };
};

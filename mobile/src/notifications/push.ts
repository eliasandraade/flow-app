import { config } from '../config/env';

/**
 * OneSignal wiring.
 *
 * The device is tied to the Flow user id through OneSignal's external id, which is what
 * the server targets. E-mail is deliberately not used: it is neither a stable identifier
 * nor an authorisation claim.
 *
 * The module is imported lazily so the app runs unchanged in Expo Go, where the native
 * OneSignal module does not exist. Push simply does not happen there; everything else,
 * including the in-app notification centre, works exactly the same.
 */

type OneSignalModule = {
  initialize: (appId: string) => void;
  login: (externalId: string) => void;
  logout: () => void;
  Notifications: {
    requestPermission: (fallbackToSettings: boolean) => Promise<boolean>;
  };
};

let oneSignal: OneSignalModule | null = null;
let initialised = false;

function load(): OneSignalModule | null {
  if (oneSignal) return oneSignal;

  try {
    // Resolved at runtime: in Expo Go the native module is absent and this throws, which
    // is an expected state rather than an error worth surfacing.
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    const module = require('react-native-onesignal');
    oneSignal = (module.OneSignal ?? module.default ?? module) as OneSignalModule;
    return oneSignal;
  } catch {
    return null;
  }
}

export const push = {
  get isAvailable(): boolean {
    return Boolean(config.oneSignalAppId) && load() !== null;
  },

  initialise(): void {
    if (initialised || !config.oneSignalAppId) return;

    const module = load();
    if (!module) return;

    try {
      module.initialize(config.oneSignalAppId);
      initialised = true;
    } catch {
      // A failed push init must never stop the app from starting.
    }
  },

  /** Called after login so the server can target this user by external id. */
  async identify(userId: string): Promise<void> {
    if (!config.oneSignalAppId) return;

    const module = load();
    if (!module) return;

    try {
      this.initialise();
      module.login(userId);

      // Asked only after sign-in, when the value of being notified is obvious, rather than
      // on first launch when it is not.
      await module.Notifications.requestPermission(false);
    } catch {
      // ignored on purpose
    }
  },

  /** Called on logout so the next user on this device does not inherit the subscription. */
  forget(): void {
    const module = load();
    if (!module) return;

    try {
      module.logout();
    } catch {
      // ignored on purpose
    }
  },
};

import * as SecureStore from 'expo-secure-store';

export type UserRole = 'Operator' | 'Manager' | 'Leadership';

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  userId: string;
  name: string;
  email: string;
  role: UserRole;
}

const KEYS = ['accessToken', 'refreshToken', 'userId', 'name', 'email', 'role'] as const;

/**
 * Tokens live in the device keystore, never in plain storage.
 *
 * The client also keeps them in memory: the refresh path reads the current pair on every
 * request, and hitting SecureStore each time would put a keystore round trip in front of
 * every call.
 */
let current: AuthSession | null = null;

/** Notified when the session is replaced or cleared, so the UI can react to a logout. */
type Listener = (session: AuthSession | null) => void;
const listeners = new Set<Listener>();

export const sessionStore = {
  get(): AuthSession | null {
    return current;
  },

  subscribe(listener: Listener): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },

  async load(): Promise<AuthSession | null> {
    try {
      const entries = await Promise.all(
        KEYS.map(async (key) => [key, await SecureStore.getItemAsync(key)] as const)
      );

      const values = Object.fromEntries(entries) as Record<(typeof KEYS)[number], string | null>;

      if (!values.accessToken || !values.refreshToken || !values.userId || !values.role) {
        current = null;
        return null;
      }

      current = {
        accessToken: values.accessToken,
        refreshToken: values.refreshToken,
        userId: values.userId,
        name: values.name ?? '',
        email: values.email ?? '',
        role: values.role as UserRole,
      };

      return current;
    } catch {
      // A keystore that cannot be read is indistinguishable from being logged out.
      current = null;
      return null;
    }
  },

  async save(session: AuthSession): Promise<void> {
    current = session;

    await Promise.all(
      KEYS.map((key) => SecureStore.setItemAsync(key, session[key] ?? ''))
    );

    notify(session);
  },

  /** Updates only the token pair, which is what a refresh produces. */
  async updateTokens(accessToken: string, refreshToken: string): Promise<void> {
    if (!current) return;

    current = { ...current, accessToken, refreshToken };

    await Promise.all([
      SecureStore.setItemAsync('accessToken', accessToken),
      SecureStore.setItemAsync('refreshToken', refreshToken),
    ]);
  },

  async clear(): Promise<void> {
    current = null;
    await Promise.all(KEYS.map((key) => SecureStore.deleteItemAsync(key).catch(() => undefined)));
    notify(null);
  },
};

function notify(session: AuthSession | null): void {
  listeners.forEach((listener) => listener(session));
}

import { create } from 'zustand';
import { authApi } from '../api/endpoints';
import { sessionStore, type AuthSession, type UserRole } from '../api/session';
import { push } from '../notifications/push';

interface AuthState {
  session: AuthSession | null;
  hydrated: boolean;

  hydrate: () => Promise<void>;
  signIn: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;

  /** Called by the API client when a refresh fails and the session is beyond saving. */
  clearSession: () => void;
}

/**
 * Session state only.
 *
 * Server data lives in TanStack Query, which already handles caching, invalidation and
 * refetching. Mirroring it here would create two sources of truth that disagree the first
 * time a mutation lands.
 */
export const useAuthStore = create<AuthState>((set) => ({
  session: null,
  hydrated: false,

  hydrate: async () => {
    const session = await sessionStore.load();
    set({ session, hydrated: true });

    if (session) void push.identify(session.userId);
  },

  signIn: async (email, password) => {
    const result = await authApi.login(email, password);
    const session: AuthSession = {
      accessToken: result.accessToken,
      refreshToken: result.refreshToken,
      userId: result.userId,
      name: result.name,
      email: result.email,
      role: result.role,
    };

    await sessionStore.save(session);
    set({ session });

    // Ties this device to the Flow user so the server can target push by external id.
    void push.identify(session.userId);
  },

  register: async (name, email, password) => {
    const result = await authApi.register(name, email, password);
    const session: AuthSession = {
      accessToken: result.accessToken,
      refreshToken: result.refreshToken,
      userId: result.userId,
      name: result.name,
      email: result.email,
      role: result.role,
    };

    await sessionStore.save(session);
    set({ session });
    void push.identify(session.userId);
  },

  signOut: async () => {
    const refreshToken = sessionStore.get()?.refreshToken;

    if (refreshToken) {
      // Best effort: the local session is cleared regardless, so a failed call here must
      // never leave the user stuck on a screen they wanted to leave.
      try {
        await authApi.logout(refreshToken);
      } catch {
        // ignored on purpose
      }
    }

    // Unsubscribe before clearing, so the next user on this device does not inherit it.
    push.forget();

    await sessionStore.clear();
    set({ session: null });
  },

  clearSession: () => {
    push.forget();
    set({ session: null });
  },
}));

export type { AuthSession, UserRole };

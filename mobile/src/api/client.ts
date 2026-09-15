import { config } from '../config/env';
import { ApiError, ProblemDetails, buildApiError, toApiError } from './errors';
import { sessionStore } from './session';

const DEFAULT_TIMEOUT_MS = 20_000;

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  /** Lets TanStack Query abort a request when the screen goes away. */
  signal?: AbortSignal;
  timeoutMs?: number;
  /** Auth endpoints must not attempt a refresh; that is what causes an infinite loop. */
  skipAuth?: boolean;
}

/**
 * Single-flight refresh.
 *
 * A screen typically fires several queries at once. When the access token has expired they
 * all come back 401 together, and a naive implementation would fire one refresh per
 * request — a refresh storm that, with rotation enabled, invalidates the chain and logs
 * the user out. Instead the first 401 starts a refresh and every other caller awaits the
 * same promise.
 */
let refreshInFlight: Promise<boolean> | null = null;

/** Called when the session is beyond saving, so the app can return to the login screen. */
let onSessionExpired: (() => void) | null = null;

export function setSessionExpiredHandler(handler: (() => void) | null): void {
  onSessionExpired = handler;
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await send(path, options);

  // A single retry after a successful refresh. The previous client cleared the session on
  // any 401, which meant a fifteen-minute access token logged the user out mid-task.
  if (response.status === 401 && !options.skipAuth) {
    const refreshed = await refreshSession();

    if (refreshed) {
      const retried = await send(path, options);
      return handle<T>(retried);
    }

    await sessionStore.clear();
    onSessionExpired?.();
  }

  return handle<T>(response);
}

async function send(path: string, options: RequestOptions): Promise<Response> {
  const { method = 'GET', body, signal, timeoutMs = DEFAULT_TIMEOUT_MS, skipAuth } = options;

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);

  // The caller's cancellation and our timeout both have to reach the same request.
  const onAbort = () => controller.abort();
  signal?.addEventListener('abort', onAbort);

  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  if (!skipAuth) {
    const token = sessionStore.get()?.accessToken;
    if (token) headers.Authorization = `Bearer ${token}`;
  }

  try {
    return await fetch(`${config.apiBaseUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: controller.signal,
    });
  } catch (error) {
    // Distinguish "the caller went away" from "we gave up waiting".
    if (signal?.aborted) throw toApiError(error);

    if (controller.signal.aborted) {
      throw new ApiError({ kind: 'timeout', message: 'O servidor demorou para responder.' });
    }

    throw toApiError(error);
  } finally {
    clearTimeout(timeout);
    signal?.removeEventListener('abort', onAbort);
  }
}

async function handle<T>(response: Response): Promise<T> {
  if (response.status === 204) return undefined as T;

  if (!response.ok) {
    throw buildApiError(response.status, await readProblem(response));
  }

  const text = await response.text();
  if (!text) return undefined as T;

  try {
    return JSON.parse(text) as T;
  } catch {
    throw new ApiError({
      kind: 'server',
      message: 'O servidor devolveu uma resposta inesperada.',
      status: response.status,
    });
  }
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    const text = await response.text();
    return text ? (JSON.parse(text) as ProblemDetails) : null;
  } catch {
    return null;
  }
}

async function refreshSession(): Promise<boolean> {
  if (refreshInFlight) return refreshInFlight;

  refreshInFlight = performRefresh().finally(() => {
    refreshInFlight = null;
  });

  return refreshInFlight;
}

async function performRefresh(): Promise<boolean> {
  const session = sessionStore.get();
  if (!session?.refreshToken) return false;

  try {
    const response = await send('/auth/refresh', {
      method: 'POST',
      skipAuth: true,
      body: { accessToken: session.accessToken, refreshToken: session.refreshToken },
    });

    if (!response.ok) return false;

    const renewed = (await response.json()) as {
      accessToken: string;
      refreshToken: string;
    };

    if (!renewed?.accessToken || !renewed?.refreshToken) return false;

    // The server rotates on every refresh, so the new pair must be persisted before any
    // waiting caller retries with it.
    await sessionStore.updateTokens(renewed.accessToken, renewed.refreshToken);
    return true;
  } catch {
    return false;
  }
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => apiRequest<T>(path, { method: 'GET', signal }),

  post: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    apiRequest<T>(path, { method: 'POST', body, signal }),

  put: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    apiRequest<T>(path, { method: 'PUT', body, signal }),

  patch: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    apiRequest<T>(path, { method: 'PATCH', body, signal }),

  delete: <T>(path: string, signal?: AbortSignal) =>
    apiRequest<T>(path, { method: 'DELETE', signal }),
};

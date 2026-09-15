/**
 * RFC 7807 problem document, as returned by every Flow endpoint on failure.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  /** Set by the API only when the message was written to be shown to the user. */
  userMessage?: string;
}

export type ApiErrorKind =
  | 'network'
  | 'timeout'
  | 'cancelled'
  | 'unauthorized'
  | 'forbidden'
  | 'notFound'
  | 'conflict'
  | 'validation'
  | 'rateLimited'
  | 'serviceUnavailable'
  | 'server'
  | 'unknown';

/**
 * A typed failure.
 *
 * Screens branch on `kind`, never on a status number, and show `message`, which is already
 * a sentence a person can act on. `traceId` travels with it so a user-reported problem can
 * be found in the logs, the traces and the audit trail.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number | null;
  readonly traceId: string | null;
  readonly fieldErrors: Record<string, string[]> | null;
  readonly retryable: boolean;

  constructor(params: {
    kind: ApiErrorKind;
    message: string;
    status?: number | null;
    traceId?: string | null;
    fieldErrors?: Record<string, string[]> | null;
  }) {
    super(params.message);
    this.name = 'ApiError';
    this.kind = params.kind;
    this.status = params.status ?? null;
    this.traceId = params.traceId ?? null;
    this.fieldErrors = params.fieldErrors ?? null;
    this.retryable = RETRYABLE.has(params.kind);
  }

  /** First message for a field, for inline form feedback. */
  fieldError(field: string): string | undefined {
    if (!this.fieldErrors) return undefined;

    const exact = this.fieldErrors[field];
    if (exact?.length) return exact[0];

    // ASP.NET Core casing varies between model binding and FluentValidation.
    const key = Object.keys(this.fieldErrors).find(
      (k) => k.toLowerCase() === field.toLowerCase()
    );
    return key ? this.fieldErrors[key][0] : undefined;
  }
}

const RETRYABLE = new Set<ApiErrorKind>([
  'network',
  'timeout',
  'server',
  'serviceUnavailable',
  'rateLimited',
]);

const MESSAGES: Record<ApiErrorKind, string> = {
  network: 'Sem conexão com o servidor. Verifique sua internet e tente novamente.',
  timeout: 'O servidor demorou para responder. Tente novamente.',
  cancelled: 'Requisição cancelada.',
  unauthorized: 'Sua sessão expirou. Entre novamente.',
  forbidden: 'Você não tem permissão para esta ação.',
  notFound: 'Não encontramos o que você procura.',
  conflict: 'Esta ação não é possível no estado atual.',
  validation: 'Revise os campos destacados.',
  rateLimited: 'Muitas tentativas em pouco tempo. Aguarde um instante.',
  serviceUnavailable: 'Serviço temporariamente indisponível.',
  server: 'Algo deu errado do nosso lado. Tente novamente em instantes.',
  unknown: 'Não foi possível concluir a ação.',
};

export function kindForStatus(status: number): ApiErrorKind {
  if (status === 401) return 'unauthorized';
  if (status === 403) return 'forbidden';
  if (status === 404) return 'notFound';
  if (status === 409) return 'conflict';
  if (status === 422 || status === 400) return 'validation';
  if (status === 429) return 'rateLimited';
  if (status === 503) return 'serviceUnavailable';
  if (status >= 500) return 'server';
  return 'unknown';
}

/**
 * The interface is pt-BR, so the copy shown to the user lives here.
 *
 * `title` is deliberately NOT used: it carries the server's exception message, which is
 * written for developers and in English. It stays useful for support — it travels with the
 * traceId — but echoing it would put English in a Portuguese app. The server marks the
 * messages it wrote *for the user* with `userMessage`, and only those are shown verbatim.
 *
 * Field-level messages come through `errors` and are already localised by the API.
 */
export function buildApiError(status: number, problem: ProblemDetails | null): ApiError {
  const kind = kindForStatus(status);

  const serverMessage = problem?.userMessage?.trim() || undefined;

  return new ApiError({
    kind,
    message: serverMessage ?? MESSAGES[kind],
    status,
    traceId: problem?.traceId ?? null,
    fieldErrors: problem?.errors ?? null,
  });
}

export function messageForKind(kind: ApiErrorKind): string {
  return MESSAGES[kind];
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;

  if (error instanceof DOMException && error.name === 'AbortError') {
    return new ApiError({ kind: 'cancelled', message: MESSAGES.cancelled });
  }

  if (error instanceof TypeError) {
    // `fetch` reports every connection problem as a TypeError.
    return new ApiError({ kind: 'network', message: MESSAGES.network });
  }

  return new ApiError({
    kind: 'unknown',
    message: error instanceof Error ? error.message : MESSAGES.unknown,
  });
}

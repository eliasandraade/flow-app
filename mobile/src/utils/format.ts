/**
 * Formatting helpers, all pt-BR.
 *
 * Centralised so a currency or a date never gets formatted two different ways on two
 * different screens.
 */

const currency = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  maximumFractionDigits: 0,
});

const currencyPrecise = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const decimal = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 });

export function formatCurrency(value: number | null | undefined, precise = false): string {
  if (value === null || value === undefined) return '—';
  return precise ? currencyPrecise.format(value) : currency.format(value);
}

/** Large money in compact form, so a KPI tile does not wrap. */
export function formatCompactCurrency(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';

  const abs = Math.abs(value);
  const sign = value < 0 ? '-' : '';

  if (abs >= 1_000_000) return `${sign}R$ ${decimal.format(abs / 1_000_000)}M`;
  if (abs >= 1_000) return `${sign}R$ ${decimal.format(abs / 1_000)}k`;
  return currency.format(value);
}

export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return decimal.format(value);
}

export function formatPercent(value: number | null | undefined, digits = 0): string {
  if (value === null || value === undefined) return '—';
  return `${value.toFixed(digits).replace('.', ',')}%`;
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return '—';

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';

  return date.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' });
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—';

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';

  return date.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** Relative time, which is what people actually want on a notification list. */
export function formatRelative(value: string | null | undefined): string {
  if (!value) return '—';

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';

  const seconds = Math.floor((Date.now() - date.getTime()) / 1000);

  if (seconds < 60) return 'agora';
  if (seconds < 3600) return `há ${Math.floor(seconds / 60)} min`;
  if (seconds < 86400) return `há ${Math.floor(seconds / 3600)} h`;

  const days = Math.floor(seconds / 86400);
  if (days === 1) return 'ontem';
  if (days < 30) return `há ${days} dias`;
  if (days < 365) return `há ${Math.floor(days / 30)} meses`;

  return formatDate(value);
}

export function formatDays(days: number | null | undefined): string {
  if (days === null || days === undefined) return '—';
  if (days === 0) return 'hoje';
  if (days === 1) return '1 dia';
  return `${Math.round(days)} dias`;
}

/** Days until a deadline, phrased the way a person would say it. */
export function formatDeadline(days: number | null | undefined): string {
  if (days === null || days === undefined) return 'sem prazo';
  if (days < 0) return `${Math.abs(Math.round(days))} dias em atraso`;
  if (days === 0) return 'vence hoje';
  if (days === 1) return 'vence amanhã';
  return `faltam ${Math.round(days)} dias`;
}

/** Turns "2026-09" into "set/26" for the trend axis. */
export function formatPeriod(period: string): string {
  const [year, month] = period.split('-');
  const months = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];
  const index = Number(month) - 1;

  if (Number.isNaN(index) || index < 0 || index > 11) return period;
  return `${months[index]}/${year.slice(2)}`;
}

export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);

  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();

  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/** Parses a pt-BR decimal input ("1.234,56") into a number. */
export function parseDecimalInput(value: string): number | null {
  const cleaned = value.trim().replace(/\./g, '').replace(',', '.');
  if (cleaned === '') return null;

  const parsed = Number(cleaned);
  return Number.isFinite(parsed) ? parsed : null;
}

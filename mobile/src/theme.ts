import { Platform } from 'react-native';

/**
 * Flow design tokens.
 *
 * One palette, one type scale, one spacing rhythm. Screens compose from these and never
 * hardcode a colour or a pixel value, which is what keeps a twenty-screen app from drifting
 * into twenty slightly different designs.
 */

const palette = {
  // Indigo carries the brand; it is used for action, never for decoration.
  brand900: '#1E1B4B',
  brand700: '#3730A3',
  brand600: '#4338CA',
  brand500: '#4F46E5',
  brand100: '#E0E7FF',
  brand50: '#EEF2FF',

  slate900: '#0F172A',
  slate800: '#1E293B',
  slate700: '#334155',
  slate600: '#475569',
  slate500: '#64748B',
  slate400: '#94A3B8',
  slate300: '#CBD5E1',
  slate200: '#E2E8F0',
  slate100: '#F1F5F9',
  slate50: '#F8FAFC',
  white: '#FFFFFF',

  emerald700: '#047857',
  emerald600: '#059669',
  emerald100: '#D1FAE5',
  emerald50: '#ECFDF5',

  amber700: '#B45309',
  amber600: '#D97706',
  amber100: '#FEF3C7',
  amber50: '#FFFBEB',

  rose700: '#BE123C',
  rose600: '#E11D48',
  rose100: '#FFE4E6',
  rose50: '#FFF1F2',

  orange700: '#C2410C',
  orange100: '#FFEDD5',
  orange50: '#FFF7ED',

  sky700: '#0369A1',
  sky100: '#E0F2FE',
  sky50: '#F0F9FF',

  violet700: '#6D28D9',
  violet100: '#EDE9FE',
  violet50: '#F5F3FF',
};

export interface StatusTone {
  bg: string;
  text: string;
  border: string;
}

const tone = (bg: string, text: string, border: string): StatusTone => ({ bg, text, border });

export const theme = {
  colors: {
    brand: palette.brand600,
    brandStrong: palette.brand700,
    brandSurface: palette.brand50,
    brandBorder: palette.brand100,

    success: palette.emerald600,
    successSurface: palette.emerald50,
    warning: palette.amber600,
    warningSurface: palette.amber50,
    danger: palette.rose600,
    dangerSurface: palette.rose50,
    info: palette.sky700,
    infoSurface: palette.sky50,

    text: {
      primary: palette.slate900,
      secondary: palette.slate600,
      muted: palette.slate400,
      inverse: palette.white,
      brand: palette.brand700,
    },

    surface: {
      background: palette.slate50,
      card: palette.white,
      raised: palette.white,
      sunken: palette.slate100,
      border: palette.slate200,
      borderStrong: palette.slate300,
      inputBorder: palette.slate300,
      overlay: 'rgba(15, 23, 42, 0.45)',
    },

    /** Status tones shared by badges, borders and accents so a state always looks the same. */
    status: {
      draft: tone(palette.slate100, palette.slate600, palette.slate200),
      underReview: tone(palette.amber50, palette.amber700, palette.amber100),
      approved: tone(palette.emerald50, palette.emerald700, palette.emerald100),
      rejected: tone(palette.rose50, palette.rose700, palette.rose100),
      planned: tone(palette.sky50, palette.sky700, palette.sky100),
      inProgress: tone(palette.brand50, palette.brand700, palette.brand100),
      blocked: tone(palette.orange50, palette.orange700, palette.orange100),
      completed: tone(palette.emerald50, palette.emerald700, palette.emerald100),
      cancelled: tone(palette.slate100, palette.slate500, palette.slate200),
      neutral: tone(palette.slate100, palette.slate600, palette.slate200),
      risk: tone(palette.amber50, palette.amber700, palette.amber100),
      overdue: tone(palette.rose50, palette.rose700, palette.rose100),
      strategy: tone(palette.violet50, palette.violet700, palette.violet100),
    },

    /** Categorical series for charts, ordered for maximum separation. */
    chart: [
      palette.brand500,
      palette.emerald600,
      palette.amber600,
      palette.sky700,
      palette.violet700,
      palette.rose600,
      palette.slate500,
    ],

    palette,
  },

  typography: {
    display: { fontSize: 32, lineHeight: 38, fontWeight: '700' as const, letterSpacing: -0.5 },
    heading: { fontSize: 22, lineHeight: 28, fontWeight: '700' as const, letterSpacing: -0.3 },
    subheading: { fontSize: 18, lineHeight: 24, fontWeight: '600' as const },
    title: { fontSize: 16, lineHeight: 22, fontWeight: '600' as const },
    body: { fontSize: 15, lineHeight: 22, fontWeight: '400' as const },
    bodyStrong: { fontSize: 15, lineHeight: 22, fontWeight: '600' as const },
    label: { fontSize: 13, lineHeight: 18, fontWeight: '600' as const },
    caption: { fontSize: 12, lineHeight: 16, fontWeight: '400' as const },
    overline: {
      fontSize: 11,
      lineHeight: 14,
      fontWeight: '700' as const,
      letterSpacing: 0.8,
      textTransform: 'uppercase' as const,
    },
    kpi: { fontSize: 28, lineHeight: 32, fontWeight: '700' as const, letterSpacing: -0.5 },
    kpiSmall: { fontSize: 20, lineHeight: 24, fontWeight: '700' as const },
  },

  spacing: { xxs: 2, xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32, xxxl: 48 },

  radius: { sm: 6, md: 10, lg: 14, xl: 20, full: 999 },

  /** Restrained elevation: two levels, used to separate layers rather than to decorate. */
  elevation: {
    card: Platform.select({
      ios: {
        shadowColor: palette.slate900,
        shadowOpacity: 0.06,
        shadowRadius: 12,
        shadowOffset: { width: 0, height: 2 },
      },
      android: { elevation: 2 },
      default: {},
    }),
    raised: Platform.select({
      ios: {
        shadowColor: palette.slate900,
        shadowOpacity: 0.12,
        shadowRadius: 20,
        shadowOffset: { width: 0, height: 6 },
      },
      android: { elevation: 6 },
      default: {},
    }),
  },

  /** 44pt is the smallest reliably tappable target; nothing interactive goes below it. */
  hitSlop: { top: 8, bottom: 8, left: 8, right: 8 },
  minTouchTarget: 44,
} as const;

export type Theme = typeof theme;

/** Maps a domain status string to its tone, so a badge never needs a switch at the call site. */
export function statusTone(status: string): StatusTone {
  const key = status.charAt(0).toLowerCase() + status.slice(1);
  return (theme.colors.status as Record<string, StatusTone>)[key] ?? theme.colors.status.neutral;
}

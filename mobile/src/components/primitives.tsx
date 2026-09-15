import React, { useMemo, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Animated,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type RefreshControlProps,
  type StyleProp,
  type TextInputProps,
  type TextStyle,
  type ViewStyle,
} from 'react-native';
import { SafeAreaView, type Edge } from 'react-native-safe-area-context';
import { theme } from '../theme';

// ─── Screen ─────────────────────────────────────────────────────────────────

export function Screen({
  children,
  scroll = false,
  refreshControl,
  edges = ['top', 'left', 'right'],
  padded = true,
  style,
}: {
  children: React.ReactNode;
  scroll?: boolean;
  refreshControl?: React.ReactElement<RefreshControlProps>;
  edges?: Edge[];
  padded?: boolean;
  style?: StyleProp<ViewStyle>;
}) {
  const content = padded ? [styles.screenPadded, style] : style;

  return (
    <SafeAreaView style={styles.screen} edges={edges}>
      {scroll ? (
        <ScrollView
          contentContainerStyle={[styles.scrollContent, content]}
          refreshControl={refreshControl}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          {children}
        </ScrollView>
      ) : (
        <View style={[styles.flex, content]}>{children}</View>
      )}
    </SafeAreaView>
  );
}

// ─── Text ───────────────────────────────────────────────────────────────────

type Variant = keyof typeof theme.typography;

export function Txt({
  children,
  variant = 'body',
  color,
  style,
  numberOfLines,
  align,
}: {
  children: React.ReactNode;
  variant?: Variant;
  color?: string;
  style?: StyleProp<TextStyle>;
  numberOfLines?: number;
  align?: 'left' | 'center' | 'right';
}) {
  return (
    <Text
      numberOfLines={numberOfLines}
      style={[
        theme.typography[variant] as TextStyle,
        { color: color ?? theme.colors.text.primary },
        align ? { textAlign: align } : null,
        style,
      ]}
    >
      {children}
    </Text>
  );
}

// ─── Card ───────────────────────────────────────────────────────────────────

export function Card({
  children,
  onPress,
  style,
  accent,
  accessibilityLabel,
}: {
  children: React.ReactNode;
  onPress?: () => void;
  style?: StyleProp<ViewStyle>;
  /** Left edge colour, used to encode state without adding another badge. */
  accent?: string;
  accessibilityLabel?: string;
}) {
  const body = (
    <View style={[styles.card, accent ? { borderLeftWidth: 4, borderLeftColor: accent } : null, style]}>
      {children}
    </View>
  );

  if (!onPress) return body;

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      style={({ pressed }) => [pressed && styles.pressed]}
    >
      {body}
    </Pressable>
  );
}

export function SectionHeader({
  title,
  subtitle,
  action,
}: {
  title: string;
  subtitle?: string;
  action?: React.ReactNode;
}) {
  return (
    <View style={styles.sectionHeader}>
      <View style={styles.flex}>
        <Txt variant="overline" color={theme.colors.text.muted}>
          {title}
        </Txt>
        {subtitle ? (
          <Txt variant="caption" color={theme.colors.text.secondary} style={styles.sectionSubtitle}>
            {subtitle}
          </Txt>
        ) : null}
      </View>
      {action}
    </View>
  );
}

// ─── Button ─────────────────────────────────────────────────────────────────

type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

export function Button({
  title,
  onPress,
  variant = 'primary',
  loading = false,
  disabled = false,
  fullWidth = true,
  compact = false,
  style,
}: {
  title: string;
  onPress: () => void;
  variant?: ButtonVariant;
  loading?: boolean;
  disabled?: boolean;
  fullWidth?: boolean;
  compact?: boolean;
  style?: StyleProp<ViewStyle>;
}) {
  const isDisabled = disabled || loading;
  const tone = BUTTON_TONES[variant];

  return (
    <Pressable
      onPress={onPress}
      disabled={isDisabled}
      accessibilityRole="button"
      accessibilityState={{ disabled: isDisabled, busy: loading }}
      accessibilityLabel={title}
      style={({ pressed }) => [
        styles.button,
        compact && styles.buttonCompact,
        { backgroundColor: tone.bg, borderColor: tone.border },
        fullWidth && styles.flexGrow,
        isDisabled && styles.buttonDisabled,
        pressed && !isDisabled && styles.pressed,
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator size="small" color={tone.text} />
      ) : (
        <Text style={[styles.buttonLabel, compact && styles.buttonLabelCompact, { color: tone.text }]}>
          {title}
        </Text>
      )}
    </Pressable>
  );
}

const BUTTON_TONES: Record<ButtonVariant, { bg: string; text: string; border: string }> = {
  primary: {
    bg: theme.colors.brand,
    text: theme.colors.text.inverse,
    border: theme.colors.brand,
  },
  secondary: {
    bg: theme.colors.surface.card,
    text: theme.colors.text.brand,
    border: theme.colors.brandBorder,
  },
  ghost: {
    bg: 'transparent',
    text: theme.colors.text.secondary,
    border: 'transparent',
  },
  danger: {
    bg: theme.colors.danger,
    text: theme.colors.text.inverse,
    border: theme.colors.danger,
  },
};

// ─── Input ──────────────────────────────────────────────────────────────────

export function Field({
  label,
  value,
  onChangeText,
  error,
  hint,
  required,
  multiline,
  ...rest
}: {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  error?: string;
  hint?: string;
  required?: boolean;
} & Omit<TextInputProps, 'value' | 'onChangeText' | 'style'>) {
  const [focused, setFocused] = useState(false);

  return (
    <View style={styles.field}>
      <View style={styles.fieldLabelRow}>
        <Txt variant="label" color={theme.colors.text.secondary}>
          {label}
        </Txt>
        {required ? (
          <Txt variant="label" color={theme.colors.danger}>
            {' *'}
          </Txt>
        ) : null}
      </View>

      <TextInput
        value={value}
        onChangeText={onChangeText}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        multiline={multiline}
        placeholderTextColor={theme.colors.text.muted}
        accessibilityLabel={label}
        style={[
          styles.input,
          multiline && styles.inputMultiline,
          focused && styles.inputFocused,
          error ? styles.inputError : null,
        ]}
        {...rest}
      />

      {error ? (
        <Txt variant="caption" color={theme.colors.danger} style={styles.fieldMessage}>
          {error}
        </Txt>
      ) : hint ? (
        <Txt variant="caption" color={theme.colors.text.muted} style={styles.fieldMessage}>
          {hint}
        </Txt>
      ) : null}
    </View>
  );
}

// ─── Chips ──────────────────────────────────────────────────────────────────

export function Chip({
  label,
  selected = false,
  onPress,
  tone,
}: {
  label: string;
  selected?: boolean;
  onPress?: () => void;
  tone?: { bg: string; text: string; border: string };
}) {
  const colors = selected
    ? { bg: theme.colors.brand, text: theme.colors.text.inverse, border: theme.colors.brand }
    : tone ?? {
        bg: theme.colors.surface.card,
        text: theme.colors.text.secondary,
        border: theme.colors.surface.border,
      };

  const body = (
    <View style={[styles.chip, { backgroundColor: colors.bg, borderColor: colors.border }]}>
      <Text style={[styles.chipLabel, { color: colors.text }]} numberOfLines={1}>
        {label}
      </Text>
    </View>
  );

  if (!onPress) return body;

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      accessibilityLabel={label}
      hitSlop={theme.hitSlop}
      style={({ pressed }) => [pressed && styles.pressed]}
    >
      {body}
    </Pressable>
  );
}

export function ChipRow({ children }: { children: React.ReactNode }) {
  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={styles.chipRow}
    >
      {children}
    </ScrollView>
  );
}

// ─── Progress ───────────────────────────────────────────────────────────────

export function ProgressBar({
  value,
  color,
  height = 8,
  showLabel = false,
}: {
  value: number;
  color?: string;
  height?: number;
  showLabel?: boolean;
}) {
  const clamped = Math.max(0, Math.min(100, value));
  const width = useRef(new Animated.Value(0)).current;

  React.useEffect(() => {
    Animated.timing(width, {
      toValue: clamped,
      duration: 400,
      useNativeDriver: false,
    }).start();
  }, [clamped, width]);

  const barColor = color ?? theme.colors.brand;

  return (
    <View>
      <View
        style={[styles.progressTrack, { height, borderRadius: height / 2 }]}
        accessibilityRole="progressbar"
        accessibilityValue={{ min: 0, max: 100, now: clamped }}
      >
        <Animated.View
          style={[
            styles.progressFill,
            {
              height,
              borderRadius: height / 2,
              backgroundColor: barColor,
              width: width.interpolate({
                inputRange: [0, 100],
                outputRange: ['0%', '100%'],
              }),
            },
          ]}
        />
      </View>
      {showLabel ? (
        <Txt variant="caption" color={theme.colors.text.secondary} style={styles.progressLabel}>
          {`${Math.round(clamped)}% concluído`}
        </Txt>
      ) : null}
    </View>
  );
}

// ─── Avatar ─────────────────────────────────────────────────────────────────

export function Avatar({ name, size = 40 }: { name: string; size?: number }) {
  const label = useMemo(() => {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }, [name]);

  return (
    <View
      style={[
        styles.avatar,
        { width: size, height: size, borderRadius: size / 2 },
      ]}
      accessibilityLabel={name}
    >
      <Text style={[styles.avatarLabel, { fontSize: size * 0.36 }]}>{label}</Text>
    </View>
  );
}

export function Divider({ spacing = theme.spacing.md }: { spacing?: number }) {
  return <View style={[styles.divider, { marginVertical: spacing }]} />;
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  flexGrow: { flexGrow: 1 },
  screen: { flex: 1, backgroundColor: theme.colors.surface.background },
  screenPadded: { paddingHorizontal: theme.spacing.lg },
  scrollContent: { paddingBottom: theme.spacing.xxxl, flexGrow: 1 },
  pressed: { opacity: 0.7 },

  card: {
    backgroundColor: theme.colors.surface.card,
    borderRadius: theme.radius.lg,
    borderWidth: 1,
    borderColor: theme.colors.surface.border,
    padding: theme.spacing.lg,
    ...theme.elevation.card,
  },

  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    justifyContent: 'space-between',
    marginTop: theme.spacing.xl,
    marginBottom: theme.spacing.md,
  },
  sectionSubtitle: { marginTop: theme.spacing.xxs },

  button: {
    minHeight: theme.minTouchTarget,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: theme.spacing.lg,
  },
  buttonCompact: { minHeight: 36, paddingHorizontal: theme.spacing.md },
  buttonDisabled: { opacity: 0.45 },
  buttonLabel: { fontSize: 15, fontWeight: '600' },
  buttonLabelCompact: { fontSize: 13 },

  field: { marginBottom: theme.spacing.lg },
  fieldLabelRow: { flexDirection: 'row', marginBottom: theme.spacing.xs },
  fieldMessage: { marginTop: theme.spacing.xs },
  input: {
    minHeight: theme.minTouchTarget,
    borderWidth: 1,
    borderColor: theme.colors.surface.inputBorder,
    borderRadius: theme.radius.md,
    paddingHorizontal: theme.spacing.md,
    paddingVertical: theme.spacing.md,
    fontSize: 15,
    color: theme.colors.text.primary,
    backgroundColor: theme.colors.surface.card,
  },
  inputMultiline: { minHeight: 110, textAlignVertical: 'top' },
  inputFocused: { borderColor: theme.colors.brand, borderWidth: 1.5 },
  inputError: { borderColor: theme.colors.danger },

  chip: {
    paddingHorizontal: theme.spacing.md,
    paddingVertical: theme.spacing.sm,
    borderRadius: theme.radius.full,
    borderWidth: 1,
    minHeight: 34,
    justifyContent: 'center',
  },
  chipLabel: { fontSize: 13, fontWeight: '600' },
  chipRow: { gap: theme.spacing.sm, paddingVertical: theme.spacing.xs },

  progressTrack: { backgroundColor: theme.colors.surface.sunken, overflow: 'hidden' },
  progressFill: {},
  progressLabel: { marginTop: theme.spacing.xs },

  avatar: {
    backgroundColor: theme.colors.brandSurface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarLabel: { color: theme.colors.text.brand, fontWeight: '700' },

  divider: { height: 1, backgroundColor: theme.colors.surface.border },
});

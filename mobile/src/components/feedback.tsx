import React, { useEffect, useRef } from 'react';
import {
  ActivityIndicator,
  Animated,
  Modal,
  Pressable,
  StyleSheet,
  View,
  type StyleProp,
  type ViewStyle,
} from 'react-native';
import { theme } from '../theme';
import { Button, Card, Txt } from './primitives';
import type { ApiError } from '../api/errors';

// ─── Loading ────────────────────────────────────────────────────────────────

/**
 * Skeleton placeholder.
 *
 * Preferred over a spinner for content that has a known shape: it keeps the layout stable,
 * so the screen does not jump when the data lands.
 */
export function Skeleton({
  width = '100%',
  height = 16,
  radius = theme.radius.sm,
  style,
}: {
  width?: number | `${number}%`;
  height?: number;
  radius?: number;
  style?: StyleProp<ViewStyle>;
}) {
  const pulse = useRef(new Animated.Value(0.4)).current;

  useEffect(() => {
    const animation = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, { toValue: 1, duration: 800, useNativeDriver: true }),
        Animated.timing(pulse, { toValue: 0.4, duration: 800, useNativeDriver: true }),
      ])
    );

    animation.start();
    return () => animation.stop();
  }, [pulse]);

  return (
    <Animated.View
      accessibilityLabel="Carregando"
      style={[
        styles.skeleton,
        { width, height, borderRadius: radius, opacity: pulse },
        style,
      ]}
    />
  );
}

/** Skeleton shaped like the list cards it replaces. */
export function SkeletonList({ count = 4 }: { count?: number }) {
  return (
    <View style={styles.skeletonList}>
      {Array.from({ length: count }).map((_, index) => (
        <View key={index} style={styles.skeletonCard}>
          <Skeleton width="45%" height={12} />
          <Skeleton width="85%" height={18} style={styles.skeletonGap} />
          <Skeleton width="60%" height={12} style={styles.skeletonGap} />
        </View>
      ))}
    </View>
  );
}

export function LoadingScreen({ label = 'Carregando…' }: { label?: string }) {
  return (
    <View style={styles.centered}>
      <ActivityIndicator size="large" color={theme.colors.brand} />
      <Txt variant="caption" color={theme.colors.text.secondary} style={styles.centeredText}>
        {label}
      </Txt>
    </View>
  );
}

// ─── Empty ──────────────────────────────────────────────────────────────────

/**
 * An empty state that tells the user what this screen is for and what to do next —
 * not just that there is nothing here.
 */
export function EmptyState({
  icon = '○',
  title,
  message,
  actionLabel,
  onAction,
}: {
  icon?: string;
  title: string;
  message: string;
  actionLabel?: string;
  onAction?: () => void;
}) {
  return (
    <View style={styles.centered}>
      <View style={styles.emptyIcon}>
        <Txt variant="heading" color={theme.colors.text.muted}>
          {icon}
        </Txt>
      </View>

      <Txt variant="subheading" align="center" style={styles.emptyTitle}>
        {title}
      </Txt>

      <Txt variant="body" color={theme.colors.text.secondary} align="center" style={styles.emptyMessage}>
        {message}
      </Txt>

      {actionLabel && onAction ? (
        <Button title={actionLabel} onPress={onAction} fullWidth={false} style={styles.emptyAction} />
      ) : null}
    </View>
  );
}

// ─── Error ──────────────────────────────────────────────────────────────────

/**
 * Error state with a retry.
 *
 * Only offers the retry when retrying could actually help: a 403 will still be a 403, and
 * a button that reliably does nothing erodes trust in every other button.
 */
export function ErrorState({
  error,
  onRetry,
  compact = false,
}: {
  error: ApiError | Error;
  onRetry?: () => void;
  compact?: boolean;
}) {
  const apiError = 'kind' in error ? (error as ApiError) : null;
  const canRetry = onRetry && (apiError ? apiError.retryable : true);

  return (
    <View style={compact ? styles.errorCompact : styles.centered}>
      {!compact ? (
        <View style={[styles.emptyIcon, styles.errorIcon]}>
          <Txt variant="heading" color={theme.colors.danger}>
            !
          </Txt>
        </View>
      ) : null}

      <Txt variant={compact ? 'label' : 'subheading'} align={compact ? 'left' : 'center'}>
        {compact ? error.message : 'Não foi possível carregar'}
      </Txt>

      {!compact ? (
        <Txt variant="body" color={theme.colors.text.secondary} align="center" style={styles.emptyMessage}>
          {error.message}
        </Txt>
      ) : null}

      {apiError?.traceId ? (
        <Txt variant="caption" color={theme.colors.text.muted} style={styles.traceId}>
          {`Código: ${apiError.traceId.slice(0, 12)}`}
        </Txt>
      ) : null}

      {canRetry ? (
        <Button
          title="Tentar novamente"
          onPress={onRetry}
          variant="secondary"
          fullWidth={false}
          compact={compact}
          style={styles.emptyAction}
        />
      ) : null}
    </View>
  );
}

/** Inline banner for a failure that should not replace the whole screen. */
export function ErrorBanner({ message, onDismiss }: { message: string; onDismiss?: () => void }) {
  return (
    <View style={styles.banner} accessibilityRole="alert">
      <Txt variant="caption" color={theme.colors.danger} style={styles.bannerText}>
        {message}
      </Txt>
      {onDismiss ? (
        <Pressable onPress={onDismiss} hitSlop={theme.hitSlop} accessibilityLabel="Fechar aviso">
          <Txt variant="caption" color={theme.colors.danger}>
            ✕
          </Txt>
        </Pressable>
      ) : null}
    </View>
  );
}

export function SuccessBanner({ message }: { message: string }) {
  return (
    <View style={[styles.banner, styles.bannerSuccess]} accessibilityRole="alert">
      <Txt variant="caption" color={theme.colors.success} style={styles.bannerText}>
        {message}
      </Txt>
    </View>
  );
}

// ─── Confirmation ───────────────────────────────────────────────────────────

/**
 * Explicit confirmation for anything destructive or irreversible.
 *
 * Blocking a project, cancelling one, deleting a draft and rejecting an idea all carry
 * consequences someone else will see, so none of them happen on a single tap.
 */
export function ConfirmDialog({
  visible,
  title,
  message,
  confirmLabel = 'Confirmar',
  cancelLabel = 'Cancelar',
  destructive = false,
  loading = false,
  onConfirm,
  onCancel,
  children,
}: {
  visible: boolean;
  title: string;
  message?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  children?: React.ReactNode;
}) {
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onCancel}>
      <View style={styles.overlay}>
        <Card style={styles.dialog}>
          <Txt variant="subheading">{title}</Txt>

          {message ? (
            <Txt variant="body" color={theme.colors.text.secondary} style={styles.dialogMessage}>
              {message}
            </Txt>
          ) : null}

          {children ? <View style={styles.dialogBody}>{children}</View> : null}

          <View style={styles.dialogActions}>
            <Button title={cancelLabel} onPress={onCancel} variant="ghost" disabled={loading} />
            <Button
              title={confirmLabel}
              onPress={onConfirm}
              variant={destructive ? 'danger' : 'primary'}
              loading={loading}
            />
          </View>
        </Card>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  skeleton: { backgroundColor: theme.colors.surface.sunken },
  skeletonList: { gap: theme.spacing.md, paddingTop: theme.spacing.md },
  skeletonCard: {
    backgroundColor: theme.colors.surface.card,
    borderRadius: theme.radius.lg,
    borderWidth: 1,
    borderColor: theme.colors.surface.border,
    padding: theme.spacing.lg,
  },
  skeletonGap: { marginTop: theme.spacing.sm },

  centered: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: theme.spacing.xl,
    minHeight: 240,
  },
  centeredText: { marginTop: theme.spacing.md },

  emptyIcon: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: theme.colors.surface.sunken,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: theme.spacing.lg,
  },
  errorIcon: { backgroundColor: theme.colors.dangerSurface },
  emptyTitle: { marginBottom: theme.spacing.sm },
  emptyMessage: { maxWidth: 300 },
  emptyAction: { marginTop: theme.spacing.xl },
  traceId: { marginTop: theme.spacing.sm },

  errorCompact: {
    padding: theme.spacing.lg,
    alignItems: 'flex-start',
    gap: theme.spacing.sm,
  },

  banner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: theme.colors.dangerSurface,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.colors.status.rejected.border,
    padding: theme.spacing.md,
    marginBottom: theme.spacing.md,
  },
  bannerSuccess: {
    backgroundColor: theme.colors.successSurface,
    borderColor: theme.colors.status.approved.border,
  },
  bannerText: { flex: 1 },

  overlay: {
    flex: 1,
    backgroundColor: theme.colors.surface.overlay,
    alignItems: 'center',
    justifyContent: 'center',
    padding: theme.spacing.xl,
  },
  dialog: { width: '100%', maxWidth: 420, ...theme.elevation.raised },
  dialogMessage: { marginTop: theme.spacing.sm },
  dialogBody: { marginTop: theme.spacing.lg },
  dialogActions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: theme.spacing.sm,
    marginTop: theme.spacing.xl,
  },
});

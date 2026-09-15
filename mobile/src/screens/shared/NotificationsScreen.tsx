import React, { useCallback, useState } from 'react';
import { FlatList, Pressable, StyleSheet, View } from 'react-native';
import {
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotifications,
} from '../../api/queries';
import { Button, Chip, ChipRow, Screen, Txt } from '../../components/primitives';
import { EmptyState, ErrorState, SkeletonList } from '../../components/feedback';
import { useRefreshControl } from '../../components/QueryView';
import { notificationTypeLabel } from '../../i18n/labels';
import { formatRelative } from '../../utils/format';
import { theme } from '../../theme';
import type { Notification } from '../../api/types';

/**
 * The notification centre lives inside Flow and is independent of any push provider, so it
 * keeps working whether or not push is configured on this build.
 */
export function NotificationsScreen() {
  const [unreadOnly, setUnreadOnly] = useState(false);

  const query = useNotifications(unreadOnly);
  const markRead = useMarkNotificationRead();
  const markAllRead = useMarkAllNotificationsRead();
  const refreshControl = useRefreshControl(query);

  const renderItem = useCallback(
    ({ item }: { item: Notification }) => (
      <NotificationRow
        notification={item}
        onPress={() => {
          if (!item.isRead) markRead.mutate(item.id);
        }}
      />
    ),
    [markRead]
  );

  if (query.isPending) {
    return (
      <Screen>
        <SkeletonList count={5} />
      </Screen>
    );
  }

  if (query.isError) {
    return (
      <Screen>
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      </Screen>
    );
  }

  const page = query.data;

  return (
    <Screen padded={false}>
      <View style={styles.header}>
        <ChipRow>
          <Chip label="Todas" selected={!unreadOnly} onPress={() => setUnreadOnly(false)} />
          <Chip
            label={page.unreadCount > 0 ? `Não lidas (${page.unreadCount})` : 'Não lidas'}
            selected={unreadOnly}
            onPress={() => setUnreadOnly(true)}
          />
        </ChipRow>

        {page.unreadCount > 0 ? (
          <Button
            title="Marcar todas como lidas"
            onPress={() => markAllRead.mutate()}
            variant="ghost"
            compact
            fullWidth={false}
            loading={markAllRead.isPending}
          />
        ) : null}
      </View>

      <FlatList
        data={page.items}
        keyExtractor={(item) => item.id}
        renderItem={renderItem}
        refreshControl={refreshControl}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        // Notification lists get long; windowing keeps scrolling smooth.
        initialNumToRender={12}
        maxToRenderPerBatch={12}
        windowSize={9}
        removeClippedSubviews
        ListEmptyComponent={
          <EmptyState
            icon="✓"
            title={unreadOnly ? 'Nada por ler' : 'Sem notificações'}
            message={
              unreadOnly
                ? 'Você está em dia. Novas notificações aparecem aqui.'
                : 'Avisos sobre suas ideias e projetos aparecem aqui.'
            }
          />
        }
      />
    </Screen>
  );
}

function NotificationRow({
  notification,
  onPress,
}: {
  notification: Notification;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${notification.title}. ${notification.isRead ? 'Lida' : 'Não lida'}`}
      style={({ pressed }) => [
        styles.row,
        !notification.isRead && styles.rowUnread,
        pressed && styles.pressed,
      ]}
    >
      <View style={styles.rowIndicator}>
        {!notification.isRead ? <View style={styles.unreadDot} /> : null}
      </View>

      <View style={styles.rowBody}>
        <View style={styles.rowHeader}>
          <Txt variant="overline" color={theme.colors.text.muted} numberOfLines={1} style={styles.flex}>
            {notificationTypeLabel[notification.type] ?? notification.type}
          </Txt>
          <Txt variant="caption" color={theme.colors.text.muted}>
            {formatRelative(notification.createdAt)}
          </Txt>
        </View>

        <Txt variant={notification.isRead ? 'body' : 'bodyStrong'} numberOfLines={2}>
          {notification.title}
        </Txt>

        {notification.body ? (
          <Txt variant="caption" color={theme.colors.text.secondary} numberOfLines={2} style={styles.rowText}>
            {notification.body}
          </Txt>
        ) : null}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  header: {
    paddingHorizontal: theme.spacing.lg,
    paddingBottom: theme.spacing.sm,
    gap: theme.spacing.xs,
  },
  list: { paddingHorizontal: theme.spacing.lg, paddingBottom: theme.spacing.xxxl },
  separator: { height: theme.spacing.sm },
  pressed: { opacity: 0.7 },

  row: {
    flexDirection: 'row',
    backgroundColor: theme.colors.surface.card,
    borderRadius: theme.radius.lg,
    borderWidth: 1,
    borderColor: theme.colors.surface.border,
    padding: theme.spacing.lg,
  },
  rowUnread: { borderColor: theme.colors.brandBorder, backgroundColor: theme.colors.brandSurface },
  rowIndicator: { width: 14, paddingTop: 6 },
  unreadDot: { width: 8, height: 8, borderRadius: 4, backgroundColor: theme.colors.brand },
  rowBody: { flex: 1 },
  rowHeader: { flexDirection: 'row', alignItems: 'center', gap: theme.spacing.sm },
  rowText: { marginTop: theme.spacing.xxs },
});

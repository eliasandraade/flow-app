import React from 'react';
import { RefreshControl, type RefreshControlProps } from 'react-native';
import type { UseQueryResult } from '@tanstack/react-query';
import type { ApiError } from '../api/errors';
import { EmptyState, ErrorState, SkeletonList } from './feedback';
import { theme } from '../theme';

/**
 * The loading / error / empty / content decision, made once.
 *
 * Every screen needs the same four branches, and writing them by hand twenty times is how
 * an app ends up with a screen that spins forever on a 403 or shows a blank page instead
 * of an explanation.
 */
export function QueryView<T>({
  query,
  children,
  empty,
  isEmpty,
  skeletonCount = 4,
}: {
  query: UseQueryResult<T, ApiError>;
  children: (data: T) => React.ReactNode;
  empty?: { icon?: string; title: string; message: string; actionLabel?: string; onAction?: () => void };
  isEmpty?: (data: T) => boolean;
  skeletonCount?: number;
}) {
  // A refetch keeps the previous content on screen; only the first load shows skeletons.
  if (query.isPending) return <SkeletonList count={skeletonCount} />;

  if (query.isError) {
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  }

  const data = query.data as T;

  if (empty && isEmpty?.(data)) {
    return (
      <EmptyState
        icon={empty.icon}
        title={empty.title}
        message={empty.message}
        actionLabel={empty.actionLabel}
        onAction={empty.onAction}
      />
    );
  }

  return <>{children(data)}</>;
}

/** Pull-to-refresh wired to a query, with the brand colour on both platforms. */
export function useRefreshControl(query: {
  refetch: () => unknown;
  isRefetching: boolean;
}): React.ReactElement<RefreshControlProps> {
  return (
    <RefreshControl
      refreshing={query.isRefetching}
      onRefresh={() => void query.refetch()}
      tintColor={theme.colors.brand}
      colors={[theme.colors.brand]}
    />
  );
}

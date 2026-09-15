export type StatusKey =
  | 'draft'
  | 'underReview'
  | 'approved'
  | 'rejected'
  | 'planned'
  | 'inProgress'
  | 'blocked'
  | 'completed'
  | 'cancelled';

const STATUS_MAP: Record<string, StatusKey> = {
  Draft:       'draft',
  UnderReview: 'underReview',
  Approved:    'approved',
  Rejected:    'rejected',
  Planned:     'planned',
  InProgress:  'inProgress',
  Blocked:     'blocked',
  Completed:   'completed',
  Cancelled:   'cancelled',
};

export function normalizeStatus(raw: string): StatusKey {
  const key = STATUS_MAP[raw];
  if (!key) {
    if (__DEV__) {
      console.warn(`[normalizeStatus] Unknown status: "${raw}". Falling back to 'draft'.`);
    }
    return 'draft';
  }
  return key;
}

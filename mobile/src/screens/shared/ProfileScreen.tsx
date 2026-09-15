import React, { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useAuthStore } from '../../store/authStore';
import { useMyPoints, useMyPointsLedger } from '../../api/queries';
import { Avatar, Button, Card, Divider, Screen, SectionHeader, Txt } from '../../components/primitives';
import { ConfirmDialog } from '../../components/feedback';
import { MetricRow } from '../../components/domain';
import { roleLabel } from '../../i18n/labels';
import { formatRelative } from '../../utils/format';
import { config } from '../../config/env';
import { theme } from '../../theme';

export function ProfileScreen() {
  const session = useAuthStore((state) => state.session);
  const signOut = useAuthStore((state) => state.signOut);

  const [confirmingSignOut, setConfirmingSignOut] = useState(false);
  const [signingOut, setSigningOut] = useState(false);

  const isOperator = session?.role === 'Operator';
  const points = useMyPoints({ enabled: isOperator });
  const ledger = useMyPointsLedger({ enabled: isOperator });

  if (!session) return null;

  async function handleSignOut() {
    setSigningOut(true);
    try {
      await signOut();
    } finally {
      setSigningOut(false);
      setConfirmingSignOut(false);
    }
  }

  return (
    <Screen scroll>
      <Card style={styles.identity}>
        <Avatar name={session.name} size={64} />
        <View style={styles.identityText}>
          <Txt variant="subheading">{session.name}</Txt>
          <Txt variant="body" color={theme.colors.text.secondary}>
            {session.email}
          </Txt>
          <View style={styles.roleTag}>
            <Txt variant="caption" color={theme.colors.text.brand}>
              {roleLabel[session.role]}
            </Txt>
          </View>
        </View>
      </Card>

      {isOperator ? (
        <>
          <SectionHeader title="Reconhecimento" subtitle="Pontos ganhos com ideias aprovadas" />

          <Card>
            <View style={styles.pointsRow}>
              <Txt variant="body" color={theme.colors.text.secondary}>
                Pontos acumulados
              </Txt>
              <Txt variant="kpi" color={theme.colors.text.brand}>
                {points.data?.points ?? 0}
              </Txt>
            </View>

            {ledger.data && ledger.data.length > 0 ? (
              <>
                <Divider />
                {ledger.data.slice(0, 6).map((entry) => (
                  <MetricRow
                    key={entry.id}
                    label={entry.reason === 'Idea approved' ? 'Ideia aprovada' : entry.reason}
                    hint={formatRelative(entry.awardedAt)}
                    value={`+${entry.points}`}
                  />
                ))}
              </>
            ) : (
              <>
                <Divider />
                <Txt variant="caption" color={theme.colors.text.muted}>
                  Ideias aprovadas rendem pontos. O histórico aparece aqui.
                </Txt>
              </>
            )}
          </Card>
        </>
      ) : null}

      <SectionHeader title="Aplicativo" />

      <Card>
        <MetricRow label="Ambiente" value={config.environment} />
        <Divider spacing={theme.spacing.sm} />
        <MetricRow label="Servidor" value={config.apiBaseUrl.replace(/^https?:\/\//, '')} />
        <Divider spacing={theme.spacing.sm} />
        <MetricRow
          label="Notificações push"
          value={config.oneSignalAppId ? 'Configuradas' : 'Não configuradas'}
          hint={
            config.oneSignalAppId
              ? undefined
              : 'A central de notificações no app funciona normalmente.'
          }
        />
      </Card>

      <Button
        title="Sair da conta"
        onPress={() => setConfirmingSignOut(true)}
        variant="secondary"
        style={styles.signOut}
      />

      <ConfirmDialog
        visible={confirmingSignOut}
        title="Sair da conta"
        message="Você precisará entrar novamente para acessar o Flow."
        confirmLabel="Sair"
        destructive
        loading={signingOut}
        onConfirm={handleSignOut}
        onCancel={() => setConfirmingSignOut(false)}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  identity: { flexDirection: 'row', alignItems: 'center', gap: theme.spacing.lg, marginTop: theme.spacing.lg },
  identityText: { flex: 1, gap: theme.spacing.xxs },
  roleTag: {
    alignSelf: 'flex-start',
    backgroundColor: theme.colors.brandSurface,
    borderRadius: theme.radius.full,
    paddingHorizontal: theme.spacing.sm,
    paddingVertical: 3,
    marginTop: theme.spacing.xs,
  },
  pointsRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  signOut: { marginTop: theme.spacing.xxl },
});

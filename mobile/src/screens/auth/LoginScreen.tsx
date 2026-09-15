import React, { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuthStore } from '../../store/authStore';
import { Button, Field, Txt } from '../../components/primitives';
import { ErrorBanner } from '../../components/feedback';
import { toApiError, type ApiError } from '../../api/errors';
import { theme } from '../../theme';
import { config } from '../../config/env';

type Mode = 'login' | 'register';

export function LoginScreen() {
  const signIn = useAuthStore((state) => state.signIn);
  const register = useAuthStore((state) => state.register);

  const [mode, setMode] = useState<Mode>('login');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const isRegister = mode === 'register';

  async function submit() {
    setError(null);
    setSubmitting(true);

    try {
      if (isRegister) {
        await register(name.trim(), email.trim(), password);
      } else {
        await signIn(email.trim(), password);
      }
    } catch (caught) {
      setError(toApiError(caught));
    } finally {
      setSubmitting(false);
    }
  }

  function switchMode() {
    setMode(isRegister ? 'login' : 'register');
    setError(null);
    setPassword('');
  }

  const canSubmit =
    email.trim().length > 0 && password.length > 0 && (!isRegister || name.trim().length > 0);

  return (
    <SafeAreaView style={styles.screen}>
      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView
          contentContainerStyle={styles.content}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          <View style={styles.brand}>
            <View style={styles.logo}>
              <Txt variant="heading" color={theme.colors.text.inverse}>
                F
              </Txt>
            </View>
            <Txt variant="display" style={styles.brandName}>
              Flow
            </Txt>
            <Txt variant="body" color={theme.colors.text.secondary} align="center">
              Da ideia ao resultado, com rastreabilidade.
            </Txt>
          </View>

          <View style={styles.form}>
            <Txt variant="subheading" style={styles.formTitle}>
              {isRegister ? 'Criar conta' : 'Entrar'}
            </Txt>

            {error ? <ErrorBanner message={error.message} onDismiss={() => setError(null)} /> : null}

            {isRegister ? (
              <Field
                label="Nome"
                value={name}
                onChangeText={setName}
                placeholder="Seu nome completo"
                autoCapitalize="words"
                autoComplete="name"
                required
                error={error?.fieldError('Name')}
              />
            ) : null}

            <Field
              label="E-mail"
              value={email}
              onChangeText={setEmail}
              placeholder="voce@empresa.com"
              keyboardType="email-address"
              autoCapitalize="none"
              autoCorrect={false}
              autoComplete="email"
              required
              error={error?.fieldError('Email')}
            />

            <Field
              label="Senha"
              value={password}
              onChangeText={setPassword}
              placeholder={isRegister ? 'Mínimo de 8 caracteres' : 'Sua senha'}
              secureTextEntry
              autoCapitalize="none"
              autoComplete={isRegister ? 'new-password' : 'current-password'}
              required
              error={error?.fieldError('Password') ?? error?.fieldError('PasswordTooShort')}
              hint={isRegister ? 'Use ao menos 8 caracteres, com um número.' : undefined}
              onSubmitEditing={canSubmit ? submit : undefined}
              returnKeyType="go"
            />

            <Button
              title={isRegister ? 'Criar conta' : 'Entrar'}
              onPress={submit}
              loading={submitting}
              disabled={!canSubmit}
            />

            <Pressable
              onPress={switchMode}
              hitSlop={theme.hitSlop}
              style={styles.switchMode}
              accessibilityRole="button"
            >
              <Txt variant="body" color={theme.colors.text.brand} align="center">
                {isRegister ? 'Já tenho conta. Entrar' : 'Não tenho conta. Criar agora'}
              </Txt>
            </Pressable>

            {isRegister ? (
              <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.roleNote}>
                Novas contas entram como Operador. Perfis de gestão são concedidos pela
                administração.
              </Txt>
            ) : null}
          </View>

          {!config.isProduction ? (
            <Txt variant="caption" color={theme.colors.text.muted} align="center" style={styles.envNote}>
              {`${config.environment} · ${config.apiBaseUrl}`}
            </Txt>
          ) : null}
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  screen: { flex: 1, backgroundColor: theme.colors.surface.background },
  content: {
    flexGrow: 1,
    justifyContent: 'center',
    padding: theme.spacing.xl,
  },
  brand: { alignItems: 'center', marginBottom: theme.spacing.xxl },
  logo: {
    width: 64,
    height: 64,
    borderRadius: theme.radius.xl,
    backgroundColor: theme.colors.brand,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: theme.spacing.lg,
    ...theme.elevation.raised,
  },
  brandName: { marginBottom: theme.spacing.xs },
  form: {
    backgroundColor: theme.colors.surface.card,
    borderRadius: theme.radius.xl,
    borderWidth: 1,
    borderColor: theme.colors.surface.border,
    padding: theme.spacing.xl,
    ...theme.elevation.card,
  },
  formTitle: { marginBottom: theme.spacing.lg },
  switchMode: { marginTop: theme.spacing.lg, minHeight: theme.minTouchTarget, justifyContent: 'center' },
  roleNote: { marginTop: theme.spacing.sm },
  envNote: { marginTop: theme.spacing.xl },
});

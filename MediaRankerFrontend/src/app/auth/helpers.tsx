"use client";
import {
  fetchAuthSession,
  getCurrentUser,
  signIn,
  signOut,
  signUp,
  confirmSignUp,
  resendSignUpCode,
  resetPassword,
  confirmResetPassword,
  updatePassword,
  SignUpOutput,
  SignInOutput,
  ConfirmSignUpOutput,
  ResendSignUpCodeOutput,
  ResetPasswordOutput,
} from "aws-amplify/auth";
import {
  clearLocalTestAuth,
  isLocalTestAuthActive,
  LOCAL_TEST_AUTH_TOKEN,
} from "@/lib/auth/local-test-auth";
import {
  getPasswordUpdateError,
  getRecoveryRequestErrorOutcome,
  getResetConfirmationError,
} from "@/lib/auth/password-decisions";

export type AuthResult<T> = {
  success: boolean;
  error?: string;
  data?: T;
};

export async function handleSignup(form: {
  username: string;
  email: string;
  password: string;
}): Promise<AuthResult<SignUpOutput>> {
  const { username, email, password } = form;

  try {
    const result = await signUp({
      username,
      password,
      options: {
        userAttributes: {
          email,
        },
      },
    });

    return { success: true, data: result };
  } catch (err: EXPLICIT_ANY) {
    return {
      success: false,
      error: err?.message || "Failed to sign up. Please try again.",
    };
  }
}

export async function handleLogin(form: {
  usernameOrEmail: string;
  password: string;
}): Promise<AuthResult<SignInOutput>> {
  try {
    const result = await signIn({
      username: form.usernameOrEmail,
      password: form.password,
    });

    return { success: true, data: result };
  } catch (err: EXPLICIT_ANY) {
    return {
      success: false,
      error: err?.message || "Failed to log in. Please check your credentials.",
    };
  }
}

export async function isAuthenticated(): Promise<boolean> {
  try {
    await getCurrentUser();
    return true;
  } catch {
    return false;
  }
}

export async function getAccessToken(): Promise<string | undefined> {
  if (isLocalTestAuthActive()) {
    return LOCAL_TEST_AUTH_TOKEN;
  }

  const session = await fetchAuthSession();
  return session.tokens?.accessToken?.toString();
}

export async function handleConfirmSignup(
  username: string,
  code: string,
): Promise<AuthResult<ConfirmSignUpOutput>> {
  try {
    const result = await confirmSignUp({
      username,
      confirmationCode: code,
    });

    return { success: true, data: result };
  } catch (err: EXPLICIT_ANY) {
    return {
      success: false,
      error:
        err?.message || "Failed to confirm signup. Please check your code.",
    };
  }
}

export async function handleResendCode(
  username: string,
): Promise<AuthResult<ResendSignUpCodeOutput>> {
  try {
    const result = await resendSignUpCode({
      username,
    });

    return { success: true, data: result };
  } catch (err: EXPLICIT_ANY) {
    return {
      success: false,
      error: err?.message || "Failed to resend code. Please try again.",
    };
  }
}

export async function handleSignOut(): Promise<AuthResult<void>> {
  if (isLocalTestAuthActive()) {
    clearLocalTestAuth();
    return { success: true, data: undefined };
  }

  try {
    await signOut();
    return { success: true, data: undefined };
  } catch (err: EXPLICIT_ANY) {
    return {
      success: false,
      error: err?.message || "Failed to sign out. Please try again.",
    };
  }
}

function getErrorName(error: unknown): string | undefined {
  if (typeof error === "object" && error !== null && "name" in error) {
    return typeof error.name === "string" ? error.name : undefined;
  }
  return undefined;
}

export async function handleResetPassword(
  identifier: string,
): Promise<AuthResult<ResetPasswordOutput | undefined>> {
  try {
    const result = await resetPassword({ username: identifier });
    return { success: true, data: result };
  } catch (error: unknown) {
    if (getRecoveryRequestErrorOutcome(getErrorName(error)) === "neutral") {
      return { success: true };
    }
    return {
      success: false,
      error: "We couldn't start password recovery. Please try again.",
    };
  }
}

export async function handleConfirmResetPassword(
  identifier: string,
  code: string,
  newPassword: string,
): Promise<AuthResult<void>> {
  try {
    await confirmResetPassword({
      username: identifier,
      confirmationCode: code,
      newPassword,
    });
    return { success: true };
  } catch (error: unknown) {
    return {
      success: false,
      error: getResetConfirmationError(getErrorName(error)),
    };
  }
}

export async function handleUpdatePassword(
  currentPassword: string,
  newPassword: string,
): Promise<AuthResult<void>> {
  if (isLocalTestAuthActive()) {
    return {
      success: false,
      error: "Password changes aren't available for the local test user.",
    };
  }

  try {
    await updatePassword({ oldPassword: currentPassword, newPassword });
    return { success: true };
  } catch (error: unknown) {
    return {
      success: false,
      error: getPasswordUpdateError(getErrorName(error)),
    };
  }
}

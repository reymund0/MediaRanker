"use client";
import {
  fetchAuthSession,
  getCurrentUser,
  signIn,
  signOut,
  signUp,
  confirmSignUp,
  resendSignUpCode,
  SignUpOutput,
  SignInOutput,
  ConfirmSignUpOutput,
  ResendSignUpCodeOutput,
} from "aws-amplify/auth";

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

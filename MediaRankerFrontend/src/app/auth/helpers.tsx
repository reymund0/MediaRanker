"use client";
import { fetchAuthSession, getCurrentUser, signIn, signUp, confirmSignUp, resendSignUpCode } from "aws-amplify/auth";

export type AuthResult = {
  success: boolean;
  error?: string;
  data?: any;
};

export async function handleSignup(form: { username: string; email: string; password: string }): Promise<AuthResult> {
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
  } catch (err: any) {
    return { 
      success: false, 
      error: err.message || "Failed to sign up. Please try again." 
    };
  }
}

export async function handleLogin(form: { usernameOrEmail: string; password: string }): Promise<AuthResult> {
  try {
    const user = await signIn({
      username: form.usernameOrEmail,
      password: form.password,
    });

    return { success: true, data: user };
  } catch (err: any) {
    return { 
      success: false, 
      error: err.message || "Failed to log in. Please check your credentials." 
    };
  }
}

export async function isAuthenticated(): Promise<boolean> {
  try {
    await getCurrentUser();
    return true;
  } catch (err) {
    return false;
  }
}

export async function getAccessToken(): Promise<string | undefined> {
  const session = await fetchAuthSession();
  return session.tokens?.accessToken?.toString();
}

export async function handleConfirmSignup(username: string, code: string): Promise<AuthResult> {
  try {
    await confirmSignUp({
      username,
      confirmationCode: code,
    });

    return { success: true };
  } catch (err: any) {
    return { 
      success: false, 
      error: err.message || "Failed to confirm signup. Please check your code." 
    };
  }
}

export async function handleResendCode(username: string): Promise<AuthResult> {
  try {
    await resendSignUpCode({
      username,
    });

    return { success: true };
  } catch (err: any) {
    return { 
      success: false, 
      error: err.message || "Failed to resend code. Please try again." 
    };
  }
}
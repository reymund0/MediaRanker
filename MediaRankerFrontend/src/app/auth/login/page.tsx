"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Box, Card, CardContent, Typography, Link, NoSsr } from "@mui/material";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { handleLogin } from "../helpers";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import {
  activateLocalTestAuth,
  isLocalTestAuthAvailable,
} from "@/lib/auth/local-test-auth";
import {
  getSignInDestination,
  saveResetIdentifier,
} from "@/lib/auth/password-decisions";

const loginSchema = z.object({
  usernameOrEmail: z.string().min(1, "Username or email is required"),
  password: z.string().min(1, "Password is required"),
});

type LoginFormData = z.infer<typeof loginSchema>;

function currentTimeMs(): number {
  return Date.now();
}

export default function Login() {
  const router = useRouter();
  const { showError, showSuccess, closeAlert } = useAlert();
  const [loading, setLoading] = useState(false);
  const [confirmSignupNeeded, setConfirmSignupNeeded] = useState(false);
  const localTestAuthAvailable = isLocalTestAuthAvailable();

  const methods = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      usernameOrEmail: "",
      password: "",
    },
  });

  const onSubmit = async (data: LoginFormData) => {
    closeAlert();
    setConfirmSignupNeeded(false);
    setLoading(true);

    const result = await handleLogin(data);

    if (!result.success || !result.data) {
      showError(result.error || "Login failed");
      setLoading(false);
      return;
    }

    switch (getSignInDestination(result.data)) {
      case "authenticated":
        showSuccess("Login successful! Redirecting to home...");
        setTimeout(() => {
          router.push("/reviews");
        }, 2000);
        break;
      case "reset-password":
        try {
          saveResetIdentifier(
            window.sessionStorage,
            data.usernameOrEmail,
            currentTimeMs(),
          );
        } catch {
          // The recovery page remains usable when tab storage is unavailable.
        }
        router.push("/auth/reset-password");
        break;
      case "confirm-signup":
        showError("Confirm your account before signing in.");
        setConfirmSignupNeeded(true);
        setLoading(false);
        break;
      default:
        showError(
          "This sign-in needs an additional verification step that isn't available here yet. Contact the site owner for help.",
        );
        setLoading(false);
    }
  };

  const useLocalTestUser = () => {
    closeAlert();
    activateLocalTestAuth();
    router.push("/reviews");
  };

  return (
    <FormProvider {...methods}>
      <Box
        sx={{
          minHeight: "100vh",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <Card sx={{ width: "100%", maxWidth: 400 }}>
          <CardContent
            component="form"
            onSubmit={methods.handleSubmit(onSubmit)}
            sx={{ display: "flex", flexDirection: "column", gap: 2 }}
          >
            <Typography variant="h4" component="h1" gutterBottom align="center">
              Login
            </Typography>

            <FormTextField<LoginFormData>
              name="usernameOrEmail"
              label="Username or Email"
              autoComplete="username"
            />

            <FormTextField<LoginFormData>
              name="password"
              label="Password"
              type="password"
              autoComplete="current-password"
            />

            <Typography variant="body2" align="right">
              <Link href="/auth/reset-password">Forgot password?</Link>
            </Typography>

            {confirmSignupNeeded && (
              <Typography variant="body2" align="center">
                <Link href="/auth/confirm-signup">Confirm your account</Link>
              </Typography>
            )}

            <PrimaryButton
              type="submit"
              fullWidth
              disabled={loading}
              sx={{ mt: 3, mb: 2 }}
            >
              {loading ? "Logging in..." : "Login"}
            </PrimaryButton>

            <NoSsr>
              {localTestAuthAvailable && (
                <PrimaryButton type="button" fullWidth onClick={useLocalTestUser}>
                  Use local test user
                </PrimaryButton>
              )}
            </NoSsr>

            <Typography variant="body2" align="center">
              {`Don't have an account? `}
              <Link href="/auth/signup">Sign up</Link>
            </Typography>
          </CardContent>
        </Card>
      </Box>
    </FormProvider>
  );
}

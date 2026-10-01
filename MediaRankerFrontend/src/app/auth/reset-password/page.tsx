"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Box,
  Card,
  CardContent,
  Link,
  TextField,
  Typography,
} from "@mui/material";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { handleConfirmResetPassword, handleResetPassword } from "../helpers";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { SecondaryButton } from "@/lib/components/inputs/button/secondary-button";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { useUser } from "@/lib/auth/user-provider";
import { consumeResetIdentifier } from "@/lib/auth/password-decisions";

const requestSchema = z.object({
  identifier: z.string().min(1, "Username or email is required"),
});

const confirmSchema = z
  .object({
    code: z.string().min(1, "Recovery code is required"),
    newPassword: z.string().min(8, "Password must be at least 8 characters"),
    confirmPassword: z.string().min(1, "Confirm password is required"),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });

type RequestFormData = z.infer<typeof requestSchema>;
type ConfirmFormData = z.infer<typeof confirmSchema>;
type ResetStep = "request" | "confirm" | "complete";
type PendingAction = "request" | "resend" | "confirm";

const neutralCodeMessage =
  "If an account can be recovered, a code may arrive at its verified recovery channel. If it does not arrive, wait a few minutes before requesting another.";

export default function ResetPassword() {
  const router = useRouter();
  const { authResolved, isAuthenticated } = useUser();
  const { showError, showSuccess, closeAlert } = useAlert();
  const [step, setStep] = useState<ResetStep>("request");
  const [identifier, setIdentifier] = useState("");
  const [pendingAction, setPendingAction] = useState<PendingAction>();
  const isBusy = pendingAction !== undefined;

  const requestMethods = useForm<RequestFormData>({
    resolver: zodResolver(requestSchema),
    defaultValues: { identifier: "" },
  });
  const confirmMethods = useForm<ConfirmFormData>({
    resolver: zodResolver(confirmSchema),
    defaultValues: { code: "", newPassword: "", confirmPassword: "" },
  });
  const { setValue: setRequestValue } = requestMethods;
  const { setFocus: focusConfirmField } = confirmMethods;

  useEffect(() => {
    try {
      const handoffIdentifier = consumeResetIdentifier(
        window.sessionStorage,
        Date.now(),
      );
      if (handoffIdentifier !== undefined) {
        setRequestValue("identifier", handoffIdentifier);
      }
    } catch {
      // The recovery form remains usable when browser storage is unavailable.
    }
  }, [setRequestValue]);

  useEffect(() => {
    if (step === "confirm") {
      focusConfirmField("code");
    }
  }, [focusConfirmField, step]);

  useEffect(() => {
    if (authResolved && isAuthenticated) {
      router.replace("/reviews");
    }
  }, [authResolved, isAuthenticated, router]);

  const showComplete = () => {
    setStep("complete");
    showSuccess("Password reset. Return to login to sign in.");
  };

  const onRequestCode = async (data: RequestFormData) => {
    closeAlert();
    setPendingAction("request");
    setIdentifier(data.identifier);

    try {
      const result = await handleResetPassword(data.identifier);
      if (!result.success) {
        showError(
          result.error || "Unable to request a reset code. Please try again.",
        );
        return;
      }

      if (result.data?.nextStep?.resetPasswordStep === "DONE") {
        showComplete();
        return;
      }

      setStep("confirm");
    } catch {
      showError("Unable to request a reset code. Please try again.");
    } finally {
      setPendingAction(undefined);
    }
  };

  const onResendCode = async () => {
    closeAlert();
    setPendingAction("resend");

    try {
      const result = await handleResetPassword(identifier);
      if (!result.success) {
        showError(
          result.error || "Unable to request a reset code. Please try again.",
        );
        return;
      }

      if (result.data?.nextStep?.resetPasswordStep === "DONE") {
        showComplete();
        return;
      }

      showSuccess(neutralCodeMessage);
    } catch {
      showError("Unable to request a reset code. Please try again.");
    } finally {
      setPendingAction(undefined);
    }
  };

  const onConfirmReset = async (data: ConfirmFormData) => {
    closeAlert();
    setPendingAction("confirm");

    try {
      const result = await handleConfirmResetPassword(
        identifier,
        data.code,
        data.newPassword,
      );
      if (result.success) {
        confirmMethods.reset();
        showComplete();
      } else {
        showError(
          result.error ||
            "Unable to reset the password. Check the code and try again.",
        );
      }
    } catch {
      showError("Unable to reset the password. Check the code and try again.");
    } finally {
      setPendingAction(undefined);
    }
  };

  const onChangeIdentifier = () => {
    confirmMethods.reset();
    setStep("request");
  };

  if (!authResolved || isAuthenticated) {
    return <Box sx={{ minHeight: "100vh" }} />;
  }

  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <Card sx={{ width: "100%", maxWidth: 400 }}>
        {step === "request" && (
          <FormProvider {...requestMethods}>
            <CardContent
              component="form"
              onSubmit={requestMethods.handleSubmit(onRequestCode)}
              sx={{ display: "flex", flexDirection: "column", gap: 2 }}
            >
              <Typography
                variant="h4"
                component="h1"
                gutterBottom
                align="center"
              >
                Reset password
              </Typography>
              <Typography variant="body2" color="text.secondary" align="center">
                Enter your username or email address to start password recovery.
              </Typography>
              <FormTextField<RequestFormData>
                name="identifier"
                label="Username or email"
                autoComplete="username"
                autoFocus
              />
              <PrimaryButton type="submit" fullWidth disabled={isBusy}>
                {pendingAction === "request"
                  ? "Sending..."
                  : "Send recovery code"}
              </PrimaryButton>
              <Typography variant="body2" align="center">
                <Link href="/auth/login">Back to login</Link>
              </Typography>
            </CardContent>
          </FormProvider>
        )}

        {step === "confirm" && (
          <FormProvider {...confirmMethods}>
            <CardContent
              component="form"
              onSubmit={confirmMethods.handleSubmit(onConfirmReset)}
              sx={{ display: "flex", flexDirection: "column", gap: 2 }}
            >
              <Typography
                variant="h4"
                component="h1"
                gutterBottom
                align="center"
              >
                Choose a new password
              </Typography>
              <Typography variant="body2" color="text.secondary" align="center">
                {neutralCodeMessage}
              </Typography>
              <FormTextField<ConfirmFormData>
                name="code"
                label="Recovery code"
                autoComplete="one-time-code"
              />
              <FormTextField<ConfirmFormData>
                name="newPassword"
                label="New password"
                type="password"
                autoComplete="new-password"
              />
              <FormTextField<ConfirmFormData>
                name="confirmPassword"
                label="Confirm new password"
                type="password"
                autoComplete="new-password"
              />
              <TextField
                label="Username or email"
                value={identifier}
                fullWidth
                InputProps={{ readOnly: true }}
              />
              <PrimaryButton type="submit" fullWidth disabled={isBusy}>
                {pendingAction === "confirm"
                  ? "Resetting..."
                  : "Reset password"}
              </PrimaryButton>
              <SecondaryButton
                type="button"
                fullWidth
                onClick={onResendCode}
                disabled={isBusy}
              >
                {pendingAction === "resend" ? "Sending..." : "Resend code"}
              </SecondaryButton>
              <SecondaryButton
                type="button"
                fullWidth
                onClick={onChangeIdentifier}
                disabled={isBusy}
              >
                Use a different account
              </SecondaryButton>
              <Typography variant="body2" align="center">
                <Link href="/auth/login">Back to login</Link>
              </Typography>
            </CardContent>
          </FormProvider>
        )}

        {step === "complete" && (
          <CardContent
            sx={{ display: "flex", flexDirection: "column", gap: 2 }}
          >
            <Typography variant="h4" component="h1" gutterBottom align="center">
              Password reset complete
            </Typography>
            <Typography variant="body2" color="text.secondary" align="center">
              Return to login to sign in with your password.
            </Typography>
            <PrimaryButton
              href="/auth/login"
              fullWidth
            >
              Return to login
            </PrimaryButton>
          </CardContent>
        )}
      </Card>
    </Box>
  );
}

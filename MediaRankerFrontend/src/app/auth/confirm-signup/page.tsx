"use client";
import { useState, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Box, Card, CardContent, Typography, Alert } from "@mui/material";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { handleConfirmSignup, handleResendCode } from "../helpers";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { SecondaryButton } from "@/lib/components/inputs/button/secondary-button";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { NextLink } from "@/lib/components/navigation/next-link";

const confirmSignupSchema = z.object({
  username: z.string().min(1, "Username is required"),
  code: z.string().min(1, "Confirmation code is required"),
});

type ConfirmSignupFormData = z.infer<typeof confirmSignupSchema>;

export default function ConfirmSignup() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);

  const methods = useForm<ConfirmSignupFormData>({
    resolver: zodResolver(confirmSignupSchema),
    defaultValues: {
      username: "",
      code: "",
    },
  });

  useEffect(() => {
    const usernameParam = searchParams.get("username");
    if (usernameParam) {
      methods.setValue("username", usernameParam);
    }
  }, [searchParams, methods]);

  const onSubmit = async (data: ConfirmSignupFormData) => {
    setError("");
    setSuccess("");
    setLoading(true);

    const result = await handleConfirmSignup(data.username, data.code);

    if (result.success) {
      setSuccess("Account confirmed! Redirecting to login...");
      setTimeout(() => {
        router.push("/auth/login");
      }, 2000);
    } else {
      setError(result.error || "Confirmation failed");
      setLoading(false);
    }
  };

  const onResendCode = async () => {
    const username = methods.getValues("username");
    setError("");
    setSuccess("");
    setResending(true);

    const result = await handleResendCode(username);

    if (result.success) {
      setSuccess("Confirmation code resent! Check your email.");
    } else {
      setError(result.error || "Failed to resend code");
    }

    setResending(false);
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
              Confirm Signup
            </Typography>

            <Typography
              variant="body2"
              color="text.secondary"
              sx={{ mb: 2 }}
              align="center"
            >
              Enter the confirmation code sent to your email
            </Typography>

            {error && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {error}
              </Alert>
            )}

            {success && (
              <Alert severity="success" sx={{ mb: 2 }}>
                {success}
              </Alert>
            )}

            <FormTextField<ConfirmSignupFormData>
              name="username"
              label="Username"
              autoComplete="username"
            />

            <FormTextField<ConfirmSignupFormData>
              name="code"
              label="Confirmation Code"
              autoComplete="off"
            />

            <PrimaryButton
              type="submit"
              fullWidth
              disabled={loading}
              sx={{ mt: 2 }}
            >
              {loading ? "Confirming..." : "Confirm"}
            </PrimaryButton>

            <SecondaryButton
              fullWidth
              onClick={onResendCode}
              disabled={resending || !methods.getValues("username")}
            >
              {resending ? "Resending..." : "Resend Code"}
            </SecondaryButton>

            <Typography variant="body2" align="center">
              Already confirmed? <NextLink href="/auth/login">Login</NextLink>
            </Typography>
          </CardContent>
        </Card>
      </Box>
    </FormProvider>
  );
}

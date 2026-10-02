"use client";

import { PageContainer } from "@/lib/components/layout/page-container";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Box, Card, CardContent, Link, Typography } from "@mui/material";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { handleUpdatePassword } from "@/app/auth/helpers";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { useUser } from "@/lib/auth/user-provider";

const changePasswordSchema = z
  .object({
    username: z.string(),
    currentPassword: z.string().min(1, "Current password is required"),
    newPassword: z.string().min(8, "Password must be at least 8 characters"),
    confirmPassword: z.string().min(1, "Confirm password is required"),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });

type ChangePasswordFormData = z.infer<typeof changePasswordSchema>;

function ChangePasswordForm({ username }: { username: string }) {
  const { showError, showSuccess, closeAlert } = useAlert();
  const [loading, setLoading] = useState(false);
  const methods = useForm<ChangePasswordFormData>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: {
      username,
      currentPassword: "",
      newPassword: "",
      confirmPassword: "",
    },
  });

  const onSubmit = async (data: ChangePasswordFormData) => {
    closeAlert();
    setLoading(true);

    try {
      const result = await handleUpdatePassword(
        data.currentPassword,
        data.newPassword,
      );
      if (result.success) {
        methods.reset({
          username,
          currentPassword: "",
          newPassword: "",
          confirmPassword: "",
        });
        showSuccess("Password updated successfully.");
      } else {
        showError(
          result.error || "Unable to update the password. Please try again.",
        );
      }
    } catch {
      showError("Unable to update the password. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <FormProvider {...methods}>
      <PageContainer>
        <Card sx={{ width: "100%", maxWidth: 560, p: 3 }}>
          <CardContent
            component="form"
            onSubmit={methods.handleSubmit(onSubmit)}
            sx={{ display: "flex", flexDirection: "column", gap: 2 }}
          >
            <Typography variant="h4" component="h1" gutterBottom align="center">
              Change password
            </Typography>
            <FormTextField<ChangePasswordFormData>
              labelAbove
              name="username"
              label="Username"
              autoComplete="username"
              InputProps={{ readOnly: true }}
            />
            <FormTextField<ChangePasswordFormData>
              labelAbove
              name="currentPassword"
              label="Current password"
              type="password"
              autoComplete="current-password"
            />
            <FormTextField<ChangePasswordFormData>
              labelAbove
              name="newPassword"
              label="New password"
              type="password"
              autoComplete="new-password"
            />
            <FormTextField<ChangePasswordFormData>
              labelAbove
              name="confirmPassword"
              label="Confirm new password"
              type="password"
              autoComplete="new-password"
            />
            <PrimaryButton type="submit" fullWidth disabled={loading}>
              {loading ? "Updating..." : "Update password"}
            </PrimaryButton>
            <Typography variant="body2" align="center">
              <Link href="/reviews">Back to reviews</Link>
            </Typography>
          </CardContent>
        </Card>
      </PageContainer>
    </FormProvider>
  );
}

export default function ChangePassword() {
  const router = useRouter();
  const { authResolved, isAuthenticated, isLocalTestUser, username } =
    useUser();

  useEffect(() => {
    if (!authResolved) {
      return;
    }

    if (isLocalTestUser) {
      router.replace("/reviews");
    } else if (!isAuthenticated) {
      router.replace("/auth/login");
    }
  }, [authResolved, isAuthenticated, isLocalTestUser, router]);

  if (!authResolved || !isAuthenticated || isLocalTestUser) {
    return <Box sx={{ minHeight: "100vh" }} />;
  }

  return <ChangePasswordForm username={username ?? ""} />;
}

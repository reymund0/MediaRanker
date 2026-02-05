"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Box, Card, CardContent, Typography, Alert, Link } from "@mui/material";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { handleSignup } from "../helpers";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";

const signupSchema = z
  .object({
    username: z.string().min(1, "Username is required"),
    email: z.string().email("Invalid email address"),
    password: z.string().min(8, "Password must be at least 8 characters"),
    confirmPassword: z.string().min(1, "Confirm password is required"),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });

type SignupFormData = z.infer<typeof signupSchema>;

export default function Signup() {
  const router = useRouter();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const methods = useForm<SignupFormData>({
    resolver: zodResolver(signupSchema),
    defaultValues: {
      username: "",
      email: "",
      password: "",
      confirmPassword: "",
    },
  });

  const onSubmit = async (data: SignupFormData) => {
    setError("");
    setLoading(true);

    const result = await handleSignup({
      username: data.username,
      email: data.email,
      password: data.password,
    });

    if (result.success) {
      router.push(
        `/auth/confirm-signup?username=${encodeURIComponent(data.username)}`,
      );
    } else {
      setError(result.error || "Signup failed");
      setLoading(false);
    }
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
              Sign Up
            </Typography>

            {error && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {error}
              </Alert>
            )}

            <FormTextField<SignupFormData>
              name="username"
              label="Username"
              autoComplete="username"
            />

            <FormTextField<SignupFormData>
              name="email"
              label="Email"
              type="email"
              autoComplete="email"
            />

            <FormTextField<SignupFormData>
              name="password"
              label="Password"
              type="password"
              autoComplete="new-password"
            />

            <FormTextField<SignupFormData>
              name="confirmPassword"
              label="Confirm Password"
              type="password"
              autoComplete="new-password"
            />

            <PrimaryButton
              type="submit"
              fullWidth
              disabled={loading}
              sx={{ mt: 2 }}
            >
              {loading ? "Signing up..." : "Sign Up"}
            </PrimaryButton>

            <Typography variant="body2" align="center">
              Already have an account?{" "}
              <Link href="/auth/login">Login</Link>
            </Typography>
          </CardContent>
        </Card>
      </Box>
    </FormProvider>
  );
}

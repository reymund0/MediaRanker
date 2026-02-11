"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import {
  Box,
  Card,
  CardContent,
  Typography,
  Alert,
  Link,
} from "@mui/material";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { handleLogin } from "../helpers";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";

const loginSchema = z.object({
  usernameOrEmail: z.string().min(1, "Username or email is required"),
  password: z.string().min(1, "Password is required"),
});

type LoginFormData = z.infer<typeof loginSchema>;

export default function Login() {
  const router = useRouter();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const methods = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      usernameOrEmail: "",
      password: "",
    },
  });

  const onSubmit = async (data: LoginFormData) => {
    setError("");
    setLoading(true);

    const result = await handleLogin(data);

    if (result.success) {
      router.push("/home");
    } else {
      setError(result.error || "Login failed");
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
              Login
            </Typography>

            {error && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {error}
              </Alert>
            )}

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

            <PrimaryButton
              type="submit"
              fullWidth
              disabled={loading}
              sx={{ mt: 3, mb: 2 }}
            >
              {loading ? "Logging in..." : "Login"}
            </PrimaryButton>

            <Typography variant="body2" align="center">
              Don't have an account?{" "}
              <Link href="/auth/signup">Sign up</Link>
            </Typography>
          </CardContent>
        </Card>
      </Box>
    </FormProvider>
  );
}

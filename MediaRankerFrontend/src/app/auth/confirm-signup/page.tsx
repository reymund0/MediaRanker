"use client";
import { useState, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Box,
  TextField,
  Button,
  Typography,
  Alert,
  Link as MuiLink,
} from "@mui/material";
import Link from "next/link";
import { handleConfirmSignup, handleResendCode } from "../helpers";

export default function ConfirmSignup() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [username, setUsername] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);

  useEffect(() => {
    const usernameParam = searchParams.get("username");
    if (usernameParam) {
      setUsername(usernameParam);
    }
  }, [searchParams]);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    setLoading(true);

    const result = await handleConfirmSignup(username, code);

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
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        bgcolor: "#f5f5f5",
      }}
    >
      <Box
        component="form"
        onSubmit={onSubmit}
        sx={{
          bgcolor: "white",
          p: 4,
          borderRadius: 2,
          boxShadow: 1,
          width: "100%",
          maxWidth: 400,
        }}
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

        <TextField
          fullWidth
          label="Username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          required
          margin="normal"
          autoComplete="username"
        />

        <TextField
          fullWidth
          label="Confirmation Code"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          required
          margin="normal"
          autoComplete="off"
          helperText="Check your email for the 6-digit code"
        />

        <Button
          type="submit"
          fullWidth
          variant="contained"
          disabled={loading}
          sx={{ mt: 3, mb: 2 }}
        >
          {loading ? "Confirming..." : "Confirm"}
        </Button>

        <Button
          fullWidth
          variant="outlined"
          onClick={onResendCode}
          disabled={resending || !username}
          sx={{ mb: 2 }}
        >
          {resending ? "Resending..." : "Resend Code"}
        </Button>

        <Typography variant="body2" align="center">
          Already confirmed?{" "}
          <Link href="/auth/login" passHref legacyBehavior>
            <MuiLink>Login</MuiLink>
          </Link>
        </Typography>
      </Box>
    </Box>
  );
}

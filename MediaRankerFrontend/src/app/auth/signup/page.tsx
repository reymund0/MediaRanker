"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import {
  Box,
  TextField,
  Button,
  Typography,
  Alert,
  Link as MuiLink,
} from "@mui/material";
import Link from "next/link";
import { handleSignup } from "../helpers";

export default function Signup() {
  const router = useRouter();
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");

    if (password !== confirmPassword) {
      setError("Passwords do not match");
      return;
    }

    if (password.length < 8) {
      setError("Password must be at least 8 characters");
      return;
    }

    setLoading(true);

    const result = await handleSignup({ username, email, password });

    if (result.success) {
      router.push(
        `/auth/confirm-signup?username=${encodeURIComponent(username)}`,
      );
    } else {
      setError(result.error || "Signup failed");
      setLoading(false);
    }
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
          Sign Up
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
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
          helperText="Choose a unique username"
        />

        <TextField
          fullWidth
          label="Email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          margin="normal"
          autoComplete="email"
        />

        <TextField
          fullWidth
          label="Password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
          margin="normal"
          autoComplete="new-password"
          helperText="Must be at least 8 characters"
        />

        <TextField
          fullWidth
          label="Confirm Password"
          type="password"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          required
          margin="normal"
          autoComplete="new-password"
        />

        <Button
          type="submit"
          fullWidth
          variant="contained"
          disabled={loading}
          sx={{ mt: 3, mb: 2 }}
        >
          {loading ? "Signing up..." : "Sign Up"}
        </Button>

        <Typography variant="body2" align="center">
          Already have an account?{" "}
          <Link href="/auth/login" passHref legacyBehavior>
            <MuiLink>Login</MuiLink>
          </Link>
        </Typography>
      </Box>
    </Box>
  );
}

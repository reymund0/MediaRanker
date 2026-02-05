"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Box, TextField, Button, Typography, Alert, Link as MuiLink } from "@mui/material";
import Link from "next/link";
import { handleLogin } from "../helpers";

export default function Login() {
    const router = useRouter();
    const [usernameOrEmail, setUsernameOrEmail] = useState("");
    const [password, setPassword] = useState("");
    const [error, setError] = useState("");
    const [loading, setLoading] = useState(false);

    const onSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError("");
        setLoading(true);

        const result = await handleLogin({ usernameOrEmail, password });

        if (result.success) {
            router.push("/home");
        } else {
            setError(result.error || "Login failed");
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
                    Login
                </Typography>

                {error && (
                    <Alert severity="error" sx={{ mb: 2 }}>
                        {error}
                    </Alert>
                )}

                <TextField
                    fullWidth
                    label="Username or Email"
                    value={usernameOrEmail}
                    onChange={(e) => setUsernameOrEmail(e.target.value)}
                    required
                    margin="normal"
                    autoComplete="username"
                />

                <TextField
                    fullWidth
                    label="Password"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    margin="normal"
                    autoComplete="current-password"
                />

                <Button
                    type="submit"
                    fullWidth
                    variant="contained"
                    disabled={loading}
                    sx={{ mt: 3, mb: 2 }}
                >
                    {loading ? "Logging in..." : "Login"}
                </Button>

                <Typography variant="body2" align="center">
                    Don't have an account?{" "}
                    <Link href="/auth/signup" passHref legacyBehavior>
                        <MuiLink>Sign up</MuiLink>
                    </Link>
                </Typography>
            </Box>
        </Box>
    );
}
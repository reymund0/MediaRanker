"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Box, Typography, Alert } from "@mui/material";
import { SecondaryButton } from "@/lib/components/inputs/button/secondary-button";
import { handleSignOut } from "@/app/auth/helpers";

export default function Home() {
  const router = useRouter();
  const [error, setError] = useState("");

  const onSignOut = async () => {
    const result = await handleSignOut();
    if (result.success) {
      router.push("/auth/login");
    } else {
      setError(result.error || "Failed to sign out");
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
        sx={{
          bgcolor: "white",
          p: 4,
          borderRadius: 2,
          boxShadow: 1,
          textAlign: "center",
        }}
      >
        <Typography variant="h3" component="h1" gutterBottom>
          Welcome to Media Ranker
        </Typography>
        <Typography variant="body1" color="text.secondary">
          You are successfully logged in!
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}

        <SecondaryButton onClick={onSignOut} sx={{ mt: 3 }}>
          Sign Out
        </SecondaryButton>
      </Box>
    </Box>
  );
}

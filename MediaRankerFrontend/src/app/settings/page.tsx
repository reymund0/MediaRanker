"use client";

import { Box, Typography } from "@mui/material";

export default function SettingsPage() {
  return (
    <Box
      sx={{
        minHeight: "calc(100vh - 72px)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        px: 2,
      }}
    >
      <Box
        sx={{
          backgroundColor: "white",
          borderRadius: 2,
          boxShadow: 1,
          p: { xs: 3, sm: 4 },
          textAlign: "center",
        }}
      >
        <Typography variant="h4" component="h1" gutterBottom>
          Settings
        </Typography>
        <Typography color="text.secondary">
          Settings page placeholder. More options will be added soon.
        </Typography>
      </Box>
    </Box>
  );
}

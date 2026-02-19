"use client";

import { Box, Typography } from "@mui/material";

export default function SettingsPage() {
  return (
    <Box
      sx={{
        flex: 1,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        px: 2,
      }}
    >
      <Box
        sx={{
          backgroundColor: "background.paper",
          borderRadius: 2,
          boxShadow: 1,
          border: "1px solid",
          borderColor: "divider",
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

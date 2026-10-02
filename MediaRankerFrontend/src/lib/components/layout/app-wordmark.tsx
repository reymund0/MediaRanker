import { Box, Typography } from "@mui/material";

export function AppWordmark() {
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 1.25, flexShrink: 0 }}>
      <Box
        aria-hidden="true"
        sx={{
          width: 30,
          height: 30,
          display: "grid",
          placeItems: "center",
          borderRadius: "8px",
          backgroundColor: "primary.main",
          color: "primary.contrastText",
        }}
      >
        <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
          <path
            d="M3 13.5h2.7V9H3v4.5Zm4.65 0h2.7V6h-2.7v7.5Zm4.65 0H15V3h-2.7v10.5Z"
            fill="currentColor"
          />
        </svg>
      </Box>
      <Typography
        variant="h6"
        component="span"
        sx={{ fontSize: 18, fontWeight: 800, letterSpacing: "-0.025em" }}
      >
        MediaRanker
      </Typography>
    </Box>
  );
}

import { Box, Typography } from "@mui/material";

export type ScoreBarProps = {
  label: string;
  value: number | null;
};

export function ScoreBar({ label, value }: ScoreBarProps) {
  const clampedValue =
    value === null ? 0 : Math.max(0, Math.min(10, Math.round(value)));

  return (
    <Box
      role="meter"
      aria-label={label}
      aria-valuemin={1}
      aria-valuemax={10}
      aria-valuenow={value === null ? undefined : clampedValue}
      aria-valuetext={
        value === null ? "Not rated" : `${clampedValue} out of 10`
      }
      sx={{ display: "flex", flexDirection: "column", gap: 0.75, minWidth: 0 }}
    >
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1 }}>
        <Typography variant="caption" color="text.muted" noWrap>
          {label}
        </Typography>
        <Typography variant="numeric" sx={{ fontSize: 13, color: "text.primary" }}>
          {value === null ? "Not rated" : clampedValue}
        </Typography>
      </Box>
      <Box aria-hidden="true" sx={{ display: "flex", gap: 0.5, width: "100%" }}>
        {Array.from({ length: 10 }, (_, index) => (
          <Box
            key={index}
            sx={{
              flex: 1,
              minWidth: 0,
              height: 6,
              borderRadius: "3px",
              backgroundColor: index < clampedValue ? "primary.main" : "divider",
            }}
          />
        ))}
      </Box>
    </Box>
  );
}

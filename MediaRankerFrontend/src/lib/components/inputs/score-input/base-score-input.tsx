import {
  Box,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { useId } from "react";

export type BaseScoreInputProps = {
  label: string;
  value: number | null;
  onChange: (value: number | null) => void;
  disabled?: boolean;
  errorMessage?: string;
};

export function BaseScoreInput({
  label,
  value,
  onChange,
  disabled = false,
  errorMessage,
}: BaseScoreInputProps) {
  const labelId = useId();

  return (
    <Box
      sx={{ display: "flex", flexDirection: "column", gap: 0.75, minWidth: 0 }}
    >
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "baseline",
          gap: 1,
        }}
      >
        <Typography
          id={labelId}
          component="span"
          sx={{ fontSize: 13, fontWeight: 600, color: "text.secondary" }}
        >
          {label}
        </Typography>
        {value === null ? (
          <Typography variant="caption" color="text.muted">
            Not rated
          </Typography>
        ) : null}
      </Box>
      <ToggleButtonGroup
        exclusive
        value={value}
        disabled={disabled}
        aria-labelledby={labelId}
        aria-label={`${label} score`}
        onChange={(_, nextValue: number | null) => onChange(nextValue)}
        sx={{
          display: "flex",
          gap: 0.5,
          width: "100%",
          "& .MuiToggleButtonGroup-grouped": {
            border: "1px solid",
            borderColor: "divider",
            borderRadius: "7px !important",
            margin: 0,
          },
        }}
      >
        {Array.from({ length: 10 }, (_, index) => index + 1).map((score) => (
          <ToggleButton
            key={score}
            value={score}
            aria-label={`${score} out of 10`}
            sx={{
              flex: 1,
              minWidth: 0,
              height: 34,
              px: 0.25,
              color: "text.muted",
              borderColor: "divider",
              backgroundColor: "background.default",
              fontFamily: "var(--font-mono), monospace",
              fontSize: 12,
              fontWeight: 600,
              "&:hover": {
                color: "text.primary",
                backgroundColor: "action.hover",
              },
              "&.Mui-selected": {
                color: "primary.contrastText",
                backgroundColor: "primary.main",
                borderColor: "primary.main",
                "&:hover": {
                  backgroundColor: "primary.light",
                },
              },
              "&.Mui-focusVisible": {
                boxShadow: (theme) => `0 0 0 3px ${theme.palette.action.focus}`,
              },
            }}
          >
            {score}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
      {errorMessage ? (
        <Typography variant="caption" color="error.light" role="alert">
          {errorMessage}
        </Typography>
      ) : null}
    </Box>
  );
}

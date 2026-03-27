"use client";

import { Box, Typography, Rating, RatingProps } from "@mui/material";
import { useTheme } from "@mui/material/styles";

export type BaseStarRankingProps = Omit<RatingProps, 'value' | 'onChange'> & {
  value: number;
  onChange: (value: number) => void;
  label?: string;
  disabled?: boolean;
  errorMessage?: string;
}


export const BaseStarRating = ({
  value,
  onChange,
  label,
  disabled = false,
  errorMessage,
  ...props
}: BaseStarRankingProps) => {
  const theme = useTheme();

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}> 
      {label && (
        <Typography variant="caption" color="text.secondary">
          {label}
        </Typography>
      )}
      <Rating
        value={value / 2 /* MUI rating is 1-5, we want 1-10 */}
        precision={0.5}
        disabled={disabled}
        onChange={(_, newValue) => {
          if (newValue !== null) onChange(Math.round(newValue * 2));
        }}
        {...props}
      />
      {errorMessage && (
        <Typography variant="caption" color="error">
          {errorMessage}
        </Typography>
      )}
    </Box>
  );
};

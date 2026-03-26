"use client";

import React, { useState } from "react";
import { Box, Tooltip, Typography } from "@mui/material";
import { Star, StarHalf } from "@mui/icons-material";
import { useTheme } from "@mui/material/styles";

export interface BaseStarRankingProps {
  value: number;
  onChange: (value: number) => void;
  label?: string;
  disabled?: boolean;
}

/**
 * A 1-10 rating component represented by 5 stars.
 * Each half-star represents a value of 1.
 */
export const BaseStarRanking: React.FC<BaseStarRankingProps> = ({
  value,
  onChange,
  label,
  disabled = false,
}) => {
  const theme = useTheme();
  const [hoverValue, setHoverValue] = useState<number | null>(null);

  // Colors based on theme and user request
  const FILLED_COLOR = "#FACC15"; // Yellow-400
  const HOVER_COLOR = "#CA8A04"; // Yellow-600
  const EMPTY_COLOR = theme.palette.divider; // Matches theme divider

  const handleMouseMove = (index: number, isRightHalf: boolean) => {
    if (disabled) return;
    const newValue = (index * 2) + (isRightHalf ? 2 : 1);
    setHoverValue(newValue);
  };

  const handleMouseLeave = () => {
    setHoverValue(null);
  };

  const handleClick = (index: number, isRightHalf: boolean) => {
    if (disabled) return;
    const newValue = (index * 2) + (isRightHalf ? 2 : 1);
    onChange(newValue);
  };

  const getHalfStarColor = (val: number) => {
    // If hovering, use hover color for all halves up to hoverValue
    if (hoverValue !== null && val <= hoverValue) {
      return HOVER_COLOR;
    }
    // Otherwise use filled color if value is >= this half
    if (val <= value) {
      return FILLED_COLOR;
    }
    return EMPTY_COLOR;
  };

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
      {label && (
        <Typography variant="caption" color="text.secondary">
          {label}
        </Typography>
      )}
      <Box 
        onMouseLeave={handleMouseLeave}
        sx={{ 
          display: "flex", 
          alignItems: "center",
          cursor: disabled ? "default" : "pointer",
          width: "fit-content"
        }}
      >
        {[0, 1, 2, 3, 4].map((starIndex) => (
          <Box 
            key={starIndex} 
            sx={{ 
              position: "relative", 
              display: "flex",
              width: 32,
              height: 32
            }}
          >
            {/* Left Half Hitbox */}
            <Box
              onMouseMove={() => handleMouseMove(starIndex, false)}
              onClick={() => handleClick(starIndex, false)}
              sx={{
                position: "absolute",
                left: 0,
                top: 0,
                width: "50%",
                height: "100%",
                zIndex: 1
              }}
            />
            {/* Right Half Hitbox */}
            <Box
              onMouseMove={() => handleMouseMove(starIndex, true)}
              onClick={() => handleClick(starIndex, true)}
              sx={{
                position: "absolute",
                right: 0,
                top: 0,
                width: "50%",
                height: "100%",
                zIndex: 1
              }}
            />
            
            {/* Visual Stars */}
            <Box sx={{ position: "relative", width: "100%", height: "100%" }}>
              {/* The "Back" star (empty or right half filled) */}
              <Star 
                sx={{ 
                  fontSize: 32, 
                  color: getHalfStarColor((starIndex * 2) + 2),
                  position: "absolute"
                }} 
              />
              {/* The "Front" half star (left half) */}
              <StarHalf 
                sx={{ 
                  fontSize: 32, 
                  color: getHalfStarColor((starIndex * 2) + 1),
                  position: "absolute"
                }} 
              />
            </Box>
          </Box>
        ))}
        <Typography 
          variant="body2" 
          sx={{ ml: 1.5, minWidth: "2.5rem", color: "text.secondary", fontWeight: "medium" }}
        >
          {hoverValue !== null ? hoverValue : value} / 10
        </Typography>
      </Box>
    </Box>
  );
};

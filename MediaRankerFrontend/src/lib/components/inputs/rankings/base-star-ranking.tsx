"use client";

import React, { useState } from "react";
import { Box, Tooltip, Typography } from "@mui/material";
import { Star, StarHalf, StarBorder } from "@mui/icons-material";
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
export const BaseStarRanking = ({
  value,
  onChange,
  label,
  disabled = false,
}: BaseStarRankingProps) => {
  const theme = useTheme();
  const [hoverValue, setHoverValue] = useState<number | null>(null);

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

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5, width: "100%", height: "100%" }}>
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
          width: "100%",
          height: "100%"
        }}
      >
        {[0, 1, 2, 3, 4].map((starIndex) => {
          const starBaseValue = starIndex * 2;
          const isHovering = hoverValue !== null;
          
          // Current display value (hover preview or actual value)
          const displayValue = isHovering ? hoverValue : value;
          
          // Determine fill level for this star in the current display
          const isFullDisplay = displayValue >= starBaseValue + 2;
          const isHalfDisplay = !isFullDisplay && displayValue >= starBaseValue + 1;

          return (
            <Box 
              key={starIndex} 
              sx={{ 
                position: "relative", 
                display: "flex",
                flex: 1,
                height: "100%",
                aspectRatio: "1/1",
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
                  zIndex: 2
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
                  zIndex: 2
                }}
              />
              
              {/* Visual Stars */}
              <Box sx={{ position: "relative", width: "100%", height: "100%" }}>
                {/* 1. Base Border (Always present) */}
                <StarBorder 
                  sx={{ 
                    width: "100%",
                    height: "100%",
                    fontSize: "inherit",
                    color: theme.palette.ranking.empty,
                    position: "absolute"
                  }} 
                />
                
                {/* 2. Fill (Full or Half based on current display value) */}
                {isFullDisplay ? (
                  <Star 
                    sx={{ 
                      width: "100%",
                      height: "100%",
                      fontSize: "inherit",
                      color: theme.palette.ranking.filled,
                      position: "absolute"
                    }} 
                  />
                ) : isHalfDisplay ? (
                  <StarHalf 
                    sx={{ 
                      width: "100%",
                      height: "100%",
                      fontSize: "inherit",
                      color: theme.palette.ranking.filled,
                      position: "absolute"
                    }} 
                  />
                ) : null}
              </Box>
            </Box>
          );
        })}
      </Box>
    </Box>
  );
};

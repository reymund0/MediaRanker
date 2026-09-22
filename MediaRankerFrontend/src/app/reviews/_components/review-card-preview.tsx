"use client";

import { Box, Stack, Typography } from "@mui/material";
import { BaseStarRating } from "@/lib/components/inputs/rating/base-star-rating";
import { ReviewDto } from "../contracts";
import { COVER_HEIGHT, INFO_HEIGHT } from "./review-card-utils";
import { CoverImage } from "@/lib/components/data-display/cover-image";

type ReviewCardPreviewProps = {
  review: ReviewDto;
  onClick: () => void;
};

export function ReviewCardPreview({ review, onClick }: ReviewCardPreviewProps) {
  return (
    <Box
      onClick={onClick}
      sx={{
        cursor: "pointer",
        height: "100%",
        display: "flex",
        flexDirection: "column",
      }}
    >
      <Box
        sx={{
          width: "100%",
          height: COVER_HEIGHT,
          overflow: "hidden",
          flexShrink: 0,
          bgcolor: "action.hover",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <CoverImage
          src={review.mediaCoverImageUrl}
          alt={`${review.mediaTitle} cover`}
          sx={{ width: "100%", height: "100%", objectFit: "cover" }}
          placeholderSx={{
            width: "100%",
            height: "100%",
            "& svg": { fontSize: 56 },
          }}
        />
      </Box>
      <Stack
        direction="column"
        justifyContent="center"
        alignItems="center"
        sx={{ height: INFO_HEIGHT, px: 1.5, py: 1, textAlign: "center" }}
        gap={0.5}
      >
        <Typography
          variant="subtitle2"
          noWrap
          title={review.mediaTitle}
          sx={{ width: "100%" }}
        >
          {review.mediaTitle}
        </Typography>
        <BaseStarRating
          value={review.overallScore}
          onChange={() => {}}
          disabled
          size="small"
        />
      </Stack>
    </Box>
  );
}

"use client";

import ImageNotSupportedIcon from "@mui/icons-material/ImageNotSupported";
import { Box, Stack, Typography } from "@mui/material";
import { BaseStarRating } from "@/lib/components/inputs/rating/base-star-rating";
import { ReviewDto } from "../contracts";
import { COVER_HEIGHT, INFO_HEIGHT } from "./review-card-utils";

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
        {review.mediaCoverImageUrl ? (
          <Box
            component="img"
            src={review.mediaCoverImageUrl}
            alt={review.mediaTitle}
            sx={{ width: "100%", height: "100%", objectFit: "cover" }}
          />
        ) : (
          <ImageNotSupportedIcon
            sx={{ fontSize: 56, color: "text.disabled" }}
          />
        )}
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

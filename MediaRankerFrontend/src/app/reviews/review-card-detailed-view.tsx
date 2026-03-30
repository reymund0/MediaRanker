"use client";

import { Box, Button, Stack, Typography } from "@mui/material";
import { BaseStarRating } from "@/lib/components/inputs/rating/base-star-rating";
import { ReviewDto } from "./contracts";

type ReviewCardDetailViewProps = {
  review: ReviewDto;
  onBack: () => void;
  onEdit: () => void;
};

export function ReviewCardDetailView({ review, onBack, onEdit }: ReviewCardDetailViewProps) {
  return (
    <Stack direction="column" sx={{ height: "100%", p: 1.5, overflow: "auto" }} gap={1}>
      <Typography variant="subtitle1" fontWeight={600} noWrap>
        {review.mediaTitle}
      </Typography>
      {review.reviewTitle && (
        <Typography variant="body2" color="text.secondary">
          {review.reviewTitle}
        </Typography>
      )}
      {review.notes && (
        <Typography variant="body2" sx={{ whiteSpace: "pre-wrap" }}>
          {review.notes}
        </Typography>
      )}
      <Box>
        <Typography variant="caption" color="text.secondary">
          Overall
        </Typography>
        <BaseStarRating
          value={review.overallScore}
          onChange={() => {}}
          disabled
          size="small"
        />
      </Box>
      {review.fields.map((field) => (
        <Box key={field.templateFieldId}>
          <Typography variant="caption" color="text.secondary">
            {field.templateFieldName}
          </Typography>
          <BaseStarRating
            value={field.value}
            onChange={() => {}}
            disabled
            size="small"
          />
        </Box>
      ))}
      <Stack
        direction="row"
        justifyContent="space-between"
        alignItems="center"
        sx={{ mt: "auto", pt: 1 }}
      >
        <Button size="small" variant="text" onClick={onBack}>
          Back
        </Button>
        <Button size="small" variant="outlined" onClick={onEdit}>
          Edit
        </Button>
      </Stack>
    </Stack>
  );
}

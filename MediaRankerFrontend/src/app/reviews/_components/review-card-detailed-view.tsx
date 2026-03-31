"use client";

import { Button, Grid, Stack, Typography } from "@mui/material";
import { BaseStarRating } from "@/lib/components/inputs/rating/base-star-rating";
import { ReviewDto } from "../contracts";

type ReviewCardDetailViewProps = {
  review: ReviewDto;
  onBack: () => void;
  onEdit: () => void;
};

export function ReviewCardDetailView({
  review,
  onBack,
  onEdit,
}: ReviewCardDetailViewProps) {
  return (
    <Stack
      direction="column"
      sx={{ height: "100%", p: 1.5, overflow: "auto" }}
      gap={1.5}
    >
      <Typography
        variant="subtitle1"
        fontWeight={600}
        sx={{ pr: 4.5, overflowWrap: "anywhere" }}
      >
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
      <Grid container spacing={1.5}>
        <Grid size={12} sx={{ py: 1 }}>
          <BaseStarRating
            label="Overall"
            value={review.overallScore}
            onChange={() => {}}
            disabled
            size="large"
          />
        </Grid>
        {review.fields.map((field) => (
          <Grid key={field.templateFieldId} size={6}>
            <BaseStarRating
              label={field.templateFieldName}
              value={field.value}
              onChange={() => {}}
              disabled
              size="medium"
            />
          </Grid>
        ))}
      </Grid>
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

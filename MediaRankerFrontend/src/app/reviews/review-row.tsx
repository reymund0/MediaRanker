"use client";

import { Button, Stack, Typography } from "@mui/material";
import { ReviewDto } from "./contracts";
import { ReviewCard } from "./review-card";
import { useQuery } from "@/lib/api/use-query";
import { useEffect, useState } from "react";

export interface ReviewRowProps {
  label: string;
  queryRoute: string;
  enableNewReview?: boolean;
  newReviewMediaTypeId?: number;
}

export function ReviewRow({ label, enableNewReview, newReviewMediaTypeId, queryRoute }: ReviewRowProps) {

  const [reviews, setReviews] = useState<ReviewDto[]>([]);

  const { data: reviewsData, isLoading: reviewsLoading, error: reviewsError } = useQuery<ReviewDto[]>({
    route: queryRoute,
    queryKey: ["reviews"]
  });

  useEffect(() => {
    if (reviewsData) {
      setReviews(reviewsData);
    }
  }, [reviewsData]);

  return (
    <Stack direction={"column"} gap={2}>
      <Stack direction={"row"} justifyContent={"space-between"}>
        <Typography variant="h6">{label}</Typography>
        {enableNewReview && newReviewMediaTypeId && (
          <Button variant="outlined">New Review</Button>
        )}
      </Stack>
      <Stack direction={"row"} gap={2}>
        {reviewsLoading ? (
          <Typography>Loading...</Typography>
        ) : reviewsError ? (
          <Typography>Error: {reviewsError.message}</Typography>
        ) : (
          reviews.length > 0 ? (
            reviews.map((review) => (
              <ReviewCard key={review.id} review={review} />
            ))
          ) : (
            <Typography>No reviews found</Typography>
          )
        )}
      </Stack>
    </Stack>
  );
}
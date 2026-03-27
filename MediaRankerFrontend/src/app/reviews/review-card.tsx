"use client";

import { ReviewDto } from "./contracts";

export interface ReviewCardProps {
  review?: ReviewDto;
  onReviewSave?: (review: ReviewDto) => void;
}

export function ReviewCard({ review, onReviewSave }: ReviewCardProps) {
  return (
    <div>
      <h1>Review Card</h1>
    </div>
  );
}

"use client";

import { Box } from "@mui/material";
import { ReviewCardPreview } from "./review-card-preview";
import { ReviewCardDetailView } from "./review-card-detailed-view";
import { ReviewCardEdit, TemplateFieldDisplay } from "./review-card-edit";
import { ReviewCardNewSteps } from "./review-card-new-steps";
import { ReviewCardDeleteButton } from "./review-card-delete-button";
import { CARD_WIDTH, CARD_HEIGHT } from "./review-card-utils";
import { useEffect, useState } from "react";
import type { ReviewDto } from "./contracts";
import { ReviewFormValues } from "./review-card-utils";

export interface ReviewCardProps {
  review?: ReviewDto;
  mediaTypeId: number;
  onInsertReview: (review: ReviewDto) => void;
  onCancelInsertReview: () => void;
  onUpdateReview: (updated: ReviewDto) => void;
  onDeleteReview: (reviewId: number) => void;
}

type CardState = "view" | "detailed-view" | "new" | "edit";

export function ReviewCard({
  review,
  mediaTypeId,
  onInsertReview,
  onCancelInsertReview,
  onUpdateReview,
  onDeleteReview
}: ReviewCardProps) {
  
  const [isNewReview, setIsNewReview] = useState(!review);
  const [cardState, setCardState] = useState<CardState>(isNewReview ? "new" : "view");
  const [currentReview, setCurrentReview] = useState<ReviewFormValues>();
  const [mediaTitle, setMediaTitle] = useState<string>("");
  const [templateFields, setTemplateFields] = useState<TemplateFieldDisplay[]>([]);

  useEffect(() => {
    if (!review) {
      setIsNewReview(true);
    } else {
      setIsNewReview(false);
      setCurrentReview({
        id: review.id,
        mediaId: review.mediaId,
        templateId: review.templateId,
        reviewTitle: review.reviewTitle ?? undefined,
        notes: review.notes ?? undefined,
        fields: review.fields.sort((a, b) => a.templateFieldPosition - b.templateFieldPosition)
          .reduce((acc, field) => {
            acc[field.templateFieldId] = field.value;
            return acc;
          }, {} as Record<string, number>)
      });
      setMediaTitle(review.mediaTitle);
      setTemplateFields(review.fields.map(field => ({
        id: field.templateFieldId,
        name: field.templateFieldName,
        position: field.templateFieldPosition
      })));
    }
  }, [review]);

  return (
    <>
      <Box
        sx={{
          position: "relative",
          width: CARD_WIDTH,
          minWidth: CARD_WIDTH,
          height: CARD_HEIGHT,
          borderRadius: 2,
          border: "1px solid",
          borderColor: "divider",
          overflow: "hidden",
          bgcolor: "background.paper",
          flexShrink: 0,
        }}
      >
      {["new", "edit", "detailed-view"].includes(cardState) && (
          <ReviewCardDeleteButton 
            review={review} 
            isNew={isNewReview}
            onDeleteReview={onDeleteReview} 
            onCancelNew={onCancelInsertReview}
          />
        )}
        {cardState === "new" && (
          <ReviewCardNewSteps
            mediaTypeId={mediaTypeId}
            onNewReview={(review, mediaId, templateFields) => {
              setCurrentReview(review);
              setMediaTitle(mediaId);
              setTemplateFields(templateFields);
              setCardState("edit");
            }}
            onCancel={onCancelInsertReview}
          />
        )}
        {cardState === "view" && review && (
          <ReviewCardPreview review={review} onClick={() => setCardState("detailed-view")} />
        )}
        {cardState === "detailed-view" && review && (
          <ReviewCardDetailView
            review={review}
            onBack={() => setCardState("view")}
            onEdit={() => setCardState("edit")}
          />
        )}
        {cardState === "edit" && (
          <ReviewCardEdit
            review={currentReview!}
            mediaTitle={mediaTitle}
            templateFields={templateFields}
            isNew={isNewReview}
            onInsert={onInsertReview}
            onUpdate={onUpdateReview}
            onUpdateCancel={() => setCardState("detailed-view")}
          />
        )}
      </Box>
    </>
  );
}

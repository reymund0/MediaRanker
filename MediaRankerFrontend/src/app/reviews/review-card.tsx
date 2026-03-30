"use client";

import CancelIcon from "@mui/icons-material/Cancel";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import { Box, CircularProgress, IconButton } from "@mui/material";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { useReviewCard } from "./use-review-card";
import { ReviewCardPreview } from "./review-card-preview";
import { ReviewCardDetailView } from "./review-card-detailed-view";
import { ReviewCardEdit, TemplateFieldDisplay } from "./review-card-edit";
import { ReviewCardNewSteps } from "./review-card-new-steps";
import { CARD_WIDTH, CARD_HEIGHT } from "./review-card-constants";
import { useEffect, useState } from "react";
import type { ReviewDto } from "./contracts";
import { ReviewFormValues } from "./review-card-schema";

export interface ReviewCardProps {
  review?: ReviewDto;
  mediaTypeId: number;
  onInsertReview: (review: ReviewDto) => void;
  onCancelInsertReview: () => void;
  onUpdateReview: (updated: ReviewDto) => void;
  onDeleteReview: (reviewId: number) => void;
}


export function ReviewCard(props: ReviewCardProps) {
  const {
    cardState,
    setCardState,
    showDeleteConfirm,
    setShowDeleteConfirm,
    review,
    isNew,
    isDeleting,
    handleDelete,
  } = useReviewCard(props);

  const [currentReview, setCurrentReview] = useState<ReviewFormValues>();
  const [mediaTitle, setMediaTitle] = useState<string>("");
  const [templateFields, setTemplateFields] = useState<TemplateFieldDisplay[]>([]);

  useEffect(() => {
    if (review) {
      // Initialize state based on supplied review. If not present we rely on the new process to fill this out.
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

  const RenderReviewEditCard = () => {
    if (isNew && cardState !== "edit") {
      return (
        <ReviewCardNewSteps
          mediaTypeId={props.mediaTypeId}
          onNewReview={(review, mediaId, templateFields) => {
            setMediaTitle(mediaId);
            setTemplateFields(templateFields);
            setCardState("edit");
          }}
          onCancel={props.onCancelInsertReview}
        />
      );
    }
    return (
      <ReviewCardEdit
        review={currentReview!}
        mediaTitle={mediaTitle}
        templateFields={templateFields}
        isNew={isNew}
        onInsert={props.onInsertReview}
        onUpdate={props.onUpdateReview}
        onUpdateCancel={() => setCardState("detailed-view")}
      />
    );
  };

  const renderTopRightAction = () => {
    if (cardState !== "edit") return null;
    if (isNew) {
      return (
        <IconButton
          size="small"
          onClick={props.onCancelInsertReview}
          sx={{ position: "absolute", top: 6, right: 6, zIndex: 1, color: "error.main" }}
        >
          <CancelIcon fontSize="small" />
        </IconButton>
      );
    }
    return (
      <IconButton
        size="small"
        onClick={() => setShowDeleteConfirm(true)}
        disabled={isDeleting}
        sx={{ position: "absolute", top: 6, right: 6, zIndex: 1, color: "error.main" }}
      >
        {isDeleting ? <CircularProgress size={16} /> : <DeleteOutlineIcon fontSize="small" />}
      </IconButton>
    );
  };

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
        {renderTopRightAction()}
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
        {cardState === "edit" && RenderReviewEditCard()}
      </Box>

      {showDeleteConfirm && review && (
        <BaseDialog
          open
          title="Delete Review"
          confirmLabel="Delete"
          confirmLoading={isDeleting}
          onConfirm={() => props.onDeleteReview(review.id)}
          onClose={() => setShowDeleteConfirm(false)}
        >
          Are you sure you want to delete your review for{" "}
          <strong>{review.mediaTitle}</strong>?
        </BaseDialog>
      )}
    </>
  );
}

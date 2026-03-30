"use client";

import CancelIcon from "@mui/icons-material/Cancel";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import { Box, CircularProgress, IconButton } from "@mui/material";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { ReviewCardPreview } from "./review-card-preview";
import { ReviewCardDetailView } from "./review-card-detailed-view";
import { ReviewCardEdit, TemplateFieldDisplay } from "./review-card-edit";
import { ReviewCardNewSteps } from "./review-card-new-steps";
import { CARD_WIDTH, CARD_HEIGHT } from "./review-card-utils";
import { useEffect, useState } from "react";
import type { ReviewDto } from "./contracts";
import { ReviewFormValues } from "./review-card-utils";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";

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
  
  const { showSuccess, showError } = useAlert();

  const [isNewReview, setIsNewReview] = useState(!review);
  const [cardState, setCardState] = useState<CardState>(isNewReview ? "new" : "view");
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);  
  const [currentReview, setCurrentReview] = useState<ReviewFormValues>();
  const [mediaTitle, setMediaTitle] = useState<string>("");
  const [templateFields, setTemplateFields] = useState<TemplateFieldDisplay[]>([]);

  // Initialize state based on supplied review. If not present we rely on the new process to fill this out.
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
  
  const { mutate: deleteReview, isPending: isDeleting } = useMutation<number, void>({
    route: (id) => `/api/reviews/${id}`,
    method: "DELETE",
  });

  const handleDelete = () => {
    if (review) {
      deleteReview(review.id, {
        onSuccess: () => {
          showSuccess("Review deleted successfully");
          onDeleteReview(review.id);
        },
        onError: (error) => {
          showError("Failed to delete review - " + error);
          console.error("Failed to delete review:", error);
        }
      });
    }
  };

  const renderTopRightAction = () => {
    if (cardState !== "detailed-view" && cardState !== "edit" && cardState !== "new") return null;
    if (isNewReview) {
      return (
        <IconButton
          size="small"
          onClick={onCancelInsertReview}
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

      {showDeleteConfirm && review && (
        <BaseDialog
          open
          title="Delete Review"
          confirmLabel="Delete"
          confirmLoading={isDeleting}
          onConfirm={handleDelete}
          onClose={() => setShowDeleteConfirm(false)}
        >
          Are you sure you want to delete your review for{" "}
          <strong>{review.mediaTitle}</strong>?
        </BaseDialog>
      )}
    </>
  );
}

"use client";

import CancelIcon from "@mui/icons-material/Cancel";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import { CircularProgress, IconButton } from "@mui/material";
import { useState } from "react";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { ReviewDto } from "./contracts";

interface ReviewCardDeleteButtonProps {
  review?: ReviewDto;
  isNew: boolean;
  onDeleteReview: (reviewId: number) => void;
  onCancelNew: () => void;
}

export function ReviewCardDeleteButton({ 
  review, 
  isNew, 
  onDeleteReview, 
  onCancelNew 
}: ReviewCardDeleteButtonProps) {
  const { showSuccess, showError } = useAlert();
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  const { mutate: deleteReview, isPending: isDeleting } = useMutation<number, void>({
    route: (id) => `/api/reviews/${id}`,
    method: "DELETE",
  });

  const handleDelete = () => {
    if (!review) return;
    deleteReview(review.id, {
      onSuccess: () => {
        showSuccess("Review deleted successfully");
        onDeleteReview(review.id);
      },
      onError: (error) => {
        showError("Failed to delete review - " + error.message);
        console.error("Failed to delete review:", error);
      },
    });
  };

  if (isNew) {
    return (
      <IconButton
        size="small"
        onClick={onCancelNew}
        sx={{ position: "absolute", top: 6, right: 6, zIndex: 1, color: "error.main" }}
      >
        <CancelIcon fontSize="small" />
      </IconButton>
    );
  }

  if (!review) return null;

  return (
    <>
      <IconButton
        size="small"
        onClick={(e) => {
          e.stopPropagation();
          setShowDeleteConfirm(true);
        }}
        disabled={isDeleting}
        sx={{ position: "absolute", top: 6, right: 6, zIndex: 1, color: "error.main" }}
      >
        {isDeleting ? <CircularProgress size={16} /> : <DeleteOutlineIcon fontSize="small" />}
      </IconButton>

      {showDeleteConfirm && (
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

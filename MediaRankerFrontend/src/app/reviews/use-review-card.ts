"use client";

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { ReviewDto, ReviewInsertRequest, ReviewUpdateRequest } from "./contracts";
import { ReviewEditSchema } from "./review-card-schema";
import { CardState } from "./review-card-constants";
import { ReviewCardProps } from "./review-card";

export function useReviewCard(props: ReviewCardProps) {
  const { showSuccess, showError } = useAlert();

  // ---- Derived ----
  const review = props.review ?? null;
  const isNew = props.review === null;

  // ---- State ----
  const [cardState, setCardState] = useState<CardState>(isNew ? "edit" : "view");
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  // ---- Mutations ----

  const { mutate: deleteReview, isPending: isDeleting } = useMutation<number, void>({
    route: (id) => `/api/reviews/${id}`,
    method: "DELETE",
  });

 
  // Handlers
  const handleDelete = () => {
    if (!review) return;
    deleteReview(review.id, {
      onSuccess: () => {
        showSuccess("Review deleted");
        setShowDeleteConfirm(false);
        props.onDeleteReview(review.id);
      },
      onError: (err) => {
        showError(err.message);
        setShowDeleteConfirm(false);
      },
    });
  };

  return {
    // State
    cardState,
    setCardState,
    showDeleteConfirm,
    setShowDeleteConfirm,
    // Derived
    review,
    isNew,
    // Mutation state
    isDeleting,
    // Handlers
    handleDelete,
  };
}

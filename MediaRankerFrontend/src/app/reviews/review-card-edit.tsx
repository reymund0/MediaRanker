"use client";

import { Button, Stack, Typography } from "@mui/material";
import { FormProvider, useForm } from "react-hook-form";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { FormStarRating } from "@/lib/components/inputs/rating/form-star-rating";
import { ReviewFormValues, ReviewEditSchema } from "./review-card-utils";
import { zodResolver } from "@hookform/resolvers/zod";
import { ReviewDto, ReviewInsertRequest, ReviewUpdateRequest } from "./contracts";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { useMutation } from "@/lib/api/use-mutation";

export interface TemplateFieldDisplay {
  id: number;
  name: string;
  position: number;
}

export interface ReviewCardEditProps {
  review: ReviewFormValues;
  mediaTitle: string;
  templateFields: TemplateFieldDisplay[];
  isNew: boolean;
  onInsert: (newReview: ReviewDto) => void;
  onUpdate: (updatedReview: ReviewDto) => void;
  onUpdateCancel: () => void;
};

export function ReviewCardEdit({
  review,
  mediaTitle,
  templateFields,
  isNew,
  onInsert,
  onUpdate,
  onUpdateCancel,
  }: ReviewCardEditProps) {

  const { mutate: insertReview, isPending: isInserting } = useMutation<ReviewInsertRequest, ReviewDto>({
    route: "/api/reviews",
    method: "POST",
  });

  const { mutate: updateReview, isPending: isUpdating } = useMutation<ReviewUpdateRequest, ReviewDto>({
    route: "/api/reviews/update",
    method: "PATCH",
  });
  
  const isLoading = isInserting || isUpdating;

  const { showSuccess, showError } = useAlert();

  const methods = useForm({
    resolver: zodResolver(ReviewEditSchema),
    defaultValues: review,
  });

  // Submit Handlers
  const handleInsert = (data: ReviewFormValues) => {

    const request: ReviewInsertRequest = {
      mediaId: data.mediaId!,
      templateId: data.templateId!,
      reviewTitle: data.reviewTitle?.trim() || null,
      notes: data.notes?.trim() || null,
      consumedAt: null,
      fields: Object.entries(data.fields).map(([id, value]) => ({
        templateFieldId: Number(id),
        value: value,
      })),
    };

    insertReview(request, {
      onSuccess: (saved) => {
        showSuccess("Review saved");
        onInsert(saved);
      },
      onError: (err) => showError(err.message),
    });
  };

const handleUpdate = (data: ReviewFormValues) => {
  const request: ReviewUpdateRequest = {
    id: Number(review.id),
    reviewTitle: data.reviewTitle?.trim() || null,
    notes: data.notes?.trim() || null,
    consumedAt: null,
    fields: Object.entries(data.fields).map(([id, value]) => ({
      templateFieldId: Number(id),
      value: value,
    })),
  };

  updateReview(request, {
    onSuccess: (saved) => {
      showSuccess("Review updated");
      onUpdate(saved);
    },
    onError: (err) => showError(err.message),
  });
};

  return (
    <FormProvider {...methods}>
      <Stack
        component="form"
        direction="column"
        sx={{ height: "100%", p: 1.5, overflow: "auto" }}
        gap={1.5}
        onSubmit={methods.handleSubmit(isNew ? handleInsert : handleUpdate)}
      >
        <Typography variant="subtitle2" noWrap>
          {mediaTitle}
        </Typography>
        <FormTextField<ReviewFormValues>
          name="reviewTitle"
          label="Review title"
          size="small"
        />
        <FormTextField<ReviewFormValues>
          name="notes"
          label="Notes"
          size="small"
          multiline
          minRows={2}
        />
        {templateFields.sort((a, b) => a.position - b.position).map((field) => (
          <FormStarRating<ReviewFormValues>
            key={field.id}
            name={`fields.${field.id}`}
            label={field.name}
          />
        ))}
        <Stack direction="row" justifyContent="flex-end" sx={{ mt: "auto", pt: 1 }}>
          <Button
            type="submit"
            size="small"
            variant="contained"
            disabled={isLoading}
            loading={isLoading}
          >
            Save
          </Button>
        </Stack>
        { // New reviews already have a red X visible in the card.
          !isNew && (
            <Button size="small" variant="text" onClick={onUpdateCancel}>
              Cancel
            </Button>
          )
        }
      </Stack>
    </FormProvider>
  );
}

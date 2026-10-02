"use client";

import CloseIcon from "@mui/icons-material/Close";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import {
  Box,
  Button,
  Divider,
  Drawer,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import type { Theme } from "@mui/material/styles";
import { FormProvider, useForm, useWatch } from "react-hook-form";
import { useQuery } from "@/lib/api/use-query";
import { usePendingCoverRefresh } from "@/lib/api/use-pending-cover-refresh";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { CoverTile } from "@/lib/components/data-display/cover-tile";
import { ScoreBar } from "@/lib/components/data-display/score-bar";
import { ReviewDto, ReviewUpdateRequest } from "../contracts";
import {
  formatReviewDate,
  getCoverTileStatus,
  getMediaTypeDisplayLabel,
  getMediaTypePluralLabel,
  getOverallPreview,
  getReleaseYear,
  sortReviewsByRank,
} from "./review-utils";
import {
  ReviewScoreField,
  ReviewWritingFields,
  ReviewScoreFormValues,
} from "./review-score-form";
import { useState } from "react";

interface ReviewDetailDrawerProps {
  review: ReviewDto;
  edit: boolean;
  onEdit: () => void;
  onCancelEdit: () => void;
  onClose: () => void;
  onUpdated: (review: ReviewDto) => void;
  onDeleted: (review: ReviewDto) => void;
}

export function ReviewDetailDrawer({
  review: initialReview,
  edit,
  onEdit,
  onCancelEdit,
  onClose,
  onUpdated,
  onDeleted,
}: ReviewDetailDrawerProps) {
  const { showSuccess, showError } = useAlert();
  const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
  const { data: typeReviews = [], refetch: refetchReview } = useQuery<
    ReviewDto[]
  >({
    route: `/api/reviews/byMediaType/${initialReview.mediaType}`,
    queryKey: ["reviews", initialReview.mediaType],
    enabled: !!initialReview,
  });
  const review =
    typeReviews.find((item) => item.id === initialReview.id) ?? initialReview;
  usePendingCoverRefresh({
    viewKey: `review:${review.id}`,
    hasPendingCovers: review.coverStatus === "pending",
    refetch: refetchReview,
    enabled: true,
  });
  const { mutate: updateReview, isPending: isSaving } = useMutation<
    ReviewUpdateRequest,
    ReviewDto
  >({
    route: "/api/reviews/update",
    method: "PATCH",
  });
  const { mutate: deleteReview, isPending: isDeleting } = useMutation<
    number,
    void
  >({
    route: (id) => `/api/reviews/${id}`,
    method: "DELETE",
  });

  const sortedReviews = sortReviewsByRank(typeReviews);
  const rank = sortedReviews.findIndex((item) => item.id === review.id) + 1;
  const fields = review.fields
    .map<ReviewScoreField>((field) => ({
      id: field.templateFieldId,
      name: field.templateFieldName,
      position: field.templateFieldPosition,
    }))
    .sort((left, right) => left.position - right.position);
  const year = getReleaseYear(review.mediaReleaseDate);
  const mediaTypeLabel = getMediaTypeDisplayLabel(review.mediaType);

  const handleUpdate = (values: ReviewScoreFormValues) => {
    const request: ReviewUpdateRequest = {
      id: review.id,
      reviewTitle: values.reviewTitle.trim() || null,
      notes: values.notes.trim() || null,
      consumedAt: values.consumedAt,
      fields: fields.map((field) => ({
        templateFieldId: field.id,
        value: values.fields[field.id] as number,
      })),
    };
    if (
      request.fields.some(
        (field) =>
          !Number.isInteger(field.value) || field.value < 1 || field.value > 10,
      )
    ) {
      showError("Score every field from 1 to 10 before saving.");
      return;
    }

    updateReview(request, {
      onSuccess: (saved) => {
        showSuccess("Review updated");
        onUpdated(saved);
      },
      onError: (error) => showError(error.message),
    });
  };

  const handleDelete = () => {
    deleteReview(review.id, {
      onSuccess: () => {
        showSuccess("Review deleted");
        setIsDeleteConfirmOpen(false);
        onDeleted(review);
      },
      onError: (error) => showError(error.message),
    });
  };

  return (
    <>
      <Drawer
        anchor="right"
        open
        onClose={onClose}
        sx={{
          "& .MuiDrawer-paper": {
            width: "min(600px, 100vw)",
            bgcolor: "background.paper",
            borderLeft: "1px solid",
            borderColor: "divider",
            display: "flex",
            flexDirection: "column",
          },
        }}
      >
        <Stack
          direction="row"
          alignItems="center"
          justifyContent="space-between"
          sx={{
            px: 3,
            py: 1.5,
            borderBottom: "1px solid",
            borderColor: "divider",
          }}
        >
          <Typography variant="overline" color="text.secondary">
            Review
          </Typography>
          <IconButton aria-label="Close review" onClick={onClose}>
            <CloseIcon />
          </IconButton>
        </Stack>

        {edit ? (
          <ReviewEditForm
            key={review.id}
            review={review}
            fields={fields}
            onSubmit={handleUpdate}
            onCancel={onCancelEdit}
            isSaving={isSaving}
          />
        ) : (
          <>
            <Box
              sx={(theme) => ({
                ...drawerScrollStyles(theme),
                flex: 1,
                minHeight: 0,
                overflow: "auto",
                px: 3,
                py: 4,
              })}
            >
              <Stack direction="row" gap={3} alignItems="flex-end">
                <CoverTile
                  title={review.mediaTitle}
                  src={review.mediaCoverImageUrl}
                  status={getCoverTileStatus(review.coverStatus)}
                  sx={{
                    width: 128,
                    height: 192,
                    borderRadius: 2,
                    flexShrink: 0,
                  }}
                />
                <Stack spacing={1} sx={{ minWidth: 0, pb: 0.5 }}>
                  <Box
                    sx={{
                      alignSelf: "flex-start",
                      px: 1.25,
                      py: 0.4,
                      borderRadius: 10,
                      border: "1px solid",
                      borderColor: "primary.main",
                      color: "primary.light",
                      fontFamily: "var(--font-mono), monospace",
                      fontSize: 12,
                      fontWeight: 700,
                    }}
                  >
                    {rank > 0 ? `#${rank} in ` : ""}
                    {getMediaTypePluralLabel(review.mediaType)}
                  </Box>
                  <Typography
                    variant="h4"
                    component="h2"
                    sx={{ overflowWrap: "anywhere" }}
                  >
                    {review.mediaTitle}
                  </Typography>
                  <Typography color="text.secondary">
                    {[year, mediaTypeLabel].filter(Boolean).join(" · ")}
                  </Typography>
                </Stack>
              </Stack>

              <Box
                sx={{
                  mt: 4,
                  p: 2.5,
                  border: "1px solid",
                  borderColor: "divider",
                  bgcolor: "background.default",
                  borderRadius: 2,
                  display: "flex",
                  alignItems: "center",
                  gap: 2.5,
                }}
              >
                <Typography
                  variant="numeric"
                  sx={{
                    color: "primary.light",
                    fontFamily: "var(--font-mono), monospace",
                    fontSize: 72,
                    fontWeight: 800,
                    lineHeight: 0.95,
                    letterSpacing: "-0.04em",
                  }}
                >
                  {review.overallScore}
                </Typography>
                <Divider orientation="vertical" flexItem />
                <Stack spacing={0.25}>
                  <Typography fontWeight={700}>Overall score</Typography>
                  <Typography variant="body2" color="text.secondary">
                    Average of {review.fields.length} scores, rounded
                  </Typography>
                </Stack>
              </Box>

              <Stack spacing={1.5} sx={{ mt: 3 }}>
                <Typography variant="overline" color="text.secondary">
                  Scores
                </Typography>
                {review.fields
                  .slice()
                  .sort(
                    (left, right) =>
                      left.templateFieldPosition - right.templateFieldPosition,
                  )
                  .map((field) => (
                    <ScoreBar
                      key={field.templateFieldId}
                      label={field.templateFieldName}
                      value={field.value}
                    />
                  ))}
              </Stack>

              {(review.reviewTitle || review.notes) && (
                <>
                  <Divider sx={{ my: 3 }} />
                  {review.reviewTitle ? (
                    <Typography variant="h6" component="h3" sx={{ mb: 1.25 }}>
                      “{review.reviewTitle}”
                    </Typography>
                  ) : null}
                  {review.notes ? (
                    <Typography
                      sx={{ whiteSpace: "pre-wrap", lineHeight: 1.7 }}
                    >
                      {review.notes}
                    </Typography>
                  ) : null}
                </>
              )}

              <Typography
                variant="caption"
                color="text.secondary"
                sx={{ display: "block", mt: 2 }}
              >
                Reviewed {formatReviewDate(review.createdAt)}
                {review.updatedAt !== review.createdAt
                  ? ` · Edited ${formatReviewDate(review.updatedAt)}`
                  : ""}
              </Typography>
            </Box>

            <Stack
              direction="row"
              alignItems="center"
              justifyContent="space-between"
              sx={{
                px: 3,
                py: 1.5,
                borderTop: "1px solid",
                borderColor: "divider",
              }}
            >
              <Button
                color="error"
                startIcon={<DeleteOutlineIcon />}
                onClick={() => setIsDeleteConfirmOpen(true)}
                disabled={isDeleting}
              >
                Delete review
              </Button>
              <Button
                variant="contained"
                startIcon={<EditOutlinedIcon />}
                onClick={onEdit}
              >
                Edit review
              </Button>
            </Stack>
          </>
        )}
      </Drawer>

      <BaseDialog
        open={isDeleteConfirmOpen}
        title="Delete review"
        confirmLabel="Delete review"
        closeLabel="Cancel"
        confirmLoading={isDeleting}
        danger
        onConfirm={handleDelete}
        onClose={() => setIsDeleteConfirmOpen(false)}
      >
        <>
          <Typography>
            Are you sure you want to delete your review for{" "}
            <strong>{review.mediaTitle}</strong>?
          </Typography>
          <Typography color="error.main" sx={{ mt: 1.5 }}>
            This action cannot be undone.
          </Typography>
        </>
      </BaseDialog>
    </>
  );
}

function ReviewEditForm({
  review,
  fields,
  onSubmit,
  onCancel,
  isSaving,
}: {
  review: ReviewDto;
  fields: ReviewScoreField[];
  onSubmit: (values: ReviewScoreFormValues) => void;
  onCancel: () => void;
  isSaving: boolean;
}) {
  const methods = useForm<ReviewScoreFormValues>({
    defaultValues: {
      reviewTitle: review.reviewTitle ?? "",
      notes: review.notes ?? "",
      consumedAt: review.consumedAt,
      fields: Object.fromEntries(
        review.fields.map((field) => [
          String(field.templateFieldId),
          field.value,
        ]),
      ),
    },
  });
  const scoreValues = useWatch({ control: methods.control, name: "fields" });
  const preview = getOverallPreview(
    fields.map((field) => scoreValues?.[field.id]),
  );
  const canSave =
    fields.length > 0 &&
    fields.every((field) => {
      const value = scoreValues?.[field.id];
      return (
        typeof value === "number" &&
        Number.isInteger(value) &&
        value >= 1 &&
        value <= 10
      );
    });

  return (
    <FormProvider {...methods}>
      <Box
        component="form"
        onSubmit={methods.handleSubmit(onSubmit)}
        sx={{ display: "flex", flexDirection: "column", flex: 1, minHeight: 0 }}
      >
        <Box
          sx={(theme) => ({
            ...drawerScrollStyles(theme),
            flex: 1,
            minHeight: 0,
            overflow: "auto",
            px: 3,
            py: 3,
          })}
        >
          <ReviewWritingFields fields={fields} />
        </Box>
        <Stack
          direction="row"
          justifyContent="flex-end"
          alignItems="center"
          gap={1.5}
          sx={{
            flexShrink: 0,
            px: 3,
            py: 1.5,
            borderTop: "1px solid",
            borderColor: "divider",
          }}
        >
          <Stack
            direction="row"
            alignItems="center"
            spacing={1}
            sx={{ mr: "auto" }}
          >
            <Typography variant="caption" color="text.secondary">
              Overall
            </Typography>
            <Typography variant="numeric" sx={{ fontSize: 28 }}>
              {preview.score ?? "–"}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {preview.scoredCount} of {preview.totalCount} scored
            </Typography>
          </Stack>
          <Button onClick={onCancel} disabled={isSaving}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            loading={isSaving}
            disabled={!canSave}
          >
            Save review
          </Button>
        </Stack>
      </Box>
    </FormProvider>
  );
}

function drawerScrollStyles(theme: Theme) {
  return {
    scrollbarWidth: "thin" as const,
    scrollbarColor: `${theme.palette.divider} transparent`,
    "&::-webkit-scrollbar": { width: 8 },
    "&::-webkit-scrollbar-thumb": {
      backgroundColor: theme.palette.divider,
      borderRadius: 4,
    },
    "&::-webkit-scrollbar-track": { backgroundColor: "transparent" },
  };
}

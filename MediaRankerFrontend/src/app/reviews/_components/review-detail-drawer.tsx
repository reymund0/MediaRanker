"use client";

import { isReviewScore, mapReviewScoreFields } from "./review-score-values";

import CloseIcon from "@mui/icons-material/Close";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import {
  Box,
  Button,
  ButtonBase,
  Divider,
  Drawer,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import Link from "next/link";
import type { Theme } from "@mui/material/styles";
import { FormProvider, useForm, useWatch } from "react-hook-form";
import { useQuery } from "@/lib/api/use-query";
import { reviewQueryOptions } from "./review-query";
import { usePendingCoverRefresh } from "@/lib/api/use-pending-cover-refresh";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { CoverTile } from "@/lib/components/data-display/cover-tile";
import { ScoreBar } from "@/lib/components/data-display/score-bar";
import { ReviewDto, ReviewUpdateRequest } from "../contracts";
import {
  formatReviewDate,
  getMediaTypeDisplayLabel,
  getRankLabel,
  getEpisodeContextLine,
  buildReviewRankLookups,
  formatSeriesYearRange,
  getOverallPreview,
  getReleaseYear,
  rankableGroup,
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
  onOpenRelatedReview: (review: ReviewDto) => void;
}

export function ReviewDetailDrawer({
  review: initialReview,
  edit,
  onEdit,
  onCancelEdit,
  onClose,
  onUpdated,
  onDeleted,
  onOpenRelatedReview,
}: ReviewDetailDrawerProps) {
  const { showSuccess, showError } = useAlert();
  const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
  const { data: typeReviews = [], refetch: refetchReview } = useQuery<
    ReviewDto[]
  >({
    ...reviewQueryOptions(initialReview.mediaType),
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

  const reviewRankLookups = buildReviewRankLookups(typeReviews);
  const currentReviewRanks = reviewRankLookups.get(rankableGroup(review));
  const rank = currentReviewRanks?.rankById.get(review.id) ?? 0;
  const seriesReview = review.seriesId == null
    ? undefined
    : typeReviews.find((item) => item.kind === "Series" && item.mediaCollectionId === review.seriesId);
  const relatedSeriesRanks = seriesReview
    ? reviewRankLookups.get(rankableGroup(seriesReview))
    : undefined;
  const episodeReviews = review.kind === "Series"
    ? sortReviewsByRank(typeReviews.filter((item) => item.kind === "Episode" && item.seriesId === review.mediaCollectionId))
    : [];
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
    const scoredFields = mapReviewScoreFields(fields, values.fields);
    if (scoredFields === null) {
      showError("Score every field from 1 to 10 before saving.");
      return;
    }
    const request: ReviewUpdateRequest = {
      id: review.id,
      reviewTitle: values.reviewTitle.trim() || null,
      notes: values.notes.trim() || null,
      consumedAt: values.consumedAt,
      fields: scoredFields,
    };

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
            {review.kind === "Series" ? "Series review" : review.kind === "Episode" ? "Episode review" : "Review"}
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
                  <Box sx={{ position: "relative", flexShrink: 0 }}>
                  <CoverTile
                  title={review.mediaTitle}
                  src={review.mediaCoverImageUrl}
                  status={review.coverStatus}
                  sx={{
                    width: 128,
                    height: 192,
                    borderRadius: 2,
                  }}
                  />
                  {review.kind === "Episode" ? <Box sx={{ position: "absolute", top: 8, left: 8, px: 0.75, py: 0.25, bgcolor: "background.default", borderRadius: 0.75, typography: "caption" }}>S{review.seasonNumber ?? "—"} · E{review.episodeNumber ?? "—"}</Box> : null}
                  </Box>
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
                    {getRankLabel(review, rank, currentReviewRanks?.totalCount ?? 0)}
                  </Box>
                  {review.kind === "Episode" && review.seriesTitle ? (
                    <Typography variant="body2" fontWeight={650} color="text.secondary">
                      {seriesReview ? (
                        <ButtonBase onClick={() => onOpenRelatedReview(seriesReview)} sx={{ color: "primary.light", font: "inherit", textAlign: "left" }}>
                          {getEpisodeContextLine(review)}
                        </ButtonBase>
                      ) : getEpisodeContextLine(review)}
                    </Typography>
                  ) : null}
                  <Typography
                    variant="h4"
                    component="h2"
                    sx={{ overflowWrap: "anywhere" }}
                  >
                    {review.mediaTitle}
                  </Typography>
                  <Typography color="text.secondary">
                    {review.kind === "Series"
                      ? `${formatSeriesYearRange(review.seriesStartYear ?? (year ? Number(year) : null), review.seriesEndYear, { includeEndOnly: true }) ?? ""} · TV series · ${review.seasonCount ?? 0} seasons, ${review.episodeCount ?? 0} episodes`
                      : [year, review.kind === "Episode" ? "TV episode" : mediaTypeLabel].filter(Boolean).join(" · ")}
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
              {review.kind === "Episode" && seriesReview ? (
                <Stack spacing={1.25} sx={{ mt: 3, pt: 3, borderTop: "1px solid", borderColor: "divider" }}>
                  <Typography variant="overline" color="text.secondary">Series review</Typography>
                  <RelatedReviewCard review={seriesReview} subtitle={getRankLabel(seriesReview, relatedSeriesRanks?.rankById.get(seriesReview.id) ?? 0, relatedSeriesRanks?.totalCount ?? 0)} onClick={() => onOpenRelatedReview(seriesReview)} />
                </Stack>
              ) : null}

              {review.kind === "Series" ? (
                <Stack spacing={1.25} sx={{ mt: 3, pt: 3, borderTop: "1px solid", borderColor: "divider" }}>
                  <Stack direction="row" alignItems="baseline" justifyContent="space-between" gap={1}>
                    <Typography variant="overline" color="text.secondary">Episodes you’ve reviewed · {episodeReviews.length}</Typography>
                    <Button component={Link} size="small" href={`/media?mediaType=TvShow&series=${review.mediaCollectionId}`} onClick={onClose}>Browse all episodes</Button>
                  </Stack>
                  {episodeReviews.map((episode) => {
                    const episodeRanks = reviewRankLookups.get(rankableGroup(episode));
                    const episodeRank = episodeRanks?.rankById.get(episode.id) ?? 0;
                    return <ButtonBase key={episode.id} onClick={() => onOpenRelatedReview(episode)} sx={{ display: "grid", width: "100%", gridTemplateColumns: "64px minmax(0, 1fr) auto", gap: 1.5, alignItems: "center", px: 1.5, py: 1.25, textAlign: "left", border: "1px solid", borderColor: "divider", borderRadius: 1.5, "&:hover": { bgcolor: "action.hover" } }}>
                      <Typography variant="numeric" color="text.secondary">S{episode.seasonNumber ?? "—"} E{episode.episodeNumber ?? "—"}</Typography>
                      <Stack sx={{ minWidth: 0 }}>
                        <Typography noWrap fontWeight={650}>{episode.mediaTitle}</Typography>
                        <Typography variant="caption" color="text.secondary">{getRankLabel(episode, episodeRank, episodeRanks?.totalCount ?? 0)} · {getReleaseYear(episode.mediaReleaseDate) ?? "Year unknown"}</Typography>
                      </Stack>
                      <Typography variant="numeric" color="primary.light">{episode.overallScore}</Typography>
                    </ButtonBase>;
                  })}
                  {episodeReviews.length === 0 ? <Typography variant="body2" color="text.secondary">No episode reviews yet.</Typography> : null}
                </Stack>
              ) : null}
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
    fields.every((field) => isReviewScore(scoreValues?.[field.id]));

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

function RelatedReviewCard({
  review,
  subtitle,
  onClick,
}: {
  review: ReviewDto;
  subtitle: string;
  onClick: () => void;
}) {
  return (
    <ButtonBase onClick={onClick} sx={{ display: "flex", width: "100%", justifyContent: "flex-start", textAlign: "left", gap: 1.5, p: 1.25, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default", "&:hover": { bgcolor: "action.hover" } }}>
      <CoverTile title={review.seriesTitle ?? review.mediaTitle} src={review.mediaCoverImageUrl} status={review.coverStatus} showTitle={false} sx={{ width: 42, height: 62, borderRadius: 1 }} />
      <Stack sx={{ flex: 1, minWidth: 0 }}>
        <Typography fontWeight={650} noWrap>{review.mediaTitle}</Typography>
        <Typography variant="caption" color="text.secondary" noWrap>{subtitle}</Typography>
      </Stack>
      <Typography variant="numeric" color="primary.light">{review.overallScore}</Typography>
      <ChevronRightIcon color="action" fontSize="small" />
    </ButtonBase>
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

"use client";

import { mapReviewScoreFields } from "./review-score-values";
import { buildReviewTarget, getEpisodeContextLine } from "./review-utils";
import { TvSeriesPicker } from "./tv-series-picker";

import AddIcon from "@mui/icons-material/Add";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import CloseIcon from "@mui/icons-material/Close";
import SearchIcon from "@mui/icons-material/Search";
import {
  Box,
  Button,
  ButtonBase,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import Link from "next/link";
import { FormProvider, useForm, useWatch } from "react-hook-form";
import { ReactNode, useState } from "react";
import { useQuery } from "@/lib/api/use-query";
import { reviewQueryOptions } from "./review-query";
import { usePagedQuery } from "@/lib/api/use-paged-query";
import { useMutation } from "@/lib/api/use-mutation";
import { usePendingCoverRefresh } from "@/lib/api/use-pending-cover-refresh";
import { useUser } from "@/lib/auth/user-provider";
import { MediaType, TemplateDto } from "@/lib/contracts/shared";
import { BaseTextField } from "@/lib/components/inputs/text-field/base-text-field";
import { BaseSelect } from "@/lib/components/inputs/select/base-select";
import { CoverTile } from "@/lib/components/data-display/cover-tile";
import { MediaTypeChips } from "@/lib/components/inputs/media-type-chips";
import { ReviewDto, ReviewInsertRequest } from "../contracts";
import { MediaCollectionDto, MediaDto } from "../../media/contracts";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import {
  getMediaTypeDisplayLabel,
  getOverallPreview,
  getReleaseYear,
} from "./review-utils";
import {
  ReviewScoreField,
  ReviewWritingFields,
  ReviewScoreFormValues,
} from "./review-score-form";

interface NewReviewDialogProps {
  initialMedia?: MediaDto;
  initialSeries?: MediaCollectionDto;
  onClose: () => void;
  onCreated: (review: ReviewDto) => void;
}

export function NewReviewDialog({
  initialMedia,
  initialSeries,
  onClose,
  onCreated,
}: NewReviewDialogProps) {
  const { userId, authResolved } = useUser();
  const { showSuccess, showError } = useAlert();
  const [mediaType, setMediaType] = useState<MediaType>(
    (initialMedia?.mediaType as MediaType | undefined) ??
      (initialSeries ? MediaType.TvShow : MediaType.VideoGame),
  );
  const [searchInput, setSearchInput] = useState("");
  const [selectedMedia, setSelectedMedia] = useState<MediaDto | null>(
    initialMedia ?? null,
  );
  const [selectedSeriesTarget, setSelectedSeriesTarget] = useState<MediaCollectionDto | null>(initialMedia ? null : initialSeries ?? null);
  const [seriesContext, setSeriesContext] = useState<MediaCollectionDto | null>(initialSeries ?? null);
  const [browsingSeries, setBrowsingSeries] = useState<MediaCollectionDto | null>(null);
  const [step, setStep] = useState<"choose" | "score">(
    initialMedia || initialSeries ? "score" : "choose",
  );
  const [selectedTemplateId, setSelectedTemplateId] = useState<number | null>(
    null,
  );

  const {
    items: mediaItems,
    totalCount,
    isLoading: isMediaQueryLoading,
    error: mediaError,
    refetch: refetchMedia,
  } = usePagedQuery<MediaDto>({
    route: "/api/media",
    routeParams: { mediaType },
    queryKey: ["media", mediaType],
    enabled: authResolved && !!userId && step === "choose" && mediaType !== MediaType.TvShow,
    pageSize: 10,
    minSearchChars: 1,
    pageRequest: {
      page: 0,
      searchField: "title",
      searchTerm: searchInput.trim(),
    },
  });

  const {
    data: reviewedMedia = [],
    isLoading: isReviewStatusQueryLoading,
    isError: isReviewStatusError,
  } = useQuery<ReviewDto[]>({
    ...reviewQueryOptions(mediaType),
    enabled: authResolved && !!userId,
  });

  const {
    data: templates = [],
    isLoading: areTemplatesQueryLoading,
    error: templatesError,
  } = useQuery<TemplateDto[]>({
    route: `/api/templates/${mediaType}`,
    queryKey: ["templates", mediaType],
    enabled: authResolved && !!userId && step === "score" && (!!selectedMedia || !!selectedSeriesTarget),
  });
  const isMediaLoading = !authResolved || isMediaQueryLoading;
  const isReviewStatusLoading = !authResolved || isReviewStatusQueryLoading;
  const areTemplatesLoading = !authResolved || areTemplatesQueryLoading;

  usePendingCoverRefresh({
    viewKey: `${mediaType}:${mediaItems.map((media) => media.id).join(",")}`,
    hasPendingCovers: mediaItems.some(
      (media) => media.coverStatus === "pending",
    ),
    refetch: refetchMedia,
    enabled: authResolved && !!userId && step === "choose" && mediaType !== MediaType.TvShow,
  });

  const { mutate: insertReview, isPending: isSaving } = useMutation<
    ReviewInsertRequest,
    ReviewDto
  >({
    route: "/api/reviews",
    method: "POST",
  });

  const selectedTemplate =
    templates.find((template) => template.id === selectedTemplateId) ??
    (templates.length === 1 ? templates[0] : null);
  const alreadyReviewedByMediaId = new Map(
    reviewedMedia.map((review) => [review.mediaId, review]),
  );

  const selectMedia = (media: MediaDto) => {
    if (media.mediaType !== mediaType || alreadyReviewedByMediaId.has(media.id))
      return;
    setSelectedMedia(media);
    setSelectedSeriesTarget(null);
    setSeriesContext(null);
    setSelectedTemplateId(null);
    setStep("score");
  };

  const changeSelectedMedia = () => {
    setSelectedTemplateId(null);
    if (seriesContext) setBrowsingSeries(seriesContext);
    setStep("choose");
  };

  const handleCreate = (values: ReviewScoreFormValues) => {
    if ((!selectedMedia && !selectedSeriesTarget) || !selectedTemplate) return;

    const fields = mapReviewScoreFields(
      [...selectedTemplate.fields].sort(
        (left, right) => left.position - right.position,
      ),
      values.fields,
    );
    if (fields === null) return;

    insertReview(
      {
        ...buildReviewTarget(selectedMedia, selectedSeriesTarget),
        templateId: selectedTemplate.id,
        reviewTitle: values.reviewTitle.trim() || null,
        notes: values.notes.trim() || null,
        consumedAt: null,
        fields,
      },
      {
        onSuccess: (saved) => {
          showSuccess("Review saved");
          onCreated(saved);
        },
        onError: (error) => showError(error.message),
      },
    );
  };

  const latestSearchMatches = mediaItems
    .filter((media) => media.mediaType === mediaType)
    .map((media) => ({
      media,
      review: alreadyReviewedByMediaId.get(media.id),
    }));

  const scoreTarget = selectedMedia ?? selectedSeriesTarget;
  const scoreHeader = scoreTarget ? (
    <>
      <Stack
        direction="row"
        alignItems="center"
        gap={2}
        sx={{
          p: 1.75,
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 2,
          bgcolor: "background.default",
        }}
      >
        <CoverTile
          title={seriesContext?.title ?? scoreTarget.title}
          src={scoreTarget.coverImageUrl}
          status={scoreTarget.coverStatus}
          sx={{
            width: 56,
            height: 84,
            borderRadius: 1,
            flexShrink: 0,
          }}
        />
        <Stack sx={{ flex: 1, minWidth: 0 }}>
          {seriesContext && !selectedSeriesTarget ? (
            <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>
              {getEpisodeContextLine({ kind: "Episode", seriesTitle: seriesContext.title, seasonNumber: selectedMedia?.seasonNumber, episodeNumber: selectedMedia?.episodeNumber })}
            </Typography>
          ) : null}
          <Typography fontWeight={700} noWrap>
            {scoreTarget.title}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {[
              selectedSeriesTarget
                ? selectedSeriesTarget.startYear?.toString()
                : getReleaseYear(selectedMedia?.releaseDate),
              selectedSeriesTarget ? "TV series" : selectedMedia?.seriesId ? "TV episode" : getMediaTypeDisplayLabel(mediaType),
            ]
              .filter(Boolean)
              .join(" · ")}
          </Typography>
        </Stack>
        <Button variant="outlined" onClick={changeSelectedMedia}>
          Change
        </Button>
      </Stack>
      {templates.length > 1 ? (
        <Box sx={{ maxWidth: 320 }}>
          <BaseSelect
            label="Template"
            value={selectedTemplate?.id ?? ""}
            options={templates.map((template) => ({
              id: template.id,
              label: template.name,
            }))}
            isLoading={areTemplatesLoading}
            onChange={(event) =>
              setSelectedTemplateId(Number(event.target.value))
            }
          />
        </Box>
      ) : null}
      {templates.length === 1 ? (
        <Typography variant="caption" color="text.secondary">
          Picked automatically — it’s your only{" "}
          {getMediaTypeDisplayLabel(mediaType).toLowerCase()} template.
        </Typography>
      ) : null}
    </>
  ) : null;

  return (
    <Dialog
      open
      onClose={onClose}
      maxWidth={false}
      aria-labelledby="new-review-title"
      PaperProps={{
        sx: {
          width: 860,
          maxWidth: "calc(100vw - 48px)",
          height: "min(788px, calc(100dvh - 64px))",
          maxHeight: "calc(100dvh - 64px)",
          borderRadius: 2.25,
          overflow: "hidden",
          display: "flex",
          flexDirection: "column",
        },
      }}
    >
      <DialogTitle
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 2.5,
          px: 3,
          py: 2,
          borderBottom: "1px solid",
          borderColor: "divider",
        }}
      >
        <Typography id="new-review-title" variant="h5" component="span">
          New review
        </Typography>
        <Stack
          direction="row"
          alignItems="center"
          spacing={1.25}
          sx={{ ml: 1 }}
        >
          <StepIndicator
            number={1}
            label="Choose a title"
            active={step === "choose"}
          />
          <Divider sx={{ width: 24 }} />
          <StepIndicator
            number={2}
            label="Score it"
            active={step === "score"}
          />
        </Stack>
        <Box sx={{ flex: 1 }} />
        <IconButton aria-label="Close new review" onClick={onClose}>
          <CloseIcon />
        </IconButton>
      </DialogTitle>

      {step === "choose" ? (
        <>
          <DialogContent
            sx={{
              flex: 1,
              minHeight: 0,
              display: "flex",
              flexDirection: "column",
              gap: 2,
              px: 3,
              py: 2.5,
            }}
          >
            {mediaType === MediaType.TvShow ? (
              <TvSeriesPicker
                search={searchInput}
                onSearch={setSearchInput}
                browsingSeries={browsingSeries}
                onBrowse={setBrowsingSeries}
              reviews={reviewedMedia}
              disabled={isReviewStatusLoading || isReviewStatusError}
              onMediaTypeChange={(nextType) => {
                setMediaType(nextType);
                setBrowsingSeries(null);
              }}
                onSelectSeries={(series) => {
                  setSelectedMedia(null);
                  setSelectedSeriesTarget(series);
                  setSeriesContext(series);
                  setSelectedTemplateId(null);
                  setStep("score");
                }}
                onSelectEpisode={(episode, series) => {
                  setSelectedSeriesTarget(null);
                  setSelectedMedia(episode);
                  setSeriesContext(series);
                  setSelectedTemplateId(null);
                  setStep("score");
                }}
              />
            ) : (
            <>
            <MediaTypeChips
              value={mediaType}
              onChange={(value) => setMediaType(value)}
            />
            <BaseTextField
              autoFocus
              type="search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Search the catalog"
              aria-label={`Search ${mediaType}`}
              InputProps={{
                startAdornment: (
                  <SearchIcon
                    fontSize="small"
                    sx={{ mr: 1, color: "text.secondary" }}
                  />
                ),
              }}
              sx={{
                "& .MuiOutlinedInput-root": {
                  height: 56,
                  bgcolor: "background.default",
                },
              }}
            />
            <Stack
              direction="row"
              justifyContent="space-between"
              alignItems="center"
            >
              <Typography variant="caption" color="text.secondary">
                {searchInput.trim()
                  ? `${totalCount || latestSearchMatches.length} best matches for “${searchInput.trim()}”`
                  : "Search the catalog to find a title"}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Already reviewed titles are marked
              </Typography>
            </Stack>
            <Box
              sx={{ flex: 1, minHeight: 0, overflow: "auto", mx: -1, px: 1 }}
            >
              {isReviewStatusError ? (
                <Typography color="error" sx={{ py: 3 }}>
                  Couldn’t check your existing reviews. Try again before
                  selecting a title.
                </Typography>
              ) : mediaError ? (
                <Typography color="error" sx={{ py: 3 }}>
                  {mediaError.message}
                </Typography>
              ) : isMediaLoading || isReviewStatusLoading ? (
                <Stack alignItems="center" sx={{ py: 5 }}>
                  <CircularProgress size={28} />
                </Stack>
              ) : latestSearchMatches.length > 0 ? (
                latestSearchMatches.map(({ media, review }) => (
                  <ButtonBase
                    key={media.id}
                    component="button"
                    disabled={!!review || isReviewStatusError}
                    onClick={() => selectMedia(media)}
                    sx={{
                      display: "flex",
                      width: "100%",
                      justifyContent: "flex-start",
                      textAlign: "left",
                      gap: 2,
                      px: 1,
                      py: 1,
                      borderRadius: 1.5,
                      opacity: review ? 0.48 : 1,
                      "&:hover:not(:disabled)": { bgcolor: "action.hover" },
                    }}
                  >
                    <CoverTile
                      title={media.title}
                      src={media.coverImageUrl}
                      status={media.coverStatus}
                      sx={{
                        width: 40,
                        height: 60,
                        borderRadius: 0.75,
                        flexShrink: 0,
                      }}
                    />
                    <Stack spacing={0.25} sx={{ flex: 1, minWidth: 0 }}>
                      <Typography fontWeight={650} noWrap>
                        {media.title}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        {[
                          getReleaseYear(media.releaseDate),
                          getMediaTypeDisplayLabel(mediaType),
                        ]
                          .filter(Boolean)
                          .join(" · ")}
                      </Typography>
                    </Stack>
                    {review ? (
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{ mr: 0.75 }}
                      >
                        Reviewed · {review.overallScore}
                      </Typography>
                    ) : (
                      <ChevronRightIcon fontSize="small" color="action" />
                    )}
                  </ButtonBase>
                ))
              ) : searchInput.trim() ? (
                <Stack
                  alignItems="center"
                  spacing={1}
                  sx={{ py: 6, textAlign: "center" }}
                >
                  <Typography fontWeight={700}>
                    Nothing matches “{searchInput.trim()}”
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Check the spelling, or add it to the catalog yourself.
                  </Typography>
                </Stack>
              ) : null}
            </Box>
            </>
            )}
          </DialogContent>
          <DialogActions
            sx={{
              borderTop: "1px solid",
              borderColor: "divider",
              px: 3,
              py: 1.5,
              justifyContent: "flex-start",
            }}
          >
            <Typography variant="body2" color="text.secondary">
              Can’t find it?{" "}
              <Button
                component={Link}
                href={`/media?mediaType=${mediaType}`}
                onClick={onClose}
                startIcon={<AddIcon />}
                size="small"
                sx={{ minWidth: 0, px: 0.5 }}
              >
                Add a title to the catalog
              </Button>
            </Typography>
          </DialogActions>
        </>
      ) : (
        <>
            {scoreTarget && selectedTemplate && !areTemplatesLoading ? (
            <NewReviewScoreForm
              key={`${selectedSeriesTarget?.id ?? selectedMedia?.id}-${selectedTemplate.id}`}
              templateName={selectedTemplate.name}
              templateFields={selectedTemplate.fields}
              onSubmit={handleCreate}
              isSaving={isSaving}
              onCancel={onClose}
              disabled={areTemplatesLoading}
            >
              {scoreHeader}
            </NewReviewScoreForm>
          ) : (
            <>
              <DialogContent sx={{ flex: 1, minHeight: 0, px: 3, py: 2.5 }}>
                <Stack spacing={2.5}>
                  {scoreHeader}
                  {templatesError ? (
                    <Typography color="error">
                      {templatesError.message}
                    </Typography>
                  ) : areTemplatesLoading ? (
                    <CircularProgress size={28} />
                  ) : templates.length === 0 ? (
                    <Typography color="text.secondary">
                      No template is available for this media type. Create one
                      in Templates first.
                    </Typography>
                  ) : null}
                </Stack>
              </DialogContent>
              <DialogActions
                sx={{
                  flexShrink: 0,
                  borderTop: "1px solid",
                  borderColor: "divider",
                  px: 3,
                  py: 1.5,
                }}
              >
                <Button onClick={onClose}>Cancel</Button>
                <Button variant="contained" disabled>
                  Save review
                </Button>
              </DialogActions>
            </>
          )}
        </>
      )}
    </Dialog>
  );
}

function StepIndicator({
  number,
  label,
  active,
}: {
  number: number;
  label: string;
  active: boolean;
}) {
  return (
    <Stack direction="row" alignItems="center" spacing={0.75}>
      <Box
        sx={{
          width: 22,
          height: 22,
          borderRadius: "50%",
          display: "grid",
          placeItems: "center",
          bgcolor: active ? "primary.main" : "action.hover",
          color: active ? "primary.contrastText" : "text.secondary",
          fontSize: 12,
          fontFamily: "var(--font-mono), monospace",
        }}
      >
        {number}
      </Box>
      <Typography
        variant="caption"
        color={active ? "text.primary" : "text.secondary"}
        fontWeight={600}
      >
        {label}
      </Typography>
    </Stack>
  );
}

function NewReviewScoreForm({
  children,
  templateName,
  templateFields,
  onSubmit,
  isSaving,
  onCancel,
  disabled,
}: {
  children: ReactNode;
  templateName: string;
  templateFields: ReviewScoreField[];
  onSubmit: (values: ReviewScoreFormValues) => void;
  isSaving: boolean;
  onCancel: () => void;
  disabled: boolean;
}) {
  const methods = useForm<ReviewScoreFormValues>({
    defaultValues: {
      reviewTitle: "",
      notes: "",
      consumedAt: null,
      fields: Object.fromEntries(
        templateFields.map((field) => [String(field.id), null]),
      ),
    },
  });
  const values = useWatch({ control: methods.control, name: "fields" });
  const preview = getOverallPreview(
    templateFields.map((field) => values?.[field.id]),
  );
  const canSave =
    templateFields.length > 0 &&
    preview.scoredCount === templateFields.length &&
    !isSaving &&
    !disabled;

  return (
    <FormProvider {...methods}>
      <Box
        component="form"
        id="new-review-score-form"
        onSubmit={methods.handleSubmit(onSubmit)}
        sx={{
          display: "flex",
          flexDirection: "column",
          flex: 1,
          minHeight: 0,
          overflow: "hidden",
        }}
      >
        <DialogContent
          sx={(theme) => ({
            flex: 1,
            minHeight: 0,
            overflow: "auto",
            px: 3,
            py: 2.5,
            scrollbarWidth: "thin",
            scrollbarColor: `${theme.palette.divider} transparent`,
          })}
        >
          <Stack spacing={2.5}>
            {children}
            <Box>
              <Typography
                variant="caption"
                fontWeight={650}
                sx={{ display: "block", mb: 0.75 }}
              >
                Template
              </Typography>
              <Typography variant="body2">{templateName}</Typography>
            </Box>
            <ReviewWritingFields fields={templateFields} />
          </Stack>
        </DialogContent>
        <DialogActions
          sx={{
            px: 3,
            py: 1.5,
            borderTop: "1px solid",
            borderColor: "divider",
            minHeight: 64,
            flexShrink: 0,
            bgcolor: "background.paper",
          }}
        >
          <Stack
            direction="row"
            alignItems="center"
            spacing={1.25}
            sx={{ flex: 1 }}
          >
            <Typography variant="caption" color="text.secondary">
              Overall
            </Typography>
            <Typography
              variant="numeric"
              sx={{ color: "primary.light", fontSize: 32, lineHeight: 1 }}
            >
              {preview.score ?? "–"}
            </Typography>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ fontFamily: "var(--font-mono), monospace" }}
            >
              {preview.scoredCount} of {preview.totalCount} scored
            </Typography>
          </Stack>
          <Button onClick={onCancel} disabled={isSaving}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={!canSave}
            loading={isSaving}
          >
            Save review
          </Button>
        </DialogActions>
      </Box>
    </FormProvider>
  );
}

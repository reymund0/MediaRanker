"use client";

import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import MovieOutlinedIcon from "@mui/icons-material/MovieOutlined";
import {
  Box,
  Button,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import Link from "next/link";
import { alpha } from "@mui/material/styles";
import { useState } from "react";
import { useQuery } from "@/lib/api/use-query";
import { reviewQueryOptions } from "./review-query";
import { usePendingCoverRefresh } from "@/lib/api/use-pending-cover-refresh";
import { useUser } from "@/lib/auth/user-provider";
import { CoverTile } from "@/lib/components/data-display/cover-tile";
import { ScoreBar } from "@/lib/components/data-display/score-bar";
import { PageContainer } from "@/lib/components/layout/page-container";
import { MediaType } from "@/lib/contracts/shared";
import { MediaTypeChips } from "@/lib/components/inputs/media-type-chips";
import { ReviewDto } from "../contracts";
import { useReviewExperience } from "./review-experience";
import {
  getMediaTypeDisplayLabel,
  getMediaTypePluralLabel,
  getReleaseYear,
  REVIEW_MEDIA_TYPES,
  sortReviewsByRank,
} from "./review-utils";

export function ReviewLibrary() {
  const { userId, authResolved } = useUser();
  const { openNewReview, openReview, isReviewOpen } = useReviewExperience();
  const [chosenType, setChosenType] = useState<MediaType | null>(null);

  const gamesQuery = useQuery<ReviewDto[]>({
    ...reviewQueryOptions(MediaType.VideoGame),
    enabled: !!userId,
  });
  const moviesQuery = useQuery<ReviewDto[]>({
    ...reviewQueryOptions(MediaType.Movie),
    enabled: !!userId,
  });
  const tvQuery = useQuery<ReviewDto[]>({
    ...reviewQueryOptions(MediaType.TvShow),
    enabled: !!userId,
  });
  const booksQuery = useQuery<ReviewDto[]>({
    ...reviewQueryOptions(MediaType.Book),
    enabled: !!userId,
  });
  const albumsQuery = useQuery<ReviewDto[]>({
    ...reviewQueryOptions(MediaType.Album),
    enabled: !!userId,
  });
  const concertsQuery = useQuery<ReviewDto[]>({
    ...reviewQueryOptions(MediaType.Concert),
    enabled: !!userId,
  });

  const reviewsByType: Record<MediaType, ReviewDto[]> = {
    [MediaType.VideoGame]: gamesQuery.data ?? [],
    [MediaType.Movie]: moviesQuery.data ?? [],
    [MediaType.TvShow]: tvQuery.data ?? [],
    [MediaType.Book]: booksQuery.data ?? [],
    [MediaType.Album]: albumsQuery.data ?? [],
    [MediaType.Concert]: concertsQuery.data ?? [],
  };
  const queries = [
    gamesQuery,
    moviesQuery,
    tvQuery,
    booksQuery,
    albumsQuery,
    concertsQuery,
  ];
  const reviewsQueriesByType = {
    [MediaType.VideoGame]: gamesQuery,
    [MediaType.Movie]: moviesQuery,
    [MediaType.TvShow]: tvQuery,
    [MediaType.Book]: booksQuery,
    [MediaType.Album]: albumsQuery,
    [MediaType.Concert]: concertsQuery,
  };
  const isLoading = !authResolved || queries.some((query) => query.isLoading);
  const error = queries.find((query) => query.error)?.error;
  const counts = Object.fromEntries(
    Object.entries(reviewsByType).map(([type, reviews]) => [
      type,
      reviews.length,
    ]),
  ) as Partial<Record<MediaType, number>>;
  const allReviews = REVIEW_MEDIA_TYPES.flatMap((type) => reviewsByType[type]);
  const activeType =
    chosenType ??
    REVIEW_MEDIA_TYPES.find((type) => reviewsByType[type].length > 0) ??
    MediaType.VideoGame;
  const rankedReviews = sortReviewsByRank(reviewsByType[activeType]);
  const latestReview = [...allReviews].sort(
    (left, right) =>
      new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime(),
  )[0];
  const latestRank = latestReview
    ? sortReviewsByRank(
        reviewsByType[latestReview.mediaType as MediaType],
      ).findIndex((review) => review.id === latestReview.id) + 1
    : 0;
  const latestMediaType = latestReview?.mediaType as MediaType | undefined;

  usePendingCoverRefresh({
    viewKey: `${activeType}:${rankedReviews
      .map((review) => review.id)
      .join(",")}`,
    hasPendingCovers: rankedReviews.some(
      (review) => review.coverStatus === "pending",
    ),
    refetch: reviewsQueriesByType[activeType].refetch,
    enabled: authResolved && !!userId && !isReviewOpen,
  });
  usePendingCoverRefresh({
    viewKey: `latest:${latestReview?.id ?? "none"}`,
    hasPendingCovers: latestReview?.coverStatus === "pending",
    refetch: reviewsQueriesByType[latestMediaType ?? activeType].refetch,
    enabled:
      authResolved &&
      !!userId &&
      !!latestReview &&
      !isReviewOpen &&
      latestMediaType !== activeType,
  });

  return (
    <PageContainer sx={{ width: "100%", maxWidth: 1280, py: 5 }}>
      <Stack spacing={3.25}>
        <Stack
          direction="row"
          justifyContent="space-between"
          alignItems="flex-end"
          gap={3}
        >
          <Box>
            <Typography
              variant="overline"
              sx={{
                color: "primary.light",
                fontFamily: "var(--font-mono), monospace",
                fontWeight: 700,
              }}
            >
              Library
            </Typography>
            <Typography variant="h1" component="h1">
              Your rankings
            </Typography>
            <Typography color="text.secondary" sx={{ mt: 0.75 }}>
              Everything you’ve scored, best first.
            </Typography>
          </Box>
        </Stack>

        <MediaTypeChips
          value={activeType}
          onChange={setChosenType}
          counts={counts}
        />

        {isLoading ? (
          <Stack alignItems="center" sx={{ py: 8 }}>
            <CircularProgress size={32} />
          </Stack>
        ) : error ? (
          <Typography color="error" sx={{ py: 3 }}>
            {error.message}
          </Typography>
        ) : allReviews.length === 0 ? (
          <EmptyLibrary onNewReview={() => openNewReview()} />
        ) : (
          <>
            {latestReview ? (
              <LatestReviewCard
                review={latestReview}
                rank={latestRank}
                totalCount={
                  reviewsByType[latestReview.mediaType as MediaType].length
                }
                onOpen={() => openReview(latestReview)}
                onEdit={() => openReview(latestReview, true)}
              />
            ) : null}

            <Stack spacing={1.5}>
              <Stack direction="row" alignItems="baseline" spacing={1.25}>
                <Typography variant="h4" component="h2">
                  {getMediaTypePluralLabel(activeType)}
                </Typography>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ fontFamily: "var(--font-mono), monospace" }}
                >
                  {rankedReviews.length} ranked
                </Typography>
              </Stack>

              {rankedReviews.length === 0 ? (
                <EmptyTypePrompt
                  mediaType={activeType}
                  browseHref={`/media?mediaType=${activeType}`}
                />
              ) : (
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: "repeat(6, minmax(0, 1fr))",
                    gap: 2.5,
                    "@media (max-width: 1050px)": {
                      gridTemplateColumns: "repeat(4, minmax(0, 1fr))",
                    },
                    "@media (max-width: 680px)": {
                      gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
                    },
                  }}
                >
                  {rankedReviews.map((review, index) => (
                    <RankedReviewTile
                      key={review.id}
                      review={review}
                      rank={index + 1}
                      highlighted={index === 0}
                      onClick={() => openReview(review)}
                    />
                  ))}
                </Box>
              )}
            </Stack>
          </>
        )}
      </Stack>
    </PageContainer>
  );
}

function LatestReviewCard({
  review,
  rank,
  totalCount,
  onOpen,
  onEdit,
}: {
  review: ReviewDto;
  rank: number;
  totalCount: number;
  onOpen: () => void;
  onEdit: () => void;
}) {
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: "200px minmax(0, 1fr) 184px",
        gap: 4,
        p: 3,
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 2,
        bgcolor: "background.paper",
        alignItems: "stretch",
        "@media (max-width: 1000px)": {
          gridTemplateColumns: "140px minmax(0,1fr)",
          gap: 2.5,
        },
      }}
    >
      <CoverTile
        title={review.mediaTitle}
        src={review.mediaCoverImageUrl}
        status={review.coverStatus}
        sx={{ width: "100%", height: 300, borderRadius: 1.5 }}
      />
      <Stack spacing={1.25} sx={{ minWidth: 0, py: 0.25 }}>
        <Stack direction="row" spacing={1} alignItems="baseline">
          <Typography
            variant="overline"
            sx={{
              color: "primary.light",
              fontFamily: "var(--font-mono), monospace",
              fontWeight: 700,
            }}
          >
            Latest review
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {new Intl.DateTimeFormat(undefined, {
              month: "short",
              day: "numeric",
              year: "numeric",
            }).format(new Date(review.updatedAt))}
          </Typography>
        </Stack>
        <Typography
          variant="h3"
          component="h2"
          sx={{
            fontFamily: "var(--font-display), sans-serif",
            fontSize: 40,
            fontWeight: 800,
            lineHeight: 1.02,
            letterSpacing: "-0.04em",
            overflowWrap: "anywhere",
          }}
        >
          {review.mediaTitle}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {[
            getReleaseYear(review.mediaReleaseDate),
            getMediaTypeDisplayLabel(review.mediaType),
            `#${rank} of ${totalCount}`,
          ]
            .filter(Boolean)
            .join(" · ")}
        </Typography>
        {review.reviewTitle ? (
          <Typography variant="h6" sx={{ mt: 0.5 }}>
            “{review.reviewTitle}”
          </Typography>
        ) : null}
        {review.notes ? (
          <Typography
            color="text.secondary"
            sx={{ maxWidth: 620, lineHeight: 1.7 }}
          >
            {review.notes.length > 150
              ? `${review.notes.slice(0, 150).trimEnd()}…`
              : review.notes}
          </Typography>
        ) : null}
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
            columnGap: 3,
            rowGap: 1.5,
            mt: "auto",
            pt: 1,
          }}
        >
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
        </Box>
      </Stack>
      <Stack
        alignItems="center"
        justifyContent="center"
        spacing={1}
        sx={{
          textAlign: "center",
          "@media (max-width: 1000px)": {
            gridColumn: "1 / -1",
            flexDirection: "row",
            justifyContent: "flex-end",
          },
        }}
      >
        <Typography variant="caption" color="text.secondary">
          Overall
        </Typography>
        <Typography
          variant="numeric"
          sx={{
            color: "primary.light",
            fontFamily: "var(--font-mono), monospace",
            fontSize: 104,
            fontWeight: 800,
            lineHeight: 0.9,
            letterSpacing: "-0.04em",
          }}
        >
          {review.overallScore}
        </Typography>
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ fontFamily: "var(--font-mono), monospace" }}
        >
          out of 10
        </Typography>
        <Stack direction="row" spacing={1} sx={{ pt: 1 }}>
          <Button
            variant="outlined"
            onClick={onOpen}
            sx={{ whiteSpace: "nowrap", minWidth: "max-content" }}
          >
            Open review
          </Button>
          <Button
            variant="outlined"
            aria-label="Edit latest review"
            onClick={onEdit}
            sx={{ minWidth: 44, px: 1 }}
          >
            <EditOutlinedIcon fontSize="small" />
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
}

function RankedReviewTile({
  review,
  rank,
  highlighted,
  onClick,
}: {
  review: ReviewDto;
  rank: number;
  highlighted: boolean;
  onClick: () => void;
}) {
  const year = getReleaseYear(review.mediaReleaseDate);
  return (
    <Stack spacing={1} sx={{ minWidth: 0 }}>
      <Box
        component="button"
        type="button"
        onClick={onClick}
        aria-label={`Open review for ${review.mediaTitle}, rank ${rank}, score ${review.overallScore}`}
        sx={{
          position: "relative",
          width: "100%",
          aspectRatio: "2 / 3",
          p: 0,
          overflow: "hidden",
          borderRadius: 1.5,
          border: highlighted ? "2px solid" : "1px solid",
          borderColor: highlighted ? "primary.light" : "divider",
          bgcolor: "background.paper",
          cursor: "pointer",
          textAlign: "left",
          transition: "transform 150ms ease, border-color 150ms ease",
          "&:hover": {
            transform: "translateY(-2px)",
            borderColor: "primary.light",
          },
          "&:focus-visible": {
            outline: "2px solid",
            outlineColor: "primary.light",
            outlineOffset: 3,
          },
        }}
      >
        <CoverTile
          title={review.mediaTitle}
          src={review.mediaCoverImageUrl}
          status={review.coverStatus}
          sx={{ width: "100%", height: "100%", borderRadius: 0 }}
        />
        <Box
          sx={{
            position: "absolute",
            top: 9,
            left: 9,
            px: 0.8,
            py: 0.35,
            borderRadius: 0.75,
            bgcolor: (theme) => alpha(theme.palette.background.default, 0.9),
            color: "common.white",
            fontFamily: "var(--font-mono), monospace",
            fontSize: 12,
            fontWeight: 700,
          }}
        >
          #{rank}
        </Box>
        {year && !hasReadyCover(review) ? (
          <Typography
            variant="caption"
            sx={{
              position: "absolute",
              top: 9,
              right: 9,
              color: "common.white",
              fontFamily: "var(--font-mono), monospace",
              textShadow: (theme) => `0 1px 4px ${theme.palette.common.black}`,
            }}
          >
            {year}
          </Typography>
        ) : null}
        {!hasReadyCover(review) ? (
          <Typography
            sx={{
              position: "absolute",
              left: 12,
              right: 48,
              bottom: 12,
              color: "common.white",
              fontWeight: 750,
              fontSize: 19,
              lineHeight: 1.05,
              textShadow: (theme) => `0 1px 5px ${theme.palette.common.black}`,
            }}
          >
            {review.mediaTitle}
          </Typography>
        ) : null}
        <Box
          sx={{
            position: "absolute",
            right: 9,
            bottom: 9,
            px: 0.9,
            py: 0.45,
            borderRadius: 1,
            bgcolor: "primary.main",
            color: "primary.contrastText",
            fontFamily: "var(--font-mono), monospace",
            fontWeight: 700,
            fontSize: 14,
          }}
        >
          {review.overallScore}
        </Box>
      </Box>
      <Typography fontWeight={700} noWrap title={review.mediaTitle}>
        {review.mediaTitle}
      </Typography>
      {year ? (
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ mt: "-4px !important" }}
        >
          {year}
        </Typography>
      ) : null}
    </Stack>
  );
}

function EmptyLibrary({ onNewReview }: { onNewReview: () => void }) {
  return (
    <Box
      sx={{
        minHeight: 320,
        display: "grid",
        placeItems: "center",
        border: "1px dashed",
        borderColor: "divider",
        borderRadius: 2,
      }}
    >
      <Stack alignItems="center" spacing={1.5} sx={{ textAlign: "center" }}>
        <Box
          sx={{
            width: 52,
            height: 52,
            borderRadius: 2,
            bgcolor: "action.hover",
            display: "grid",
            placeItems: "center",
            color: "primary.light",
          }}
        >
          <MovieOutlinedIcon />
        </Box>
        <Typography variant="h5">No reviews yet</Typography>
        <Typography color="text.secondary">
          Start your library by reviewing something you love.
        </Typography>
        <Button variant="contained" onClick={onNewReview}>
          New review
        </Button>
      </Stack>
    </Box>
  );
}

function hasReadyCover(review: ReviewDto): boolean {
  return review.coverStatus === "ready" && !!review.mediaCoverImageUrl;
}

function EmptyTypePrompt({
  mediaType,
  browseHref,
}: {
  mediaType: MediaType;
  browseHref: string;
}) {
  return (
    <Box
      sx={{
        p: 2.5,
        border: "1px dashed",
        borderColor: "divider",
        borderRadius: 2,
        display: "flex",
        alignItems: "center",
        gap: 1.5,
      }}
    >
      <Box
        sx={{
          width: 48,
          height: 48,
          borderRadius: 1.5,
          bgcolor: "action.hover",
          color: "primary.light",
          display: "grid",
          placeItems: "center",
        }}
      >
        <MovieOutlinedIcon />
      </Box>
      <Stack sx={{ flex: 1 }}>
        <Typography fontWeight={700}>
          No {getMediaTypePluralLabel(mediaType).toLowerCase()} ranked yet
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Browse the catalog and add your first review.
        </Typography>
      </Stack>
      <Button component={Link} href={browseHref} variant="outlined">
        Browse {getMediaTypePluralLabel(mediaType).toLowerCase()}
      </Button>
    </Box>
  );
}

import type { ReviewDto } from "../contracts";
import {
  ALL_MEDIA_TYPES,
  MediaType,
  MEDIA_TYPE_LABELS,
} from "@/lib/contracts/shared";

export const REVIEW_MEDIA_TYPES = ALL_MEDIA_TYPES;

const MEDIA_TYPE_DISPLAY_LABELS: Record<MediaType, string> = {
  [MediaType.VideoGame]: "Video game",
  [MediaType.Book]: "Book",
  [MediaType.Movie]: "Movie",
  [MediaType.TvShow]: "TV show",
  [MediaType.Album]: "Album",
  [MediaType.Concert]: "Concert",
};

const MEDIA_TYPE_PLURAL_LABELS: Record<MediaType, string> = {
  [MediaType.VideoGame]: "Video games",
  [MediaType.Book]: "Books",
  [MediaType.Movie]: "Movies",
  [MediaType.TvShow]: "TV shows",
  [MediaType.Album]: "Albums",
  [MediaType.Concert]: "Concerts",
};

export function getMediaTypeDisplayLabel(mediaType: string): string {
  return (
    MEDIA_TYPE_DISPLAY_LABELS[mediaType as MediaType] ??
    MEDIA_TYPE_LABELS[mediaType as MediaType] ??
    mediaType
  );
}

export function getMediaTypePluralLabel(mediaType: string): string {
  return (
    MEDIA_TYPE_PLURAL_LABELS[mediaType as MediaType] ??
    getMediaTypeDisplayLabel(mediaType)
  );
}

export function getOverallPreview(values: Array<number | null | undefined>): {
  score: number | null;
  scoredCount: number;
  totalCount: number;
} {
  const scored = values.filter(
    (value): value is number =>
      typeof value === "number" && Number.isFinite(value),
  );

  return {
    score: scored.length
      ? roundToEven(
          scored.reduce((sum, value) => sum + value, 0) / scored.length,
        )
      : null,
    scoredCount: scored.length,
    totalCount: values.length,
  };
}

export function getReleaseYear(
  releaseDate: string | null | undefined,
): string | null {
  if (!releaseDate) return null;
  return /^\d{4}/.exec(releaseDate)?.[0] ?? null;
}

export function formatReviewDate(
  date: string | null | undefined,
): string | null {
  if (!date) return null;
  const parsed = new Date(date);
  if (Number.isNaN(parsed.getTime())) return null;
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
  }).format(parsed);
}

/** Round to the nearest integer, resolving exact halves to the nearest even integer. */
export function roundToEven(value: number) {
  const lower = Math.floor(value);
  const fraction = value - lower;

  if (fraction < 0.5) return lower;
  if (fraction > 0.5) return lower + 1;
  return lower % 2 === 0 ? lower : lower + 1;
}

const TYPE_PLURALS: Record<string, string> = {
  VideoGame: "video games",
  Book: "books",
  Movie: "movies",
  TvShow: "TV shows",
  Album: "albums",
  Concert: "concerts",
};

export function rankableGroup(review: ReviewDto) {
  return `${review.mediaType}:${review.mediaType === "TvShow" ? review.kind : "Title"}`;
}

export function sortReviewsByRank(reviews: ReviewDto[]) {
  return [...reviews].sort((left, right) => {
    const scoreDifference = right.overallScore - left.overallScore;
    if (scoreDifference !== 0) return scoreDifference;
    const updatedDifference =
      new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime();
    if (updatedDifference !== 0) return updatedDifference;
    return left.id - right.id;
  });
}

export function getReviewGroup(reviews: ReviewDto[], review: ReviewDto) {
  const key = rankableGroup(review);
  return sortReviewsByRank(
    reviews.filter((item) => rankableGroup(item) === key),
  );
}

export function buildReviewRankLookups(reviews: ReviewDto[]) {
  const groups = new Map<string, ReviewDto[]>();
  for (const review of reviews) {
    const key = rankableGroup(review);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key)!.push(review);
  }

  return new Map(
    [...groups].map(([key, group]) => {
      const ranked = sortReviewsByRank(group);
      return [
        key,
        {
          totalCount: ranked.length,
          rankById: new Map(ranked.map((item, index) => [item.id, index + 1])),
        },
      ];
    }),
  );
}

export function formatSeriesYearRange(
  startYear: number | null | undefined,
  endYear: number | null | undefined,
  { includeEndOnly = false } = {},
) {
  if (startYear == null) {
    return includeEndOnly && endYear != null ? `–${endYear}` : null;
  }
  if (endYear == null || startYear === endYear) return String(startYear);
  return `${startYear}–${endYear}`;
}

export function getRankLabel(review: ReviewDto, rank: number, total: number) {
  if (review.mediaType === "TvShow") {
    return `#${rank} of ${total} ${review.kind === "Series" ? "TV series" : "TV episodes"}`;
  }
  return `#${rank} of ${total} ${TYPE_PLURALS[review.mediaType] ?? review.mediaType}`;
}

export function getEpisodeContextLine(
  review: Pick<ReviewDto, "kind" | "seriesTitle"> &
    Partial<Pick<ReviewDto, "seasonNumber" | "episodeNumber">>,
) {
  if (review.kind !== "Episode" || !review.seriesTitle) return null;
  return `${review.seriesTitle} · Season ${review.seasonNumber ?? "—"}, Episode ${review.episodeNumber ?? "—"}`;
}

export function buildReviewTarget(
  media: { id: number } | null,
  series: { id: number } | null,
) {
  return series && !media
    ? { mediaId: null, mediaCollectionId: series.id }
    : { mediaId: media?.id ?? null, mediaCollectionId: null };
}

import {
  ALL_MEDIA_TYPES,
  MediaType,
  MEDIA_TYPE_LABELS,
} from "@/lib/contracts/shared";
import { ReviewDto } from "../contracts";
import { roundToEven } from "./review-rounding.mjs";

export { roundToEven } from "./review-rounding.mjs";

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

export function sortReviewsByRank(reviews: ReviewDto[]): ReviewDto[] {
  return [...reviews].sort((left, right) => {
    const scoreDifference = right.overallScore - left.overallScore;
    if (scoreDifference !== 0) return scoreDifference;

    const updatedDifference =
      new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime();
    if (updatedDifference !== 0) return updatedDifference;

    return left.id - right.id;
  });
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

export function getCoverTileStatus(
  status: string | null | undefined,
): "ready" | "pending" | "missing" {
  if (status === "ready") return "ready";
  if (status === "pending") return "pending";
  return "missing";
}

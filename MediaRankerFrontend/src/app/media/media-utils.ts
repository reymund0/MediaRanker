import type { MediaCollectionUpsertRequest } from "./contracts";

export function buildSeriesUpsertRequest({
  id,
  title,
  startYear,
}: {
  id: number | null;
  title: string;
  startYear: number;
}): MediaCollectionUpsertRequest {
  return {
    id: id ?? null,
    title: title.trim(),
    collectionType: 0,
    mediaType: "TvShow",
    parentMediaCollectionId: null,
    releaseDate: `${startYear}-01-01`,
  };
}

export const TV_EPISODE_PAGE_SIZE = 25;

export function mergeEpisodePages<T extends { id: number }>(
  previous: T[],
  next: T[],
): T[] {
  const merged = new Map(previous.map((item) => [item.id, item]));
  next.forEach((item) => merged.set(item.id, item));
  return [...merged.values()];
}

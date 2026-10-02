import type { CoverStatus } from "@/lib/contracts/shared";

export type { CoverStatus } from "@/lib/contracts/shared";

export interface MediaDto {
  id: number;
  title: string;
  releaseDate: string | null;
  createdAt: string;
  updatedAt: string;
  mediaType: string;
  coverImageUrl?: string | null;
  coverStatus: CoverStatus;
  episodeNumber?: number | null;
  seasonNumber?: number | null;
  seriesId?: number | null;
  seriesTitle?: string | null;
}

export interface MediaCollectionDto {
  id: number;
  title: string;
  collectionType: "Series" | "Season" | string;
  mediaType: string;
  parentMediaCollectionId: number | null;
  parentMediaCollectionTitle?: string | null;
  releaseDate: string | null;
  seasonNumber: number | null;
  seasonCount: number | null;
  episodeCount: number | null;
  startYear: number | null;
  endYear: number | null;
  createdAt: string;
  updatedAt: string;
  coverImageUrl?: string | null;
  coverStatus: CoverStatus;
}

export interface SeriesRemovalCountsDto {
  episodeCount: number;
  reviewCount: number;
}

export interface MediaCollectionUpsertRequest {
  id: number | null;
  title: string;
  collectionType: number;
  mediaType: string;
  parentMediaCollectionId: number | null;
  releaseDate: string;
}

export interface MediaUpsertRequest {
  id: number | null;
  title: string;
  mediaType: string;
  releaseDate: string;
}

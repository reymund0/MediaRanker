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
}

export interface MediaUpsertRequest {
  id: number | null;
  title: string;
  mediaType: string;
  releaseDate: string;
}

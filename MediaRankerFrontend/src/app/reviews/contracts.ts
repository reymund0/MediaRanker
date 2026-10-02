import type { CoverStatus } from "@/lib/contracts/shared";

export type { CoverStatus } from "@/lib/contracts/shared";

export interface ReviewDto {
  id: number;
  userId: string;
  overallScore: number;
  reviewTitle: string | null;
  notes: string | null;
  consumedAt: string | null;
  createdAt: string;
  updatedAt: string;
  fields: ReviewFieldDto[];
  templateId: number;
  templateName: string;
  mediaId: number;
  mediaTitle: string;
  mediaType: string;
  mediaReleaseDate?: string | null;
  mediaCoverImageUrl: string | null;
  coverStatus: CoverStatus;
}

export interface ReviewFieldDto {
  reviewId: number;
  templateFieldId: number;
  templateFieldName: string;
  templateFieldPosition: number;
  value: number;
}

export interface UnreviewedMediaDto {
  id: number;
  title: string;
  releaseDate: string | null;
  coverImageUrl: string | null;
  coverStatus: CoverStatus;
}

export interface ReviewFieldUpsertRequest {
  templateFieldId: number;
  value: number;
}

export interface ReviewInsertRequest {
  mediaId: number;
  mediaType: string;
  templateId: number;
  reviewTitle: string | null;
  notes: string | null;
  consumedAt: string | null;
  fields: ReviewFieldUpsertRequest[];
}

export interface ReviewUpdateRequest {
  id: number;
  reviewTitle: string | null;
  notes: string | null;
  consumedAt: string | null;
  fields: ReviewFieldUpsertRequest[];
}

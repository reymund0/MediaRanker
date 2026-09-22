export interface MediaDto {
  id: number;
  title: string;
  releaseDate: string | null;
  createdAt: string;
  updatedAt: string;
  mediaTypeId: number;
  mediaTypeName: string;
  coverImageUrl?: string | null;
  coverStatus: CoverStatus;
}

export type CoverStatus =
  | "ready"
  | "pending"
  | "missing"
  | "failed"
  | "disabled"
  | "unsupported";

export interface MediaUpsertRequest {
  id: number | null;
  title: string;
  mediaTypeId: number;
  releaseDate: string;
}

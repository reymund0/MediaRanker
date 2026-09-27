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
  mediaType: string;
  releaseDate: string;
}

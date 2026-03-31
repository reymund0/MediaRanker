import { MediaTypeDto } from "@/lib/contracts/shared";

export interface MediaDto {
  id: number;
  title: string;
  releaseDate: string;
  createdAt: string;
  updatedAt: string;
  mediaType: MediaTypeDto;
  coverImageUrl?: string;
}

export interface MediaUpsertRequest {
  id: number | null;
  title: string;
  mediaTypeId: number;
  releaseDate: string;
  coverUploadId?: number;
}

export interface GenerateUploadCoverUrlRequest {
  mediaId: number | null;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
}

export interface GenerateUploadCoverUrlResponse {
  url: string;
  uploadId: number;
}

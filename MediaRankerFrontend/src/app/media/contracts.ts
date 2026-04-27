export interface MediaDto {
  id: number;
  title: string;
  releaseDate: string | null;
  createdAt: string;
  updatedAt: string;
  mediaTypeId: number;
  mediaTypeName: string;
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

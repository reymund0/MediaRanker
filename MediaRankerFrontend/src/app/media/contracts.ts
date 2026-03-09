export interface MediaTypeDto {
  id: number;
  name: string;
}

export interface MediaDto {
  id: number;
  title: string;
  releaseDate: string;
  createdAt: string;
  updatedAt: string;
  mediaType: MediaTypeDto;
}

export interface MediaUpsertRequest {
  id: number | null;
  title: string;
  mediaTypeId: number;
  releaseDate: string;
}

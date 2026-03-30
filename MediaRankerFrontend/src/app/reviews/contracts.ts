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
  mediaTypeId: number;
  mediaTypeName: string;
  mediaCoverImageUrl: string | null;
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
  releaseDate: string;
  coverImageUrl: string | null;
}

export interface ReviewFieldUpsertRequest {
  templateFieldId: number;
  value: number;
}

export interface ReviewInsertRequest {
  mediaId: number;
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
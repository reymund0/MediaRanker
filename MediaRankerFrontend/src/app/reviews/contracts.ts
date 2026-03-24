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
  id: number;
  reviewId: number;
  templateFieldId: number;
  score: number;
  createdAt: string;
  updatedAt: string;
}

export interface UnreviewedMediaDto {
  id: number;
  title: string;
  releaseDate: string;
  coverImageUrl: string | null;
}
export interface RankedMediaDto {
  id: number;
  userId: string;
  overallScore: number;
  reviewTitle: string | null;
  notes: string | null;
  consumedAt: string | null;
  createdAt: string;
  updatedAt: string;
  scores: RankedMediaScoreDto[];
  templateId: number;
  templateName: string;
  mediaId: number;
  mediaTitle: string;
  mediaTypeId: number;
  mediaTypeName: string;
}

export interface RankedMediaScoreDto {
  id: number;
  rankedMediaId: number;
  templateFieldId: number;
  score: number;
  createdAt: string;
  updatedAt: string;
}

export interface UnreviewedMediaDto {
  id: number;
  title: string;
  releaseDate: string;
}
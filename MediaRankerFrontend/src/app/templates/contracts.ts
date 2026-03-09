import { MediaTypeDto } from '@/lib/contracts/shared';

// DTOS

export interface TemplateFieldDto {
  id: number;
  name: string;
  position: number;
}

export interface TemplateDto {
  id: number;
  isSystem: boolean;
  userId: string;
  name: string;
  description: string | null;
  createdAt: Date;
  updatedAt: Date;
  fields: TemplateFieldDto[];
  mediaType: MediaTypeDto;
}

// API Request Contracts

export interface TemplateFieldUpsertRequest {
  id: number | null;
  name: string;
  position: number;
}

export interface TemplateUpsertRequest {
  id: number | null;
  mediaTypeId: number;
  name: string;
  description: string | null;
  fields: TemplateFieldUpsertRequest[];
}

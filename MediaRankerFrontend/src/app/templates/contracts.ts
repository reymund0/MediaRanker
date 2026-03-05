// I need some contracts in here.

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
  createdAt: string;
  updatedAt: string;
  fields: TemplateFieldDto[];
}

// API Request Contracts

export interface TemplateFieldUpsertRequest {
  id: number | null;
  name: string;
  position: number;
}

export interface TemplateUpsertRequest {
  id: number | null;
  name: string;
  description: string | null;
  fields: TemplateFieldUpsertRequest[];
}
// If this grows too large, split into module-based files.

export interface MediaTypeDto {
  id: number;
  name: string;
}

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
  mediaTypeId: number;
  mediaTypeName: string;
}

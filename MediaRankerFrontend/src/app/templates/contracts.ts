export type { TemplateFieldDto, TemplateDto } from "@/lib/contracts/shared";

// API Request Contracts

export interface TemplateFieldUpsertRequest {
  id: number | null;
  name: string;
  position: number;
}

export interface TemplateUpsertRequest {
  id: number | null;
  mediaType: string;
  name: string;
  description: string | null;
  fields: TemplateFieldUpsertRequest[];
}

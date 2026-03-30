import type { ReviewDto } from "./contracts";

// ---------------------------------------------------------------------------
// Layout constants
// ---------------------------------------------------------------------------

export const CARD_WIDTH = 260;
export const CARD_GAP = 16;
export const CARD_HEIGHT = 380;
export const COVER_HEIGHT = Math.round(CARD_HEIGHT * 0.6);
export const INFO_HEIGHT = CARD_HEIGHT - COVER_HEIGHT;

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type CardState = "view" | "detailed-view" | "edit";
export type NewStep = "select-media" | "select-template" | "edit";

export type ReviewFormValues = {
  reviewTitle?: string;
  notes?: string;
  fields: Record<string, number>;
};

type ExistingCardProps = {
  review: ReviewDto;
  isNew?: never;
  mediaTypeId?: never;
  onSave?: never;
  onCancel?: never;
  onUpdate: (updated: ReviewDto) => void;
  onDelete: (reviewId: number) => void;
};

type NewCardProps = {
  review?: never;
  isNew: true;
  mediaTypeId: number;
  onSave: (review: ReviewDto) => void;
  onCancel: () => void;
  onUpdate?: never;
  onDelete?: never;
};

export type ReviewCardProps = ExistingCardProps | NewCardProps;

import type { ReviewDto } from "./contracts";
import { ReviewFormValues } from "./review-card-schema";

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


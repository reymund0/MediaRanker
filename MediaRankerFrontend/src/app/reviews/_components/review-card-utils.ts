// ---------------------------------------------------------------------------
// Layout constants
// ---------------------------------------------------------------------------

const EXPANDED_MULTIPLIER = 1.5;
export const CARD_WIDTH = 200;
export const EXPANDED_CARD_WIDTH = CARD_WIDTH * EXPANDED_MULTIPLIER;
export const CARD_GAP = 16;
export const CARD_HEIGHT = 280;
export const EXPANDED_CARD_HEIGHT = CARD_HEIGHT * EXPANDED_MULTIPLIER;
export const COVER_HEIGHT = Math.round(CARD_HEIGHT * 0.8);
export const INFO_HEIGHT = CARD_HEIGHT - COVER_HEIGHT;

// ---------------------------------------------------------------------------
// Validation Schema
// ---------------------------------------------------------------------------

import { z } from "zod";

export const ReviewEditSchema = z
  .object({
    id: z.number().optional(),
    mediaId: z.number().optional(),
    templateId: z.number().optional(),
    reviewTitle: z.string().optional(),
    notes: z.string().optional(),
    fields: z.record(z.string(), z.number().min(1, "Min 1").max(10, "Max 10")),
  })
  .refine((review) => {
    // If a review is new (no id) then a mediaId and templateId is required
    if (!review.id && (!review.mediaId || !review.templateId)) {
      console.error(
        "Developer error: Review is new but missing mediaId or templateId",
      );
      return false;
    }
    return true;
  });

export type ReviewFormValues = z.infer<typeof ReviewEditSchema>;

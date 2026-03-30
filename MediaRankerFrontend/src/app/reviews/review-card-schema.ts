import { z } from "zod";

export const ReviewEditSchema = z.object({
    id: z.number().optional(),
    mediaId: z.number().optional(),
    templateId: z.number().optional(),
    reviewTitle: z.string().optional(),
    notes: z.string().optional(),
    fields: z.record(z.string(), z.number().min(1, "Min 1").max(10, "Max 10")),
  }).refine((review) => {
    // If a review is new (no id) then a mediaId and templateId is required
    if (!review.id && (!review.mediaId || !review.templateId)) {
      console.error("Developer error: Review is new but missing mediaId or templateId");
      return false;
    }
    return true;
  });


export type ReviewFormValues = z.infer<typeof ReviewEditSchema>;

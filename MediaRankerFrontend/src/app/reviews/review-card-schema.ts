import { z } from "zod";

export const buildReviewSchema = (fieldIds: number[]) =>
  z.object({
    reviewTitle: z.string().optional(),
    notes: z.string().optional(),
    fields: z.object(
      Object.fromEntries(
        fieldIds.map((id) => [
          String(id),
          z
            .number({ message: "Score required" })
            .min(1, "Min 1")
            .max(10, "Max 10"),
        ]),
      ),
    ),
  });

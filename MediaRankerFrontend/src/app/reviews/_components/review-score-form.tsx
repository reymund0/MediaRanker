"use client";

import { Box, Divider, Stack, Typography } from "@mui/material";
import { Path, useFormContext, useWatch } from "react-hook-form";
import { FormScoreInput } from "@/lib/components/inputs/score-input/form-score-input";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";

export interface ReviewScoreFormValues {
  reviewTitle: string;
  notes: string;
  consumedAt: string | null;
  fields: Record<string, number | null>;
}

export interface ReviewScoreField {
  id: number;
  name: string;
  position: number;
}

export function ReviewScoreFields({ fields }: { fields: ReviewScoreField[] }) {
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
        gap: 2,
      }}
    >
      {[...fields]
        .sort((left, right) => left.position - right.position)
        .map((field) => (
          <FormScoreInput<ReviewScoreFormValues>
            key={field.id}
            name={`fields.${field.id}` as Path<ReviewScoreFormValues>}
            label={field.name}
          />
        ))}
    </Box>
  );
}

export function ReviewWritingFields({
  fields,
}: {
  fields: ReviewScoreField[];
}) {
  const { control } = useFormContext<ReviewScoreFormValues>();
  const notes = useWatch({ control, name: "notes" });
  const wordCount = notes?.trim() ? notes.trim().split(/\s+/u).length : 0;

  return (
    <Stack spacing={2.5}>
      <ReviewScoreFields fields={fields} />
      <Divider />
      <FormTextField<ReviewScoreFormValues>
        name="reviewTitle"
        label="Headline (optional)"
        labelAbove
        placeholder="Your one-line verdict"
        variant="standard"
        multiline
        maxRows={2}
        sx={(theme) => ({
          "& .MuiInput-root": {
            ...theme.typography.h3,
            fontSize: 24,
            lineHeight: 1.35,
            py: 1,
          },
          "& .MuiInput-root:before": {
            borderBottomColor: theme.palette.divider,
          },
          "& .MuiInput-root:after": {
            borderBottomColor: theme.palette.primary.main,
          },
        })}
      />
      <Box>
        <FormTextField<ReviewScoreFormValues>
          name="notes"
          label="Notes (optional)"
          labelAbove
          placeholder="What stuck with you?"
          multiline
          minRows={8}
          sx={{ "& .MuiInputBase-root": { fontSize: 15, lineHeight: 1.7 } }}
        />
        {wordCount > 0 ? (
          <Typography
            variant="numeric"
            component="div"
            sx={{
              mt: 1,
              textAlign: "right",
              color: "text.muted",
              fontSize: 12,
            }}
          >
            {wordCount} {wordCount === 1 ? "word" : "words"}
          </Typography>
        ) : null}
      </Box>
    </Stack>
  );
}

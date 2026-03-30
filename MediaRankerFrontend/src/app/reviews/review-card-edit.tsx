"use client";

import { Button, Stack, Typography } from "@mui/material";
import { FormProvider, UseFormReturn } from "react-hook-form";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { FormStarRating } from "@/lib/components/inputs/rating/form-star-rating";
import { ReviewFormValues } from "./review-card-constants";

type FieldItem = {
  id: number;
  name: string;
};

type ReviewCardEditProps = {
  mediaTitle: string;
  fieldList: FieldItem[];
  methods: UseFormReturn<ReviewFormValues>;
  isSaving: boolean;
  isNew: boolean;
  onSave: (e?: React.BaseSyntheticEvent) => void;
  onCancel: () => void;
};

export function ReviewCardEdit({
  mediaTitle,
  fieldList,
  methods,
  isSaving,
  isNew,
  onSave,
  onCancel,
}: ReviewCardEditProps) {
  return (
    <FormProvider {...methods}>
      <Stack
        component="form"
        direction="column"
        sx={{ height: "100%", p: 1.5, overflow: "auto" }}
        gap={1.5}
        onSubmit={onSave}
      >
        <Typography variant="subtitle2" noWrap>
          {mediaTitle}
        </Typography>
        <FormTextField<ReviewFormValues>
          name="reviewTitle"
          label="Review title"
          size="small"
        />
        <FormTextField<ReviewFormValues>
          name="notes"
          label="Notes"
          size="small"
          multiline
          minRows={2}
        />
        {fieldList.map((field) => (
          <FormStarRating<ReviewFormValues>
            key={field.id}
            name={`fields.${field.id}` as `fields.${string}`}
            label={field.name}
          />
        ))}
        <Stack direction="row" justifyContent="flex-end" sx={{ mt: "auto", pt: 1 }}>
          <Button
            type="submit"
            size="small"
            variant="contained"
            disabled={isSaving}
            loading={isSaving}
          >
            Save
          </Button>
        </Stack>
        {!isNew && (
          <Button size="small" variant="text" onClick={onCancel}>
            Cancel
          </Button>
        )}
      </Stack>
    </FormProvider>
  );
}

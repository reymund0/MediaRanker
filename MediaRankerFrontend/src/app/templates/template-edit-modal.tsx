import { useState } from "react";
import { Box, Button, Divider, Stack, Typography } from "@mui/material";
import { DeleteOutline } from "@mui/icons-material";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { BaseTextField } from "@/lib/components/inputs/text-field/base-text-field";
import { FormDnDList } from "@/lib/components/data-display/form-dnd-list";
import { FormSelect } from "@/lib/components/inputs/select/form-select";
import {
  ALL_MEDIA_TYPES,
  MEDIA_TYPE_LABELS,
  MediaType,
} from "@/lib/contracts/shared";
import { TemplateUpsertRequest } from "./contracts";

const schema = z.object({
  id: z.number().nullable(),
  mediaType: z.nativeEnum(MediaType),
  name: z.string().trim().min(1, "Template name is required"),
  description: z.string().nullable(),
  fields: z
    .array(
      z.object({
        id: z.number().nullable(),
        name: z.string().trim().min(1, "Score name is required"),
        position: z.number(),
      }),
    )
    .min(1, "Add at least one score"),
});
type FormValues = z.infer<typeof schema>;

export function TemplateEditor({
  draft,
  onSubmit,
  onCancel,
  onDelete,
}: {
  draft: TemplateUpsertRequest;
  onSubmit: (data: TemplateUpsertRequest) => Promise<void>;
  onCancel: () => void;
  onDelete?: () => void;
}) {
  const [newScore, setNewScore] = useState("");
  const methods = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { ...draft, mediaType: draft.mediaType as MediaType },
    mode: "onChange",
  });
  const { handleSubmit, getValues, setValue, formState } = methods;
  const addField = () => {
    const name = newScore.trim();
    if (!name) return;
    const fields = getValues("fields");
    setValue(
      "fields",
      [...fields, { id: null, name, position: fields.length }],
      { shouldDirty: true, shouldValidate: true },
    );
    setNewScore("");
  };
  const removeField = (index: number) =>
    setValue(
      "fields",
      getValues("fields").filter((_, i) => index !== i),
      { shouldDirty: true, shouldValidate: true },
    );
  return (
    <FormProvider {...methods}>
      <Box
        component="form"
        onSubmit={handleSubmit((data) =>
          onSubmit({
            ...data,
            fields: data.fields.map((field, position) => ({
              ...field,
              position,
            })),
          }),
        )}
        sx={{
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 1.5,
          bgcolor: "background.paper",
          position: "sticky",
          top: 24,
        }}
      >
        <Box sx={{ p: 3.5 }}>
          <Typography variant="h5">
            {draft.id ? "Edit template" : "New template"}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            Choose what matters to you, then drag your scores into order.
          </Typography>
        </Box>
        <Divider />
        <Stack spacing={3} sx={{ p: 3.5 }}>
          <Stack direction="row" spacing={2}>
            <FormTextField<FormValues> name="name" label="Name" labelAbove />
            <FormTextField<FormValues>
              name="description"
              label="Description (optional)"
              labelAbove
            />
          </Stack>
          <FormSelect<FormValues>
            name="mediaType"
            label="Media type"
            options={ALL_MEDIA_TYPES.map((type) => ({
              id: type,
              label: MEDIA_TYPE_LABELS[type],
            }))}
          />
          <Box>
            <Typography variant="body2" sx={{ mb: 1 }}>
              Scores · drag to reorder
            </Typography>
            <FormDnDList
              name="fields"
              onItemRemove={removeField}
              itemContent={(index) => (
                <FormTextField<FormValues>
                  name={`fields.${index}.name`}
                  inputProps={{ "aria-label": `Score ${index + 1}` }}
                  size="small"
                  sx={{ pr: 4 }}
                />
              )}
            />
            {formState.errors.fields?.message && (
              <Typography color="error" variant="caption">
                {formState.errors.fields.message}
              </Typography>
            )}
          </Box>
          <Stack direction="row" spacing={1}>
            <BaseTextField
              label="Add a score"
              placeholder="e.g. Replayability"
              value={newScore}
              onChange={(event) => setNewScore(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  addField();
                }
              }}
            />
            <Button
              variant="outlined"
              disabled={!newScore.trim()}
              onClick={addField}
            >
              Add
            </Button>
          </Stack>
        </Stack>
        <Divider />
        <Stack direction="row" alignItems="center" spacing={2} sx={{ p: 2.5 }}>
          {onDelete && (
            <Button
              color="error"
              startIcon={<DeleteOutline />}
              onClick={onDelete}
              disabled={formState.isSubmitting}
            >
              Delete template
            </Button>
          )}
          <Box sx={{ flex: 1 }} />
          <Button onClick={onCancel} disabled={formState.isSubmitting}>
            Discard
          </Button>
          <Button
            variant="contained"
            type="submit"
            loading={formState.isSubmitting}
            disabled={draft.id !== null && !formState.isDirty}
          >
            Save template
          </Button>
        </Stack>
      </Box>
    </FormProvider>
  );
}

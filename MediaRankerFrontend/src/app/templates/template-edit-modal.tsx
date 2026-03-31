import AddIcon from "@mui/icons-material/Add";
import { Box, Stack, Typography } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { FormDialog } from "@/lib/components/feedback/dialog/form-dialog";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { FormDnDList } from "@/lib/components/data-display/form-dnd-list";
import { TemplateUpsertRequest } from "./contracts";
import { MediaTypeDto } from "@/lib/contracts/shared";
import { TemplateRow } from "./grid-utils";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { FormSelect } from "@/lib/components/inputs/select/form-select";

const templateEditSchema = z.object({
  id: z.number().optional(),
  mediaTypeId: z.number(),
  name: z.string().trim().min(1, "Template name is required"),
  description: z.string().optional(),
  fields: z
    .array(
      z.object({
        id: z.number().optional(),
        name: z.string().trim().min(1, "Field name is required"),
      }),
    )
    .min(1, "At least one field is required"),
});

type TemplateEditFormValues = z.infer<typeof templateEditSchema>;

type TemplateEditModalProps = {
  open: boolean;
  row: TemplateRow;
  mediaTypes: MediaTypeDto[];
  onSubmit: (data: TemplateUpsertRequest) => void;
  onCancel: () => void;
};

export function TemplateEditModal({
  open,
  row,
  mediaTypes,
  onSubmit,
  onCancel,
}: TemplateEditModalProps) {
  const methods = useForm<TemplateEditFormValues>({
    resolver: zodResolver(templateEditSchema),
    defaultValues: {
      id: row.id,
      mediaTypeId: row.mediaType.id,
      name: row.name,
      description: row.description || undefined,
      fields:
        row.fields.length == 0
          ? [{ id: undefined, name: "" }]
          : row.fields.map((templateField) => ({
              id: templateField.id,
              name: templateField.name,
            })),
    },
    mode: "onChange",
  });

  const { handleSubmit, setValue, getValues } = methods;

  const onSubmitClick = (data: TemplateEditFormValues) => {
    onSubmit({
      id: data.id || null,
      mediaTypeId: data.mediaTypeId,
      name: data.name.trim(),
      description: data.description?.trim() || null,
      fields: data.fields.map((templateField, index) => ({
        id: templateField.id || null,
        name: templateField.name.trim(),
        position: index,
      })),
    });
  };

  const handleAddField = () => {
    const currentFields = getValues("fields");
    const newFields = [...currentFields, { id: undefined, name: "" }];
    setValue("fields", newFields, { shouldValidate: true, shouldDirty: true });
  };

  const handleRemoveField = (index: number) => {
    const currentFields = getValues("fields");
    const newFields = currentFields.filter((_, i) => i !== index);
    setValue("fields", newFields, { shouldValidate: true, shouldDirty: true });
  };

  return (
    <FormDialog<TemplateEditFormValues>
      open={open}
      title="Edit Template"
      onSubmit={handleSubmit(onSubmitClick)}
      onCancel={onCancel}
      methods={methods}
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        <Stack direction="row" spacing={2}>
          <FormTextField<TemplateEditFormValues>
            name="name"
            label="Template name"
          />
          <FormSelect<TemplateEditFormValues>
            name="mediaTypeId"
            label="Media type"
            options={mediaTypes.map((mediaType) => ({
              id: mediaType.id,
              label: mediaType.name,
            }))}
          />
        </Stack>
        <FormTextField<TemplateEditFormValues>
          name="description"
          label="Template description"
          multiline
          minRows={2}
        />

        <Box display="flex" justifyContent="space-between" alignItems="center">
          <Typography variant="subtitle1">
            Template Fields (drag to reorder)
          </Typography>
          <PrimaryButton onClick={handleAddField} startIcon={<AddIcon />}>
            Add Field
          </PrimaryButton>
        </Box>

        <FormDnDList
          name="fields"
          onItemRemove={handleRemoveField}
          itemContent={(index) => (
            <FormTextField<TemplateEditFormValues>
              name={`fields.${index}.name`}
              placeholder={`Field ${index + 1}`}
              size="small"
              sx={{ pr: 4 }}
            />
          )}
        />
      </Stack>
    </FormDialog>
  );
}

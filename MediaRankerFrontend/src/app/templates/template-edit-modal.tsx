import { Stack, Typography } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useEffect } from "react";
import { FormDialog } from "@/lib/components/feedback/dialog/form-dialog";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { FormDnDList } from "@/lib/components/data-display/form-dnd-list";

const templateEditSchema = z.object({
  id: z.string().min(1, "Template id is required"),
  name: z.string().trim().min(1, "Template name is required"),
  description: z.string(),
  templateFields: z
    .array(
      z.object({
        value: z.string().trim().min(1, "Field name is required"),
      }),
    )
    .min(1, "At least one field is required"),
});

type TemplateEditFormValues = z.infer<typeof templateEditSchema>;

export type TemplateEditRowData = {
  id: string;
  name: string;
  description: string;
  templateFields: string[];
};

export type TemplateEditSubmitData = {
  id: string;
  name: string;
  description: string;
  templateFields: string[];
};

type TemplateEditModalProps = {
  open: boolean;
  row: TemplateEditRowData | null;
  onSubmitClick: (data: TemplateEditSubmitData) => void;
  onCancelClick: () => void;
};

export function TemplateEditModal({
  open,
  row,
  onSubmitClick,
  onCancelClick,
}: TemplateEditModalProps) {
  const methods = useForm<TemplateEditFormValues>({
    resolver: zodResolver(templateEditSchema),
    defaultValues: {
      id: "",
      name: "",
      description: "",
      templateFields: [],
    },
    mode: "onChange",
  });

  const { handleSubmit, reset } = methods;

  useEffect(() => {
    if (!row) {
      return;
    }

    reset({
      id: row.id,
      name: row.name,
      description: row.description,
      templateFields: row.templateFields.map((templateField) => ({ value: templateField })),
    });
  }, [row, reset]);

  const onSubmit = (data: TemplateEditFormValues) => {
    onSubmitClick({
      id: data.id,
      name: data.name.trim(),
      description: data.description.trim(),
      templateFields: data.templateFields.map((templateField) => templateField.value.trim()),
    });
  };

  return (
    <FormDialog<TemplateEditFormValues>
      open={open}
      title="Edit Template"
      methods={methods}
      content={
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          <FormTextField<TemplateEditFormValues>
            name="name"
            label="Template name"
          />
          <FormTextField<TemplateEditFormValues>
            name="description"
            label="Template description"
            multiline
            minRows={2}
          />

          <Typography variant="subtitle1">
            Template Fields (drag to reorder)
          </Typography>

          <FormDnDList
            name="templateFields"
            itemContent={(index) => (
              <FormTextField<TemplateEditFormValues>
                name={`templateFields.${index}.value`}
                placeholder={`Field ${index + 1}`}
                size="small"
                sx={{ pr: 4 }}
              />
            )}
          />
        </Stack>
      }
      onCancel={onCancelClick}
      onSubmit={handleSubmit(onSubmit)}
    />
  );
}

import { IconButton, List, ListItem, Stack, Typography } from "@mui/material";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import { useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  closestCenter,
  DndContext,
  DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useEffect } from "react";
import { FormDialog } from "@/lib/components/feedback/dialog/form-dialog";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";

const templateEditSchema = z.object({
  id: z.string().min(1, "Template id is required"),
  name: z.string().trim().min(1, "Template name is required"),
  description: z.string(),
  fields: z
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
  fields: string[];
};

export type TemplateEditSubmitData = {
  id: string;
  name: string;
  description: string;
  fields: string[];
};

type TemplateEditModalProps = {
  open: boolean;
  row: TemplateEditRowData | null;
  onSubmitClick: (data: TemplateEditSubmitData) => void;
  onCancelClick: () => void;
};

type SortableFieldItemProps = {
  id: string;
  index: number;
};

function SortableFieldItem({ id, index }: SortableFieldItemProps) {
  const { attributes, listeners, setNodeRef, transform, transition } =
    useSortable({ id });

  return (
    <ListItem
      ref={setNodeRef}
      secondaryAction={
        <IconButton size="small" {...attributes} {...listeners}>
          <DragIndicatorIcon color="action" fontSize="small" />
        </IconButton>
      }
      sx={{
        borderBottom: "1px solid",
        borderColor: "divider",
        "&:last-child": {
          borderBottom: 0,
        },
        transform: CSS.Transform.toString(transform),
        transition,
      }}
    >
      <FormTextField<TemplateEditFormValues>
        name={`fields.${index}.value`}
        placeholder={`Field ${index + 1}`}
        size="small"
        sx={{ pr: 4 }}
      />
    </ListItem>
  );
}

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
      fields: [],
    },
    mode: "onChange",
  });

  const { control, handleSubmit, reset } = methods;

  const { fields, move } = useFieldArray({
    control,
    name: "fields",
  });

  useEffect(() => {
    if (!row) {
      return;
    }

    reset({
      id: row.id,
      name: row.name,
      description: row.description,
      fields: row.fields.map((field) => ({ value: field })),
    });
  }, [row, reset]);

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

  const fieldIds = fields.map((field) => field.id);

  const onDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;

    if (!over || active.id === over.id) {
      return;
    }

    const oldIndex = fieldIds.indexOf(String(active.id));
    const newIndex = fieldIds.indexOf(String(over.id));

    if (oldIndex < 0 || newIndex < 0) {
      return;
    }

    move(oldIndex, newIndex);
  };

  const onSubmit = (data: TemplateEditFormValues) => {
    onSubmitClick({
      id: data.id,
      name: data.name.trim(),
      description: data.description.trim(),
      fields: data.fields.map((field) => field.value.trim()),
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

          <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragEnd={onDragEnd}
          >
            <SortableContext
              items={fieldIds}
              strategy={verticalListSortingStrategy}
            >
              <List
                dense
                sx={{
                  p: 0,
                  border: "1px solid",
                  borderColor: "divider",
                  borderRadius: 1.5,
                  overflow: "hidden",
                  backgroundColor: "background.paper",
                }}
              >
                {fields.map((field, index) => (
                  <SortableFieldItem
                    key={field.id}
                    id={field.id}
                    index={index}
                  />
                ))}
              </List>
            </SortableContext>
          </DndContext>
        </Stack>
      }
      onCancel={onCancelClick}
      onSubmit={handleSubmit(onSubmit)}
    />
  );
}

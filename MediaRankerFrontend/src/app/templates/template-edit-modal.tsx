import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  List,
  ListItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
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
  arrayMove,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";

type TemplateEditModalProps = {
  open: boolean;
  name: string;
  description: string;
  onNameChange: (value: string) => void;
  onDescriptionChange: (value: string) => void;
  fields: string[];
  onFieldChange: (index: number, value: string) => void;
  onFieldsReorder: (nextFields: string[]) => void;
  onSubmit: () => void;
  onCancel: () => void;
  submitDisabled: boolean;
};

type SortableFieldItemProps = {
  id: string;
  index: number;
  value: string;
  onFieldChange: (index: number, value: string) => void;
};

function SortableFieldItem({ id, index, value, onFieldChange }: SortableFieldItemProps) {
  const { attributes, listeners, setNodeRef, transform, transition } = useSortable({ id });

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
      <TextField
        fullWidth
        size="small"
        value={value}
        onChange={(event) => onFieldChange(index, event.target.value)}
        placeholder={`Field ${index + 1}`}
        sx={{ pr: 4 }}
      />
    </ListItem>
  );
}

export function TemplateEditModal({
  open,
  name,
  description,
  onNameChange,
  onDescriptionChange,
  fields,
  onFieldChange,
  onFieldsReorder,
  onSubmit,
  onCancel,
  submitDisabled,
}: TemplateEditModalProps) {
  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

  const fieldIds = fields.map((_, index) => String(index));

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

    onFieldsReorder(arrayMove(fields, oldIndex, newIndex));
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="md">
      <DialogTitle>Edit Template</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          <TextField
            label="Template name"
            value={name}
            onChange={(event) => onNameChange(event.target.value)}
            fullWidth
          />
          <TextField
            label="Template description"
            value={description}
            onChange={(event) => onDescriptionChange(event.target.value)}
            fullWidth
            multiline
            minRows={2}
          />

          <Typography variant="subtitle1">Template Fields (drag to reorder)</Typography>

          <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
            <SortableContext items={fieldIds} strategy={verticalListSortingStrategy}>
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
                    key={`${index}-${field}`}
                    id={String(index)}
                    index={index}
                    value={field}
                    onFieldChange={onFieldChange}
                  />
                ))}
              </List>
            </SortableContext>
          </DndContext>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button variant="outlined" onClick={onCancel}>
          Cancel
        </Button>
        <Button variant="contained" onClick={onSubmit} disabled={submitDisabled}>
          Submit
        </Button>
      </DialogActions>
    </Dialog>
  );
}

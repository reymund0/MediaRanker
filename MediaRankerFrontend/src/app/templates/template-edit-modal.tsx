import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  ListItemText,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import { useState } from "react";

function reorderItems(items: string[], fromIndex: number, toIndex: number) {
  const next = [...items];
  const [moved] = next.splice(fromIndex, 1);

  if (moved === undefined) {
    return items;
  }

  next.splice(toIndex, 0, moved);
  return next;
}

type TemplateEditModalProps = {
  open: boolean;
  name: string;
  description: string;
  onNameChange: (value: string) => void;
  onDescriptionChange: (value: string) => void;
  fields: string[];
  onFieldsReorder: (nextFields: string[]) => void;
  onSubmit: () => void;
  onCancel: () => void;
  submitDisabled: boolean;
};

export function TemplateEditModal({
  open,
  name,
  description,
  onNameChange,
  onDescriptionChange,
  fields,
  onFieldsReorder,
  onSubmit,
  onCancel,
  submitDisabled,
}: TemplateEditModalProps) {
  const [dragIndex, setDragIndex] = useState<number | null>(null);

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
              <ListItem
                key={`${field}-${index}`}
                draggable
                onDragStart={() => setDragIndex(index)}
                onDragOver={(event) => event.preventDefault()}
                onDrop={() => {
                  if (dragIndex === null || dragIndex === index) {
                    return;
                  }

                  onFieldsReorder(reorderItems(fields, dragIndex, index));
                  setDragIndex(null);
                }}
                onDragEnd={() => setDragIndex(null)}
                secondaryAction={<DragIndicatorIcon color="action" fontSize="small" />}
                sx={{
                  cursor: "grab",
                  borderBottom: "1px solid",
                  borderColor: "divider",
                  "&:last-child": {
                    borderBottom: 0,
                  },
                }}
              >
                <ListItemText
                  primaryTypographyProps={{ color: "text.primary" }}
                  primary={`${index + 1}. ${field}`}
                />
              </ListItem>
            ))}
          </List>
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

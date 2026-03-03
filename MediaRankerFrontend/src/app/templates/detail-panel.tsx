import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import { Button, List, ListItem, ListItemText, Stack, Typography } from "@mui/material";

type TemplateDetailPanelProps = {
  fields: string[];
  dragIndex: number | null;
  onDragStart: (index: number) => void;
  onDragEnd: () => void;
  onDropAtIndex: (index: number) => void;
  onSubmit: () => void;
  onCancel: () => void;
  submitDisabled: boolean;
};

export function TemplateDetailPanel({
  fields,
  dragIndex,
  onDragStart,
  onDragEnd,
  onDropAtIndex,
  onSubmit,
  onCancel,
  submitDisabled,
}: TemplateDetailPanelProps) {
  return (
    <>
      <Typography variant="subtitle1" sx={{ mb: 1 }}>
        Template Fields (drag to reorder)
      </Typography>

      <List
        dense
        sx={{
          p: 0,
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 1.5,
          overflow: "hidden",
          mb: 2,
          backgroundColor: "background.paper",
        }}
      >
        {fields.map((field, index) => (
          <ListItem
            key={`${field}-${index}`}
            draggable
            onDragStart={() => onDragStart(index)}
            onDragOver={(event) => event.preventDefault()}
            onDrop={() => {
              if (dragIndex === null || dragIndex === index) {
                return;
              }

              onDropAtIndex(index);
            }}
            onDragEnd={onDragEnd}
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

      <Stack direction="row" spacing={1} justifyContent="flex-start">
        <Button variant="contained" onClick={onSubmit} disabled={submitDisabled}>
          Submit
        </Button>
        <Button variant="outlined" onClick={onCancel}>
          Cancel
        </Button>
      </Stack>
    </>
  );
}

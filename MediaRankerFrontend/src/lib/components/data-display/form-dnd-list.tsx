import { closestCenter, DndContext, DragEndEvent, KeyboardSensor, PointerSensor, useSensor, useSensors } from "@dnd-kit/core";
import { SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities"
import { IconButton, List, ListItem } from "@mui/material";
import { useEffect, useState } from "react";
import { useFieldArray, useFormContext } from "react-hook-form";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";



export interface FormDnDListProps {
  name: string,
  onItemRemove?: (index: number) => void,
  itemContent: (index: number) => React.ReactNode
}

export function FormDnDList(props: FormDnDListProps) {
  const { control } = useFormContext();

  const { fields, move } = useFieldArray({
    control,
    name: props.name,
    keyName: "dndId"
  });

  const dndFieldIds = fields.map((field) => field.dndId);

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

  const onDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;

    if (!over || active.id === over.id) {
      return;
    }

    const oldIndex = dndFieldIds.indexOf(String(active.id));
    const newIndex = dndFieldIds.indexOf(String(over.id));

    if (oldIndex < 0 || newIndex < 0) {
      return;
    }

    move(oldIndex, newIndex);
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragEnd={onDragEnd}
    >
      <SortableContext
        items={dndFieldIds}
        strategy={verticalListSortingStrategy}>
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
        >{
            fields.map((field, index) => (
              <SortableItem
                key={field.dndId}
                id={field.dndId}
                index={index}
                itemContent={props.itemContent}
                onRemove={props.onItemRemove}
              />
            ))}
        </List>

      </SortableContext>

    </DndContext>
  )
}

interface SortableItemProps {
  id: string,
  index: number,
  itemContent: (index: number) => React.ReactNode
  onRemove?: (index: number) => void
}

function SortableItem(props: SortableItemProps) {
  const { attributes, listeners, setNodeRef, transform, transition } =
    useSortable({ id: props.id });

  return (
    <ListItem
      ref={setNodeRef}
      secondaryAction={props.onRemove ? (
        <IconButton
          size="small"
          color="error"
          onClick={() => props.onRemove?.(props.index)}
        >
          <DeleteOutlineIcon fontSize="small" />
        </IconButton>
      ) : null}
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
      <IconButton size="small" {...attributes} {...listeners}>
        <DragIndicatorIcon color="action" fontSize="small" />
      </IconButton>
      {props.itemContent(props.index)}
    </ListItem>
  )
}
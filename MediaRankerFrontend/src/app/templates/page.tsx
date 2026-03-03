"use client";

import AddIcon from "@mui/icons-material/Add";
import {
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Typography,
} from "@mui/material";
import {
  GridColDef,
  GridRowId,
  GridRowClassNameParams,
} from "@mui/x-data-grid";
import { useMemo, useState } from "react";
import { BaseDataGrid } from "@/lib/components/data-grid/base-data-grid";
import { TemplateDetailPanel } from "./detail-panel";
import { buildTemplateColumns, TemplateDraft, TemplateRow } from "./grid-utils";

type EditDraft = TemplateDraft & {
  fields: string[];
};

const INITIAL_ROWS: TemplateRow[] = [
  {
    id: "system-1",
    name: "Default Movie Review",
    description: "Base review template seeded by the system.",
    updatedAt: "2026-03-01 09:10",
    isSystem: true,
    fields: ["Title", "Rating", "Summary", "Would Recommend"],
  },
  {
    id: "user-1",
    name: "Anime Weekly",
    description: "Template focused on episode pacing and art quality.",
    updatedAt: "2026-03-02 13:45",
    isSystem: false,
    fields: ["Series", "Episode", "Animation", "Pacing", "Overall Score"],
  },
];

function reorderItems(items: string[], fromIndex: number, toIndex: number) {
  const next = [...items];
  const [moved] = next.splice(fromIndex, 1);
  next.splice(toIndex, 0, moved);
  return next;
}

export default function TemplatesPage() {
  const [rows, setRows] = useState<TemplateRow[]>(INITIAL_ROWS);
  const [editingRowId, setEditingRowId] = useState<string | null>(null);
  const [expandedRowIds, setExpandedRowIds] = useState<GridRowId[]>([]);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [draft, setDraft] = useState<EditDraft>({
    name: "",
    description: "",
    fields: [],
  });

  const editingRow = useMemo(
    () => rows.find((row) => row.id === editingRowId) ?? null,
    [rows, editingRowId],
  );

  const startEditing = (row: TemplateRow) => {
    if (row.isSystem) {
      return;
    }

    setEditingRowId(row.id);
    setExpandedRowIds((prev) => (prev.includes(row.id) ? prev : [...prev, row.id]));
    setDraft({
      name: row.name,
      description: row.description,
      fields: [...row.fields],
    });
  };

  const cancelEditing = () => {
    if (!editingRow) {
      return;
    }

    if (editingRow.isTemporary) {
      setRows((prev) => prev.filter((row) => row.id !== editingRow.id));
    }

    setExpandedRowIds((prev) => prev.filter((rowId) => rowId !== editingRow.id));
    setEditingRowId(null);
    setDragIndex(null);
    setDraft({ name: "", description: "", fields: [] });
  };

  const submitEditing = () => {
    if (!editingRow) {
      return;
    }

    const now = new Date();
    const formattedUpdatedAt = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(
      now.getDate(),
    ).padStart(2, "0")} ${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;

    setRows((prev) =>
      prev.map((row) =>
        row.id === editingRow.id
          ? {
              ...row,
              name: draft.name.trim(),
              description: draft.description.trim(),
              fields: [...draft.fields],
              updatedAt: formattedUpdatedAt,
              isTemporary: false,
            }
          : row,
      ),
    );

    setExpandedRowIds((prev) => prev.filter((rowId) => rowId !== editingRow.id));
    setEditingRowId(null);
    setDragIndex(null);
    setDraft({ name: "", description: "", fields: [] });
  };

  const addTemplate = () => {
    const id = `temp-${Date.now()}`;
    const newRow: TemplateRow = {
      id,
      name: "",
      description: "",
      updatedAt: "-",
      isSystem: false,
      isTemporary: true,
      fields: ["Field 1", "Field 2", "Field 3"],
    };

    setRows((prev) => [newRow, ...prev]);
    setEditingRowId(id);
    setExpandedRowIds((prev) => (prev.includes(id) ? prev : [...prev, id]));
    setDragIndex(null);
    setDraft({
      name: "",
      description: "",
      fields: [...newRow.fields],
    });
  };

  const removeRow = (row: TemplateRow) => {
    setRows((prev) => prev.filter((candidate) => candidate.id !== row.id));
    setExpandedRowIds((prev) => prev.filter((rowId) => rowId !== row.id));

    if (editingRowId === row.id) {
      setEditingRowId(null);
      setDragIndex(null);
      setDraft({ name: "", description: "", fields: [] });
    }
  };

  const columns: GridColDef<TemplateRow>[] = buildTemplateColumns({
    editingRowId,
    draft: { name: draft.name, description: draft.description },
    onDraftChange: (updates) =>
      setDraft((prev) => ({
        ...prev,
        ...updates,
      })),
    startEditing,
    cancelEditing,
    removeRow,
  });

  return (
    <Box sx={{ flex: 1, px: { xs: 2, md: 3 }, py: { xs: 2, md: 3 } }}>
      <Card>
        <CardContent sx={{ p: { xs: 2, md: 3 } }}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            alignItems={{ xs: "stretch", sm: "center" }}
            justifyContent="space-between"
            spacing={2}
            sx={{ mb: 2 }}
          >
            <Box>
              <Typography variant="h4" component="h1">
                Templates
              </Typography>
              <Typography color="text.secondary">
                Manage your custom templates and reorder template fields.
              </Typography>
            </Box>

            <Button variant="contained" startIcon={<AddIcon />} onClick={addTemplate}>
              Add Template +
            </Button>
          </Stack>

          <Box
            sx={{
              border: "1px solid",
              borderColor: "divider",
              borderRadius: 2,
              overflow: "hidden",
            }}
          >
            <BaseDataGrid
              autoHeight
              disableRowSelectionOnClick
              hideFooter
              rowHeight={64}
              rows={rows}
              columns={columns}
              expandedRowIds={expandedRowIds}
              onExpandedRowIdsChange={setExpandedRowIds}
              getDetailPanelContent={(row) =>
                row.id === editingRowId ? (
                  <TemplateDetailPanel
                    fields={draft.fields}
                    dragIndex={dragIndex}
                    onDragStart={setDragIndex}
                    onDragEnd={() => setDragIndex(null)}
                    onDropAtIndex={(index) => {
                      if (dragIndex === null) {
                        return;
                      }

                      setDraft((prev) => ({
                        ...prev,
                        fields: reorderItems(prev.fields, dragIndex, index),
                      }));
                      setDragIndex(null);
                    }}
                    onSubmit={submitEditing}
                    onCancel={cancelEditing}
                    submitDisabled={draft.name.trim().length === 0}
                  />
                ) : null
              }
              getRowClassName={(params: GridRowClassNameParams<TemplateRow>) =>
                params.row.id === editingRowId ? "template-row--editing" : ""
              }
              sx={{
                border: 0,
                "& .MuiDataGrid-columnHeaders": {
                  backgroundColor: "background.default",
                  borderBottom: "1px solid",
                  borderColor: "divider",
                },
                "& .MuiDataGrid-cell": {
                  borderBottomColor: "divider",
                  alignItems: "center",
                },
                "& .template-row--editing": {
                  backgroundColor: "action.hover",
                },
              }}
            />
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}

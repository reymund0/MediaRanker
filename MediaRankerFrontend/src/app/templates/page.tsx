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
} from "@mui/x-data-grid";
import { useMemo, useState } from "react";
import { BaseDataGrid } from "@/lib/components/data-grid/base-data-grid";
import { TemplateEditModal } from "./template-edit-modal";
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

export default function TemplatesPage() {
  const [rows, setRows] = useState<TemplateRow[]>(INITIAL_ROWS);
  const [editingRowId, setEditingRowId] = useState<string | null>(null);
  const [draft, setDraft] = useState<EditDraft>({
    name: "",
    description: "",
    fields: [],
  });

  const editingRow = useMemo(
    () => rows.find((row) => row.id === editingRowId) ?? null,
    [rows, editingRowId],
  );

  const onEditClick = (row: TemplateRow) => {
    if (row.isSystem) {
      return;
    }

    setEditingRowId(row.id);
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

    setEditingRowId(null);
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

    setEditingRowId(null);
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
    setDraft({
      name: "",
      description: "",
      fields: [...newRow.fields],
    });
  };

  const onDeleteClick = (row: TemplateRow) => {
    setRows((prev) => prev.filter((candidate) => candidate.id !== row.id));

    if (editingRowId === row.id) {
      setEditingRowId(null);
      setDraft({ name: "", description: "", fields: [] });
    }
  };

  const columns: GridColDef<TemplateRow>[] = buildTemplateColumns({
    onEditClick,
    onDeleteClick,
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
            />
          </Box>

          <TemplateEditModal
            open={Boolean(editingRowId)}
            name={draft.name}
            description={draft.description}
            onNameChange={(value) =>
              setDraft((prev) => ({
                ...prev,
                name: value,
              }))
            }
            onDescriptionChange={(value) =>
              setDraft((prev) => ({
                ...prev,
                description: value,
              }))
            }
            fields={draft.fields}
            onFieldsReorder={(nextFields) =>
              setDraft((prev) => ({
                ...prev,
                fields: nextFields,
              }))
            }
            onSubmit={submitEditing}
            onCancel={cancelEditing}
            submitDisabled={draft.name.trim().length === 0}
          />
        </CardContent>
      </Card>
    </Box>
  );
}

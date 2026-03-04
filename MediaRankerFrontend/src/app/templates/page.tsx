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
import { GridColDef } from "@mui/x-data-grid";
import { useMemo, useState } from "react";
import { BaseDataGrid } from "@/lib/components/data-grid/base-data-grid";
import {
  TemplateEditModal,
  TemplateEditSubmitData,
} from "./template-edit-modal";
import { buildTemplateColumns, TemplateRow } from "./grid-utils";

const INITIAL_ROWS: TemplateRow[] = [
  {
    id: "system-1",
    name: "Default Movie Review",
    description: "Base review template seeded by the system.",
    updatedAt: "2026-03-01 09:10",
    isSystem: true,
    templateFields: ["Title", "Rating", "Summary", "Would Recommend"],
  },
  {
    id: "user-1",
    name: "Anime Weekly",
    description: "Template focused on episode pacing and art quality.",
    updatedAt: "2026-03-02 13:45",
    isSystem: false,
    templateFields: ["Series", "Episode", "Animation", "Pacing", "Overall Score"],
  },
];

export default function TemplatesPage() {
  const [rows, setRows] = useState<TemplateRow[]>(INITIAL_ROWS);
  const [editingRowId, setEditingRowId] = useState<string | null>(null);

  const editingRow = useMemo(
    () => rows.find((row) => row.id === editingRowId) ?? null,
    [rows, editingRowId],
  );

  const onEditClick = (row: TemplateRow) => {
    if (row.isSystem) {
      return;
    }

    setEditingRowId(row.id);
  };

  const cancelEditing = () => {
    if (!editingRow) {
      return;
    }

    if (editingRow.isTemporary) {
      setRows((prev) => prev.filter((row) => row.id !== editingRow.id));
    }

    setEditingRowId(null);
  };

  const submitEditing = (data: TemplateEditSubmitData) => {
    const now = new Date();
    const formattedUpdatedAt = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(
      now.getDate(),
    ).padStart(
      2,
      "0",
    )} ${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;

    setRows((prev) =>
      prev.map((row) =>
        row.id === data.id
          ? {
              ...row,
              name: data.name,
              description: data.description,
              templateFields: [...data.templateFields],
              updatedAt: formattedUpdatedAt,
              isTemporary: false,
            }
          : row,
      ),
    );

    // this is where refresh grid goes

    setEditingRowId(null);
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
      templateFields: ["Field 1", "Field 2", "Field 3"],
    };

    setRows((prev) => [newRow, ...prev]);
    setEditingRowId(id);
  };

  const onDeleteClick = (row: TemplateRow) => {
    setRows((prev) => prev.filter((candidate) => candidate.id !== row.id));

    if (editingRowId === row.id) {
      setEditingRowId(null);
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

            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={addTemplate}
            >
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
            row={
              editingRow
                ? {
                    id: editingRow.id,
                    name: editingRow.name,
                    description: editingRow.description,
                    templateFields: editingRow.templateFields,
                  }
                : null
            }
            onSubmitClick={submitEditing}
            onCancelClick={cancelEditing}
          />
        </CardContent>
      </Card>
    </Box>
  );
}

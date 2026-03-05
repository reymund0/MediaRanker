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
import { useEffect, useMemo, useState } from "react";
import { BaseDataGrid } from "@/lib/components/data-grid/base-data-grid";
import {
  TemplateEditModal,
} from "./template-edit-modal";
import { buildTemplateColumns, TemplateRow } from "./grid-utils";
import { TemplateDto, TemplateUpsertRequest } from "./contracts";
import { useQuery } from "@/lib/api/use-query";
import { useUser } from "@/lib/auth/user-provider";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";

export default function TemplatesPage() {
  const { showSuccess, showError } = useAlert();
  const { userId } = useUser();

  const [rows, setRows] = useState<TemplateRow[]>([]);
  const [editingRowId, setEditingRowId] = useState<number | undefined>(undefined);
  const editingRow = useMemo(
    () => rows.find((row) => row.id === editingRowId) ?? undefined,
    [rows, editingRowId],
  );

  console.log("editingRow", editingRow);

  const { data: templates, isLoading, isError } = useQuery<TemplateDto[]>({
    route: "/api/templates",
    queryKey: ["templates"],
    enabled: !!userId,
  });

  useEffect(() => {
    if (templates) {
      setRows(templates);
    }
  }, [templates]);

  const { mutate: upsertTemplate } = useMutation<TemplateUpsertRequest, TemplateDto>({
    route: "/api/templates",
    method: "POST",
  });


  const onEditClick = (row: TemplateRow) => {
    setEditingRowId(row.id);
  };

  const cancelEditing = () => {
    if (!editingRow) {
      return;
    }

    // Clear temporary new row.
    if (editingRow.id === 0) {
      setRows((prev) => prev.filter((row) => row.id !== editingRow.id));
    }

    setEditingRowId(undefined);
  };

  const submitEditing = (data: TemplateUpsertRequest) => {
    upsertTemplate(data, {
      onSuccess: (response) => {
        showSuccess("Template saved successfully");
        // Update the in-edit row with the response.
        setRows((prev) =>
          prev.map((row) =>
            row.id === editingRowId ? response : row
          )
        );
      },
      onError: (error) => {
        showError(error.message);
      },
    });

    setEditingRowId(undefined);
  };

  const addTemplate = () => {
    const newRow: TemplateRow = {
      id: 0,
      isSystem: false,
      userId: userId!,
      name: "",
      description: null,
      createdAt: "-",
      updatedAt: "-",
      fields: [],
    };

    setRows((prev) => [newRow, ...prev]);
    setEditingRowId(newRow.id);
  };

  const onDeleteClick = (row: TemplateRow) => {
    setRows((prev) => prev.filter((candidate) => candidate.id !== row.id));

    if (editingRowId === row.id) {
      setEditingRowId(undefined);
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

            <PrimaryButton
              startIcon={<AddIcon />}
              onClick={addTemplate}
            >
              Add Template
            </PrimaryButton>
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
              disableRowSelectionOnClick
              hideFooter
              loading={isLoading}
              error={isError}
              rowHeight={64}
              rows={rows}
              columns={columns}
            />
          </Box>
          {editingRow && (
            <TemplateEditModal
              open={true}
              row={editingRow}
              onSubmit={submitEditing}
              onCancel={cancelEditing}
            />
          )}
        </CardContent>
      </Card>
    </Box>
  );
}

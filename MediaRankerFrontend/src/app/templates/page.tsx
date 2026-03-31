"use client";

import AddIcon from "@mui/icons-material/Add";
import { Box, Stack, Typography } from "@mui/material";
import { GridColDef } from "@mui/x-data-grid";
import { useEffect, useMemo, useState } from "react";
import { BaseDataGrid } from "@/lib/components/data-grid/base-data-grid";
import { TemplateEditModal } from "./template-edit-modal";
import {
  buildTemplateColumns,
  mapTemplateToRow,
  TemplateRow,
} from "./grid-utils";
import { TemplateDto, TemplateUpsertRequest } from "./contracts";
import { MediaTypeDto } from "@/lib/contracts/shared";
import { useQuery } from "@/lib/api/use-query";
import { useUser } from "@/lib/auth/user-provider";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { PageCard } from "@/lib/components/layout/page-card";

export default function TemplatesPage() {
  const { showSuccess, showError } = useAlert();
  const { userId } = useUser();

  const {
    data: mediaTypes,
    isError: isMediaTypesError,
    isLoading: isMediaTypesLoading,
  } = useQuery<MediaTypeDto[]>({
    route: "/api/mediaTypes",
    queryKey: ["mediaTypes"],
    enabled: !!userId,
  });

  const [rows, setRows] = useState<TemplateRow[]>([]);
  const [deleteRowId, setDeleteRowId] = useState<number | undefined>(undefined);
  const [editingRowId, setEditingRowId] = useState<number | undefined>(
    undefined,
  );
  const editingRow = useMemo(
    () => rows.find((row) => row.id === editingRowId) ?? undefined,
    [rows, editingRowId],
  );

  const {
    data: templates,
    isLoading: isTemplatesLoading,
    isError: isTemplatesError,
  } = useQuery<TemplateDto[]>({
    route: "/api/templates",
    queryKey: ["templates"],
    enabled: !!userId && !editingRow,
  });

  useEffect(() => {
    const updateRows = async () => {
      if (templates) {
        setRows(templates.map(mapTemplateToRow));
      }
    };
    updateRows();
  }, [templates]);

  const { mutate: upsertTemplate } = useMutation<
    TemplateUpsertRequest,
    TemplateDto
  >({
    route: "/api/templates",
    method: "POST",
  });

  const { mutate: deleteTemplate } = useMutation<number, void>({
    route: (id) => `/api/templates/${id}`,
    method: "DELETE",
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
            row.id !== editingRowId ? row : mapTemplateToRow(response),
          ),
        );

        setEditingRowId(undefined);
      },
      onError: (error) => {
        showError(error.message);
      },
    });
  };

  const addTemplate = () => {
    const newRow: TemplateRow = {
      id: 0,
      mediaType: mediaTypes?.[0] ?? { id: 0, name: "" },
      isSystem: false,
      userId: userId!,
      name: "",
      description: null,
      createdAt: new Date(),
      updatedAt: new Date(),
      fields: [],
    };

    setRows((prev) => [newRow, ...prev]);
    setEditingRowId(newRow.id);
  };

  const onDeleteClick = (row: TemplateRow) => {
    setDeleteRowId(row.id);
  };

  const onDeleteConfirm = (rowId: number) => {
    // If somehow the delete icon was pressed on a new row that hasn't been saved yet.
    if (rowId === 0) {
      setRows((prev) => prev.filter((candidate) => candidate.id !== rowId));
      return;
    }

    deleteTemplate(rowId, {
      onSuccess: () => {
        showSuccess("Template deleted successfully");
        setRows((prev) => prev.filter((candidate) => candidate.id !== rowId));
        setDeleteRowId(undefined);
      },
      onError: (error) => {
        showError(error.message);
      },
    });
  };

  const columns: GridColDef<TemplateRow>[] = buildTemplateColumns({
    onEditClick,
    onDeleteClick,
  });

  return (
    <PageCard>
      <Stack
        direction="row"
        alignItems="center"
        justifyContent="space-between"
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

        <PrimaryButton startIcon={<AddIcon />} onClick={addTemplate}>
          Add Template
        </PrimaryButton>
      </Stack>

      <Box
        sx={{
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 2,
        }}
      >
        <BaseDataGrid
          loading={isTemplatesLoading || isMediaTypesLoading}
          error={isTemplatesError || isMediaTypesError}
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
          mediaTypes={mediaTypes || []}
        />
      )}
      {deleteRowId && (
        <BaseDialog
          open={true}
          onConfirm={() => onDeleteConfirm(deleteRowId)}
          onClose={() => setDeleteRowId(undefined)}
          title="Delete Template"
          confirmLabel="Delete"
          confirmLoading={false}
        >
          {"Are you sure you want to delete " +
            rows.find((r) => r.id === deleteRowId)?.name +
            " template?"}
        </BaseDialog>
      )}
    </PageCard>
  );
}

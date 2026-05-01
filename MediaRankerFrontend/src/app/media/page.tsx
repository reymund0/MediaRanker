"use client";

import AddIcon from "@mui/icons-material/Add";
import { Box, Stack, Typography } from "@mui/material";
import { GridColDef } from "@mui/x-data-grid";
import { useEffect, useMemo, useState } from "react";
import { useMutation } from "@/lib/api/use-mutation";
import { useQuery } from "@/lib/api/use-query";
import { useUser } from "@/lib/auth/user-provider";
import { BaseDataGrid } from "@/lib/components/data-grid/base-data-grid";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { MediaDto, MediaUpsertRequest } from "./contracts";
import { MediaTypeDto, PageResult } from "@/lib/contracts/shared";
import { buildMediaColumns, MediaRow, mapMediaToRow } from "./grid-utils";
import { MediaEditModal } from "./media-edit-modal";
import { PageCard } from "@/lib/components/layout/page-card";

export default function MediaPage() {
  const { showSuccess, showError } = useAlert();
  const { userId } = useUser();

  const [rows, setRows] = useState<MediaRow[]>([]);
  const [deleteRowId, setDeleteRowId] = useState<number | undefined>(undefined);
  const [editingRowId, setEditingRowId] = useState<number | undefined>(
    undefined,
  );

  const editingRow = useMemo(
    () => rows.find((row) => row.id === editingRowId) ?? undefined,
    [rows, editingRowId],
  );

  const {
    data: media,
    isLoading: isMediaLoading,
    isError: isMediaError,
  } = useQuery<PageResult<MediaDto>>({
    route: "/api/media",
    queryKey: ["media"],
    enabled: !!userId && !editingRow,
  });

  const {
    data: mediaTypes,
    isLoading: isMediaTypesLoading,
    isError: isMediaTypesError,
  } = useQuery<MediaTypeDto[]>({
    route: "/api/mediaTypes",
    queryKey: ["mediaTypes"],
    enabled: !!userId,
  });

  useEffect(() => {
    const updateRows = async () => {
      if (!media) {
        return;
      }

      setRows(media.items.map((mediaRecord) => mapMediaToRow(mediaRecord)));
    };

    updateRows();
  }, [media]);

  const { mutate: upsertMedia } = useMutation<MediaUpsertRequest, MediaDto>({
    route: "/api/media",
    method: "POST",
  });

  const { mutate: deleteMedia } = useMutation<number, void>({
    route: (id) => `/api/media/${id}`,
    method: "DELETE",
  });

  const onEditClick = (row: MediaRow) => {
    setEditingRowId(row.id);
  };

  const cancelEditing = () => {
    if (!editingRow) {
      return;
    }

    if (editingRow.id === 0) {
      setRows((prev) => prev.filter((row) => row.id !== editingRow.id));
    }

    setEditingRowId(undefined);
  };

  const submitEditing = (data: MediaUpsertRequest) => {
    upsertMedia(data, {
      onSuccess: (response) => {
        showSuccess("Media saved successfully");
        setRows((prev) =>
          prev.map((row) =>
            row.id !== editingRowId ? row : mapMediaToRow(response),
          ),
        );
        setEditingRowId(undefined);
      },
      onError: (error) => {
        showError(error.message);
      },
    });
  };

  const addMedia = () => {
    const defaultMediaType = mediaTypes?.[0] ?? { id: 0, name: "" };

    const newRow: MediaRow = {
      id: 0,
      title: "",
      mediaTypeId: defaultMediaType.id,
      mediaTypeName: defaultMediaType.name,
      releaseDate: null,
      createdAt: null,
      updatedAt: null,
    };

    setRows((prev) => [newRow, ...prev]);
    setEditingRowId(newRow.id);
  };

  const onDeleteClick = (row: MediaRow) => {
    setDeleteRowId(row.id);
  };

  const onDeleteConfirm = (rowId: number) => {
    if (rowId === 0) {
      setRows((prev) => prev.filter((candidate) => candidate.id !== rowId));
      setDeleteRowId(undefined);
      return;
    }

    deleteMedia(rowId, {
      onSuccess: () => {
        showSuccess("Media deleted successfully");
        setRows((prev) => prev.filter((candidate) => candidate.id !== rowId));
        setDeleteRowId(undefined);
      },
      onError: (error) => {
        showError(error.message);
      },
    });
  };

  const columns: GridColDef<MediaRow>[] = buildMediaColumns({
    onEditClick,
    onDeleteClick,
  });

  return (
    <PageCard sx={{ maxWidth: "1100px" }}>
      <Stack
        direction="row"
        alignItems="center"
        justifyContent="space-between"
        sx={{ mb: 2 }}
      >
        <Box>
          <Typography variant="h4" component="h1">
            Media
          </Typography>
          <Typography color="text.secondary">
            Manage your media catalog.
          </Typography>
        </Box>

        <PrimaryButton startIcon={<AddIcon />} onClick={addMedia}>
          Add Media
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
          loading={isMediaLoading || isMediaTypesLoading}
          error={isMediaError || isMediaTypesError}
          rows={rows}
          columns={columns}
        />
      </Box>

      {editingRow ? (
        <MediaEditModal
          open={true}
          row={editingRow}
          mediaTypes={mediaTypes || []}
          onSubmit={submitEditing}
          onCancel={cancelEditing}
        />
      ) : null}

      {deleteRowId !== undefined ? (
        <BaseDialog
          open={true}
          onConfirm={() => onDeleteConfirm(deleteRowId)}
          onClose={() => setDeleteRowId(undefined)}
          title="Delete Media"
          confirmLabel="Delete"
          confirmLoading={false}
        >
          {"Are you sure you want to delete " +
            rows.find((r) => r.id === deleteRowId)?.title +
            "?"}
        </BaseDialog>
      ) : null}
    </PageCard>
  );
}

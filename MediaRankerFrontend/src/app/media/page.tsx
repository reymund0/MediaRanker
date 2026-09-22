"use client";

import AddIcon from "@mui/icons-material/Add";
import { Box, NoSsr, Stack, Typography } from "@mui/material";
import { GridColDef } from "@mui/x-data-grid";
import { useState } from "react";
import { usePaginatedDatagrid } from "@/lib/components/data-grid/use-paginated-datagrid";
import { useQueryClient } from "@tanstack/react-query";
import { useMutation } from "@/lib/api/use-mutation";
import { usePagedQuery } from "@/lib/api/use-paged-query";
import { useQuery } from "@/lib/api/use-query";
import { useUser } from "@/lib/auth/user-provider";
import { BaseDataGrid } from "@/lib/components/data-grid/base-data-grid";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { BaseSelect } from "@/lib/components/inputs/select/base-select";
import { MediaDto, MediaUpsertRequest } from "./contracts";
import { MediaTypeDto } from "@/lib/contracts/shared";
import { buildMediaColumns, MediaRow, mapMediaToRow } from "./grid-utils";
import { MediaEditModal } from "./media-edit-modal";
import { PageCard } from "@/lib/components/layout/page-card";
import { usePendingCoverRefresh } from "@/lib/api/use-pending-cover-refresh";

export default function MediaPage() {
  const { showSuccess, showError } = useAlert();
  const { userId } = useUser();
  const queryClient = useQueryClient();

  const [selectedMediaTypeId, setSelectedMediaTypeId] = useState<
    number | undefined
  >(undefined);
  const [draftRow, setDraftRow] = useState<MediaRow | undefined>(undefined);
  const [deleteRowId, setDeleteRowId] = useState<number | undefined>(undefined);

  const { dataGridProps, pageRequest } = usePaginatedDatagrid({
    defaultPageSize: 25,
    pageSizeOptions: [10, 25, 50, 100],
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

  const activeMediaTypeId = selectedMediaTypeId ?? mediaTypes?.[0]?.id;

  const {
    items,
    totalCount,
    isLoading: isMediaLoading,
    error: mediaError,
    refetch: refetchMedia,
  } = usePagedQuery<MediaDto>({
    route: "/api/media",
    routeParams: { mediaTypeId: activeMediaTypeId },
    queryKey: ["media", activeMediaTypeId],
    enabled: !!userId && activeMediaTypeId != null,
    pageSize: dataGridProps.paginationModel.pageSize,
    pageRequest,
  });

  const rows = items.map(mapMediaToRow);

  usePendingCoverRefresh({
    viewKey: `${activeMediaTypeId}:${items.map((media) => media.id).join(",")}`,
    hasPendingCovers: items.some((media) => media.coverStatus === "pending"),
    refetch: refetchMedia,
    enabled: !!userId && activeMediaTypeId != null,
  });

  const { mutate: upsertMedia } = useMutation<MediaUpsertRequest, MediaDto>({
    route: "/api/media",
    method: "POST",
  });

  const { mutate: deleteMedia } = useMutation<number, void>({
    route: (id) => `/api/media/${id}`,
    method: "DELETE",
  });

  const onEditClick = (row: MediaRow) => {
    setDraftRow({ ...row });
  };

  const cancelEditing = () => {
    setDraftRow(undefined);
  };

  const submitEditing = (data: MediaUpsertRequest) => {
    upsertMedia(data, {
      onSuccess: () => {
        showSuccess("Media saved successfully");
        queryClient.invalidateQueries({ queryKey: ["media"] });
        setDraftRow(undefined);
      },
      onError: (error) => {
        showError(error.message);
      },
    });
  };

  const addMedia = () => {
    const activeTypeId = activeMediaTypeId ?? 0;
    const activeTypeName =
      mediaTypes?.find((mt) => mt.id === activeTypeId)?.name ?? "";

    setDraftRow({
      id: undefined,
      title: "",
      mediaTypeId: activeTypeId,
      mediaTypeName: activeTypeName,
      releaseDate: null,
      createdAt: null,
      updatedAt: null,
      coverStatus: "unsupported",
    });
  };

  const onDeleteClick = (row: MediaRow) => {
    setDeleteRowId(row.id);
  };

  const onDeleteConfirm = (rowId: number) => {
    deleteMedia(rowId, {
      onSuccess: () => {
        showSuccess("Media deleted successfully");
        queryClient.invalidateQueries({ queryKey: ["media"] });
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
        direction={{ xs: "column", sm: "row" }}
        alignItems={{ xs: "stretch", sm: "center" }}
        justifyContent="space-between"
        gap={2}
        sx={{ mb: 2 }}
      >
        <Stack
          direction={{ xs: "column", sm: "row" }}
          alignItems={{ xs: "stretch", sm: "center" }}
          gap={{ xs: 2, sm: 4 }}
        >
          <Box>
            <Typography variant="h4" component="h1">
              Media
            </Typography>
            <Typography color="text.secondary">
              Manage your media catalog.
            </Typography>
          </Box>
          <Box sx={{ minWidth: 180 }}>
            <BaseSelect
              label="Media Type"
              value={activeMediaTypeId ?? ""}
              options={(mediaTypes ?? []).map((mt) => ({
                id: mt.id,
                label: mt.name,
              }))}
              isLoading={isMediaTypesLoading}
              onChange={(e) => {
                const next = Number(e.target.value);
                setSelectedMediaTypeId(next);
                dataGridProps.onPaginationModelChange({
                  ...dataGridProps.paginationModel,
                  page: 0,
                });
              }}
            />
          </Box>
        </Stack>

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
        <NoSsr>
          <BaseDataGrid
            loading={isMediaLoading || isMediaTypesLoading}
            error={!!mediaError || isMediaTypesError}
            rows={rows}
            columns={columns}
            rowCount={totalCount}
            {...dataGridProps}
          />
        </NoSsr>
      </Box>

      {draftRow ? (
        <MediaEditModal
          open={true}
          row={draftRow}
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
            (rows.find((r) => r.id === deleteRowId)?.title ?? "this media") +
            "?"}
        </BaseDialog>
      ) : null}
    </PageCard>
  );
}

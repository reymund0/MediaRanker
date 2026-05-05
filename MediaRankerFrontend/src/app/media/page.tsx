"use client";

import AddIcon from "@mui/icons-material/Add";
import { Box, Stack, Typography } from "@mui/material";
import { GridColDef, GridFilterModel, GridSortModel } from "@mui/x-data-grid";
import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useMutation } from "@/lib/api/use-mutation";
import { usePagedQuery } from "@/lib/api/use-paged-query";
import { useQuery } from "@/lib/api/use-query";
import { useUser } from "@/lib/auth/user-provider";
import { BaseDataGrid } from "@/lib/components/data-grid/base-data-grid";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { MediaDto, MediaUpsertRequest } from "./contracts";
import { MediaTypeDto } from "@/lib/contracts/shared";
import { buildMediaColumns, MediaRow, mapMediaToRow } from "./grid-utils";
import { MediaEditModal } from "./media-edit-modal";
import { PageCard } from "@/lib/components/layout/page-card";

export default function MediaPage() {
  const { showSuccess, showError } = useAlert();
  const { userId } = useUser();
  const queryClient = useQueryClient();

  const [paginationModel, setPaginationModel] = useState({
    page: 0,
    pageSize: 25,
  });
  const [sortModel, setSortModel] = useState<GridSortModel>([]);
  const [filterModel, setFilterModel] = useState<GridFilterModel>({ items: [] });
  const [draftRow, setDraftRow] = useState<MediaRow | undefined>(undefined);
  const [deleteRowId, setDeleteRowId] = useState<number | undefined>(undefined);

  const sortField =
    sortModel[0]?.sort === "asc" || sortModel[0]?.sort === "desc"
      ? sortModel[0].field
      : undefined;
  const sortDirection =
    sortModel[0]?.sort === "asc" || sortModel[0]?.sort === "desc"
      ? sortModel[0].sort
      : undefined;
  const filterItem = filterModel.items[0];
  const filterValue = String(filterItem?.value ?? "").trim();
  const searchField =
    filterItem?.field === "title" &&
    filterItem?.operator === "contains" &&
    filterValue.length > 0
      ? "title"
      : undefined;
  const searchTerm = searchField ? filterValue : undefined;

  const { items, totalCount, isLoading: isMediaLoading, error: mediaError } =
    usePagedQuery<MediaDto>({
      route: "/api/media",
      queryKey: ["media"],
      enabled: !!userId,
      pageSize: paginationModel.pageSize,
      pageRequest: {
        page: paginationModel.page,
        sortField,
        sortDirection,
        searchField,
        searchTerm,
      },
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

  const rows = items.map(mapMediaToRow);

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
    const defaultMediaType = mediaTypes?.[0] ?? { id: 0, name: "" };

    setDraftRow({
      id: undefined,
      title: "",
      mediaTypeId: defaultMediaType.id,
      mediaTypeName: defaultMediaType.name,
      releaseDate: null,
      createdAt: null,
      updatedAt: null,
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
          error={!!mediaError || isMediaTypesError}
          rows={rows}
          columns={columns}
          rowCount={totalCount}
          paginationMode="server"
          sortingMode="server"
          filterMode="server"
          paginationModel={paginationModel}
          onPaginationModelChange={(next) =>
            setPaginationModel((prev) =>
              next.pageSize !== prev.pageSize ? { ...next, page: 0 } : next,
            )
          }
          sortModel={sortModel}
          onSortModelChange={(m) => {
            setSortModel(m);
            setPaginationModel((p) => ({ ...p, page: 0 }));
          }}
          filterModel={filterModel}
          onFilterModelChange={(m) => {
            setFilterModel(m);
            setPaginationModel((p) => ({ ...p, page: 0 }));
          }}
          pageSizeOptions={[10, 25, 50, 100]}
          hideFooter={false}
        />
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

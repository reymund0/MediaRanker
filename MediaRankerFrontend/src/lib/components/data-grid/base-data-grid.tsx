import { Box, SxProps, Theme } from "@mui/material";
import {
  DataGrid,
  DataGridProps,
  GridRowParams,
  MuiEvent,
  GridCallbackDetails,
  GridRowId,
  GridValidRowModel,
} from "@mui/x-data-grid";
import { ReactNode, useMemo, useState } from "react";
import { BaseDetailPanel } from "./base-detail-panel";

type BaseDataGridProps<R extends GridValidRowModel> = DataGridProps<R> & {
  expandedRowIds?: GridRowId[];
  defaultExpandedRowIds?: GridRowId[];
  onExpandedRowIdsChange?: (rowIds: GridRowId[]) => void;
  getDetailPanelContent?: (row: R) => ReactNode;
  getDetailPanelHeight?: (row: R) => number | "auto";
  detailPanelSx?: SxProps<Theme>;
};

function toRowLookup<R extends GridValidRowModel>(rows: readonly R[], getId: (row: R) => GridRowId) {
  const lookup = new Map<GridRowId, R>();

  rows.forEach((row) => {
    lookup.set(getId(row), row);
  });

  return lookup;
}

export function BaseDataGrid<R extends GridValidRowModel>({
  expandedRowIds,
  defaultExpandedRowIds,
  onExpandedRowIdsChange,
  getDetailPanelContent,
  getDetailPanelHeight,
  detailPanelSx,
  rows,
  getRowId,
  onRowClick,
  ...dataGridProps
}: BaseDataGridProps<R>) {
  const [internalExpandedRowIds, setInternalExpandedRowIds] = useState<GridRowId[]>(defaultExpandedRowIds ?? []);

  const resolvedExpandedRowIds = expandedRowIds ?? internalExpandedRowIds;
  const resolveRowId = useMemo(() => getRowId ?? ((row: R) => row.id), [getRowId]);

  const rowLookup = useMemo(
    () => toRowLookup(rows ?? [], resolveRowId),
    [rows, resolveRowId],
  );

  const detailRows = useMemo(
    () => resolvedExpandedRowIds.map((rowId) => rowLookup.get(rowId)).filter((row): row is R => Boolean(row)),
    [resolvedExpandedRowIds, rowLookup],
  );

  const handleExpandedRowIdsChange = (nextRowIds: GridRowId[]) => {
    if (expandedRowIds === undefined) {
      setInternalExpandedRowIds(nextRowIds);
    }

    onExpandedRowIdsChange?.(nextRowIds);
  };

  const handleRowClick = (
    params: GridRowParams<R>,
    event: MuiEvent<React.MouseEvent<HTMLElement>>,
    details: GridCallbackDetails,
  ) => {
    onRowClick?.(params, event, details);

    if (!getDetailPanelContent) {
      return;
    }

    if (!getDetailPanelContent(params.row)) {
      return;
    }

    const nextExpandedRowIds = resolvedExpandedRowIds.includes(params.id)
      ? resolvedExpandedRowIds.filter((rowId) => rowId !== params.id)
      : [...resolvedExpandedRowIds, params.id];

    handleExpandedRowIdsChange(nextExpandedRowIds);
  };

  return (
    <Box>
      <DataGrid rows={rows} getRowId={getRowId} onRowClick={handleRowClick} {...dataGridProps} />

      {getDetailPanelContent
        ? detailRows.map((row) => {
            const detailContent = getDetailPanelContent(row);

            if (!detailContent) {
              return null;
            }

            const detailHeight = getDetailPanelHeight?.(row);
            const rowId = resolveRowId(row);

            return (
              <BaseDetailPanel
                key={String(rowId)}
                open
                sx={{
                  ...(typeof detailHeight === "number" ? { minHeight: detailHeight } : {}),
                  ...detailPanelSx,
                }}
              >
                {detailContent}
              </BaseDetailPanel>
            );
          })
        : null}
    </Box>
  );
}

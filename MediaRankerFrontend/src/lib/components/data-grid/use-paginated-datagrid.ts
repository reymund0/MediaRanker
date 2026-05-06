import { useState } from "react";
import { GridFilterModel, GridSortModel } from "@mui/x-data-grid";
import { PageRequest } from "@/lib/contracts/shared";

export type UsePaginatedDatagridOptions = {
  defaultPageSize?: number;
  pageSizeOptions?: number[];
};

export type UsePaginatedDatagridResult = {
  dataGridProps: {
    paginationMode: "server";
    sortingMode: "server";
    filterMode: "server";
    paginationModel: { page: number; pageSize: number };
    onPaginationModelChange: (next: { page: number; pageSize: number }) => void;
    sortModel: GridSortModel;
    onSortModelChange: (model: GridSortModel) => void;
    filterModel: GridFilterModel;
    onFilterModelChange: (model: GridFilterModel) => void;
    pageSizeOptions: number[];
    hideFooter: false;
  };
  pageRequest: Omit<PageRequest, "pageSize">;
};

export function usePaginatedDatagrid({
  defaultPageSize = 25,
  pageSizeOptions = [10, 25, 50, 100],
}: UsePaginatedDatagridOptions = {}): UsePaginatedDatagridResult {
  const [paginationModel, setPaginationModel] = useState({
    page: 0,
    pageSize: defaultPageSize,
  });
  const [sortModel, setSortModel] = useState<GridSortModel>([]);
  const [filterModel, setFilterModel] = useState<GridFilterModel>({
    items: [],
  });

  const sortItem = sortModel[0];
  const sortField =
    sortItem?.sort === "asc" || sortItem?.sort === "desc"
      ? sortItem.field
      : undefined;
  const sortDirection =
    sortItem?.sort === "asc" || sortItem?.sort === "desc"
      ? sortItem.sort
      : undefined;

  const filterItem = filterModel.items[0];
  const filterValue = String(filterItem?.value ?? "").trim();
  const searchField =
    filterItem?.field !== undefined && filterValue.length > 0
      ? filterItem.field
      : undefined;
  const searchTerm = searchField ? filterValue : undefined;

  const pageRequest: Omit<PageRequest, "pageSize" | "includeTotalCount"> = {
    page: paginationModel.page,
    sortField,
    sortDirection,
    searchField,
    searchTerm,
  };

  const onPaginationModelChange = (next: {
    page: number;
    pageSize: number;
  }) => {
    setPaginationModel((prev) =>
      next.pageSize !== prev.pageSize ? { ...next, page: 0 } : next,
    );
  };

  const onSortModelChange = (model: GridSortModel) => {
    setSortModel(model);
    setPaginationModel((p) => ({ ...p, page: 0 }));
  };

  const onFilterModelChange = (model: GridFilterModel) => {
    setFilterModel(model);
    setPaginationModel((p) => ({ ...p, page: 0 }));
  };

  return {
    dataGridProps: {
      paginationMode: "server",
      sortingMode: "server",
      filterMode: "server",
      paginationModel,
      onPaginationModelChange,
      sortModel,
      onSortModelChange,
      filterModel,
      onFilterModelChange,
      pageSizeOptions,
      hideFooter: false,
    },
    pageRequest,
  };
}

import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Chip, IconButton, Stack, Typography } from "@mui/material";
import { GridColDef, GridRenderCellParams } from "@mui/x-data-grid";
import { parseISO } from "date-fns";
import { TemplateDto, TemplateFieldDto } from "./contracts";
import { DateTimeCell } from "@/lib/components/data-grid/datetime-cell";

export type TemplateRow = Omit<
  TemplateDto,
  "id" | "createdAt" | "updatedAt"
> & {
  id: number | undefined;
  createdAt: Date | null;
  updatedAt: Date | null;
};

export type TemplateFieldRow = Omit<TemplateFieldDto, "id"> & {
  id: number | undefined;
};

export const mapTemplateToRow = (template: TemplateDto): TemplateRow => ({
  ...template,
  createdAt: template.createdAt ? parseISO(template.createdAt) : null,
  updatedAt: template.updatedAt ? parseISO(template.updatedAt) : null,
  // Sort fields by position (API might not return them in order).
  fields: template.fields.sort((a, b) => a.position - b.position),
});

interface BuildTemplateColumnsParams {
  onEditClick: (row: TemplateRow) => void;
  onDeleteClick: (row: TemplateRow) => void;
}

export function buildTemplateColumns({
  onEditClick,
  onDeleteClick,
}: BuildTemplateColumnsParams): GridColDef<TemplateRow>[] {
  return [
    {
      field: "name",
      headerName: "Name",
      flex: 2,
      sortable: false,
      renderCell: (params: GridRenderCellParams<TemplateRow>) => {
        return (
          <Stack
            direction="row"
            spacing={1}
            alignItems="center"
            sx={{ minWidth: 0 }}
          >
            <Typography noWrap>{params.value}</Typography>
            <Chip
              label={params.row.mediaTypeName}
              size="small"
              variant="outlined"
            />
            {params.row.isSystem ? (
              <Chip label="System" size="small" variant="outlined" />
            ) : null}
          </Stack>
        );
      },
    },
    {
      field: "description",
      headerName: "Description",
      flex: 3,
      sortable: false,
      renderCell: (params: GridRenderCellParams<TemplateRow, string>) => (
        <Typography noWrap color="text.secondary">
          {params.value || "-"}
        </Typography>
      ),
    },
    {
      field: "fields",
      headerName: "Fields",
      flex: 1.5,
      sortable: false,
      renderCell: (
        params: GridRenderCellParams<TemplateRow, TemplateFieldRow[]>,
      ) => (
        <Typography
          color="text.secondary"
          variant="body2"
          sx={{ wordBreak: "break-word" }}
        >
          {params.value?.map((field) => field.name).join(", ") || "-"}
        </Typography>
      ),
    },
    {
      field: "updatedAt",
      headerName: "Updated At",
      type: "dateTime",
      flex: 1,
      sortable: false,
      renderCell: (params: GridRenderCellParams<TemplateRow, Date | null>) => (
        <DateTimeCell value={params.value} />
      ),
    },
    {
      field: "actions",
      headerName: "Actions",
      width: 90,
      sortable: false,
      filterable: false,
      disableColumnMenu: true,
      renderCell: (params: GridRenderCellParams<TemplateRow>) => {
        return (
          <Stack direction="row" spacing={0.5}>
            <IconButton
              size="small"
              color="primary"
              disabled={params.row.isSystem}
              onClick={() => onEditClick(params.row)}
            >
              <EditOutlinedIcon fontSize="small" />
            </IconButton>
            <IconButton
              size="small"
              color="error"
              disabled={params.row.isSystem}
              onClick={() => onDeleteClick(params.row)}
            >
              <DeleteOutlineIcon fontSize="small" />
            </IconButton>
          </Stack>
        );
      },
    },
  ];
}

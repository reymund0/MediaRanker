import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Chip, IconButton, Stack, Typography } from "@mui/material";
import { GridColDef, GridRenderCellParams } from "@mui/x-data-grid";

export type TemplateRow = {
  id: string;
  name: string;
  description: string;
  updatedAt: string;
  isSystem: boolean;
  isTemporary?: boolean;
  templateFields: string[];
};

export type TemplateDraft = {
  name: string;
  description: string;
};

type BuildTemplateColumnsParams = {
  onEditClick: (row: TemplateRow) => void;
  onDeleteClick: (row: TemplateRow) => void;
};

export function buildTemplateColumns({
  onEditClick,
  onDeleteClick,
}: BuildTemplateColumnsParams): GridColDef<TemplateRow>[] {
  return [
    {
      field: "name",
      headerName: "Name",
      flex: 1,
      minWidth: 220,
      sortable: false,
      renderCell: (params: GridRenderCellParams<TemplateRow, string>) => {
        return (
          <Stack
            direction="row"
            spacing={1}
            alignItems="center"
            sx={{ minWidth: 0 }}
          >
            <Typography noWrap>{params.value}</Typography>
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
      flex: 1.5,
      minWidth: 320,
      sortable: false,
      renderCell: (params: GridRenderCellParams<TemplateRow, string>) => (
        <Typography noWrap color="text.secondary">
          {params.value || "-"}
        </Typography>
      ),
    },
    {
      field: "updatedAt",
      headerName: "UpdatedAt",
      width: 170,
      sortable: false,
      renderCell: (params: GridRenderCellParams<TemplateRow, string>) => (
        <Typography color="text.secondary">{params.value || "-"}</Typography>
      ),
    },
    {
      field: "actions",
      headerName: "Actions",
      width: 140,
      sortable: false,
      filterable: false,
      disableColumnMenu: true,
      renderCell: (params: GridRenderCellParams<TemplateRow>) => {
        const isSystem = params.row.isSystem;

        return (
          <Stack direction="row" spacing={0.5}>
            <IconButton
              size="small"
              color="primary"
              disabled={isSystem}
              onClick={() => onEditClick(params.row)}
            >
              <EditOutlinedIcon fontSize="small" />
            </IconButton>
            <IconButton
              size="small"
              color="error"
              disabled={isSystem}
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

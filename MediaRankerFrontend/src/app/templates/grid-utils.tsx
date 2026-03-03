import CloseIcon from "@mui/icons-material/Close";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Chip, IconButton, Stack, TextField, Typography } from "@mui/material";
import { GridColDef, GridRenderCellParams } from "@mui/x-data-grid";

export type TemplateRow = {
  id: string;
  name: string;
  description: string;
  updatedAt: string;
  isSystem: boolean;
  isTemporary?: boolean;
  fields: string[];
};

export type TemplateDraft = {
  name: string;
  description: string;
};

type BuildTemplateColumnsParams = {
  editingRowId: string | null;
  draft: TemplateDraft;
  onDraftChange: (updates: Partial<TemplateDraft>) => void;
  startEditing: (row: TemplateRow) => void;
  cancelEditing: () => void;
  removeRow: (row: TemplateRow) => void;
};

export function buildTemplateColumns({
  editingRowId,
  draft,
  onDraftChange,
  startEditing,
  cancelEditing,
  removeRow,
}: BuildTemplateColumnsParams): GridColDef<TemplateRow>[] {
  return [
    {
      field: "name",
      headerName: "Name",
      flex: 1,
      minWidth: 220,
      sortable: false,
      renderCell: (params: GridRenderCellParams<TemplateRow, string>) => {
        const isEditing = params.row.id === editingRowId;

        if (isEditing) {
          return (
            <TextField
              size="small"
              fullWidth
              value={draft.name}
              placeholder="Template name"
              onChange={(event) => onDraftChange({ name: event.target.value })}
            />
          );
        }

        return (
          <Stack direction="row" spacing={1} alignItems="center" sx={{ minWidth: 0 }}>
            <Typography noWrap>{params.value}</Typography>
            {params.row.isSystem ? <Chip label="System" size="small" variant="outlined" /> : null}
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
      renderCell: (params: GridRenderCellParams<TemplateRow, string>) => {
        const isEditing = params.row.id === editingRowId;

        if (isEditing) {
          return (
            <TextField
              size="small"
              fullWidth
              value={draft.description}
              placeholder="Template description"
              onChange={(event) => onDraftChange({ description: event.target.value })}
            />
          );
        }

        return <Typography noWrap color="text.secondary">{params.value || "-"}</Typography>;
      },
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
        const isEditing = params.row.id === editingRowId;
        const isSystem = params.row.isSystem;

        return (
          <Stack direction="row" spacing={0.5}>
            <IconButton
              size="small"
              color="primary"
              disabled={isSystem}
              onClick={() => {
                if (isEditing) {
                  cancelEditing();
                } else {
                  startEditing(params.row);
                }
              }}
            >
              {isEditing ? <CloseIcon fontSize="small" /> : <EditOutlinedIcon fontSize="small" />}
            </IconButton>
            <IconButton
              size="small"
              color="error"
              disabled={isSystem}
              onClick={() => removeRow(params.row)}
            >
              <DeleteOutlineIcon fontSize="small" />
            </IconButton>
          </Stack>
        );
      },
    },
  ];
}

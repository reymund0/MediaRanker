import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Chip, IconButton, Stack, Typography } from "@mui/material";
import { GridColDef, GridRenderCellParams } from "@mui/x-data-grid";
import { format, parseISO } from "date-fns";
import { MediaDto } from "./contracts";

export type MediaRow = Omit<
  MediaDto,
  "id" | "releaseDate" | "createdAt" | "updatedAt"
> & {
  id: number | undefined;
  releaseDate: Date | null;
  createdAt: Date | null;
  updatedAt: Date | null;
};

export const mapMediaToRow = (media: MediaDto): MediaRow => ({
  ...media,
  releaseDate: media.releaseDate ? parseISO(media.releaseDate) : null,
  createdAt: media.createdAt ? parseISO(media.createdAt) : null,
  updatedAt: media.updatedAt ? parseISO(media.updatedAt) : null,
});

interface BuildMediaColumnsParams {
  onEditClick: (row: MediaRow) => void;
  onDeleteClick: (row: MediaRow) => void;
}

export function buildMediaColumns({
  onEditClick,
  onDeleteClick,
}: BuildMediaColumnsParams): GridColDef<MediaRow>[] {
  return [
    {
      field: "title",
      headerName: "Title",
      flex: 3,
      minWidth: 240,
      sortable: false,
      renderCell: (params: GridRenderCellParams<MediaRow, string>) => (
        <Stack
          direction="row"
          spacing={1}
          alignItems="center"
          sx={{ minWidth: 0 }}
        >
          <Typography noWrap>{params.value}</Typography>
          <Chip
            label={params.row.mediaType.name}
            size="small"
            variant="outlined"
          />
        </Stack>
      ),
    },
    {
      field: "releaseDate",
      headerName: "Release Date",
      type: "date",
      flex: 1,
      minWidth: 160,
      sortable: false,
      renderCell: (params: GridRenderCellParams<MediaRow, Date | null>) => (
        <Typography color="text.secondary">
          {params.value ? format(params.value, "PPP") : "-"}
        </Typography>
      ),
    },
    {
      field: "updatedAt",
      headerName: "Updated At",
      type: "dateTime",
      flex: 1,
      minWidth: 170,
      sortable: false,
      renderCell: (params: GridRenderCellParams<MediaRow, Date | null>) => (
        <Typography color="text.secondary">
          {params.value ? format(params.value, "PPp") : "-"}
        </Typography>
      ),
    },
    {
      field: "actions",
      headerName: "Actions",
      width: 140,
      sortable: false,
      filterable: false,
      disableColumnMenu: true,
      renderCell: (params: GridRenderCellParams<MediaRow>) => {
        return (
          <Stack direction="row" spacing={0.5}>
            <IconButton
              size="small"
              color="primary"
              onClick={() => onEditClick(params.row)}
            >
              <EditOutlinedIcon fontSize="small" />
            </IconButton>
            <IconButton
              size="small"
              color="error"
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

import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import ImageIcon from "@mui/icons-material/Image";
import { Chip, IconButton, Stack, Typography, Box } from "@mui/material";
import { GridColDef, GridRenderCellParams } from "@mui/x-data-grid";
import { format, parseISO } from "date-fns";
import { MediaDto } from "./contracts";
import { DateTimeCell } from "@/lib/components/data-grid/datetime-cell";

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
      field: "coverImageUrl",
      headerName: "",
      width: 90,
      sortable: false,
      disableColumnMenu: true,
      renderCell: (
        params: GridRenderCellParams<MediaRow, string | undefined>,
      ) => (
        <Box sx={{ width: "100%", display: "flex", justifyContent: "center" }}>
          {params.value ? (
            <Box
              component="img"
              src={params.value}
              sx={{
                width: 44,
                height: 64,
                borderRadius: 1,
                objectFit: "cover",
                display: "block",
              }}
            />
          ) : (
            <ImageIcon
              sx={{
                width: 44,
                height: 64,
                borderRadius: 1,
                objectFit: "cover",
                display: "block",
                color: "text.disabled",
              }}
            />
          )}
        </Box>
      ),
    },
    {
      field: "title",
      headerName: "Title",
      flex: 2,
      renderCell: (params: GridRenderCellParams<MediaRow, string>) => (
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
        </Stack>
      ),
    },
    {
      field: "releaseDate",
      headerName: "Release Date",
      type: "date",
      flex: 1,
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
      renderCell: (params: GridRenderCellParams<MediaRow, Date | null>) => (
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

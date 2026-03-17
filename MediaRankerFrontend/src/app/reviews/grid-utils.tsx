
import { GridColDef, GridRenderCellParams } from "@mui/x-data-grid";
import { RankedMediaDto, RankedMediaScoreDto } from "./contracts";
import { IconButton, Stack } from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";

export type RankedMediaScoreRow = Omit<RankedMediaScoreDto, "id" | "createdAt" | "updatedAt"> & {
  id: number | undefined;
  createdAt: Date;
  updatedAt: Date;
};

export type RankedMediaRow = Omit<RankedMediaDto, "id" | "scores" | "consumedAt" | "createdAt" | "updatedAt"> & {
  id: number | undefined;
  consumedAt: Date | null;
  createdAt: Date;
  updatedAt: Date;
  scores: RankedMediaScoreRow[];
};

export const mapRankedMediaToRow = (rankedMedia: RankedMediaDto): RankedMediaRow => ({
  ...rankedMedia,
  id: rankedMedia.id,
  consumedAt: rankedMedia.consumedAt ? new Date(rankedMedia.consumedAt) : null,
  createdAt: new Date(rankedMedia.createdAt),
  updatedAt: new Date(rankedMedia.updatedAt),
  scores: rankedMedia.scores.map(score => ({
    ...score,
    createdAt: new Date(score.createdAt),
    updatedAt: new Date(score.updatedAt),
  })),
});

interface RankedMediaColumnsParams {
  onEditClick: (row: RankedMediaRow) => void;
  onDeleteClick: (row: RankedMediaRow) => void;
}

export function rankedMediaColumns({
  onEditClick,
  onDeleteClick,
}: RankedMediaColumnsParams): GridColDef<RankedMediaRow>[] {
  return [
    {
      flex: 2,
      field: "mediaTitle",
      headerName: "Title",
    },
    { 
      flex: 1,
      field: "mediaTypeName",
      headerName: "Type"
    },
    {
      flex: 1,
      field: "overallScore",
      headerName: "Score"
    },
    {
      flex: 1,
      field: "consumedAt",
      // TODO: Would be cool to rename to action verb based on mediaType. Consumed is weird.
      headerName: "Date Consumed"
    },
    {
      flex: 1,
      field: "templateName",
      headerName: "Template"
    },
    {
      field: "actions",
      headerName: "Actions",
      width: 90,
      sortable: false,
      filterable: false,
      disableColumnMenu: true,
      renderCell: (params: GridRenderCellParams<RankedMediaRow>) => {
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
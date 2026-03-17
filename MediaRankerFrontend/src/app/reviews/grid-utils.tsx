import { GridColDef, GridRenderCellParams } from "@mui/x-data-grid";
import { ReviewDto, ReviewFieldsDto } from "./contracts";
import { IconButton, Stack } from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";

export type ReviewFieldsRow = Omit<ReviewFieldsDto, "id" | "createdAt" | "updatedAt"> & {
  id: number | undefined;
  createdAt: Date;
  updatedAt: Date;
};

export type ReviewsRow = Omit<ReviewDto, "id" | "scores" | "consumedAt" | "createdAt" | "updatedAt"> & {
  id: number | undefined;
  consumedAt: Date | null;
  createdAt: Date;
  updatedAt: Date;
  scores: ReviewFieldsRow[];
};

export const mapReviewsToRow = (reviews: ReviewDto): ReviewsRow => ({
  ...reviews,
  id: reviews.id,
  consumedAt: reviews.consumedAt ? new Date(reviews.consumedAt) : null,
  createdAt: new Date(reviews.createdAt),
  updatedAt: new Date(reviews.updatedAt),
  scores: reviews.scores.map(score => ({
    ...score,
    createdAt: new Date(score.createdAt),
    updatedAt: new Date(score.updatedAt),
  })),
});

interface ReviewsColumnsParams {
  onEditClick: (row: ReviewsRow) => void;
  onDeleteClick: (row: ReviewsRow) => void;
}

export function reviewsColumns({
  onEditClick,
  onDeleteClick,
}: ReviewsColumnsParams): GridColDef<ReviewsRow>[] {
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
      renderCell: (params: GridRenderCellParams<ReviewsRow>) => {
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
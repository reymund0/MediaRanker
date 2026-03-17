import { GridColDef, GridRenderCellParams } from "@mui/x-data-grid";
import { ReviewDto, ReviewFieldDto } from "./contracts";
import { IconButton, Stack } from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";

export type ReviewFieldsRow = Omit<ReviewFieldDto, "id" | "createdAt" | "updatedAt"> & {
  id: number | undefined;
  createdAt: Date;
  updatedAt: Date;
};

export type ReviewRow = Omit<ReviewDto, "id" | "fields" | "consumedAt" | "createdAt" | "updatedAt"> & {
  id: number | undefined;
  consumedAt: Date | null;
  createdAt: Date;
  updatedAt: Date;
  fields: ReviewFieldsRow[];
};

export const mapReviewToRow = (review: ReviewDto): ReviewRow => ({
  ...review,
  id: review.id,
  consumedAt: review.consumedAt ? new Date(review.consumedAt) : null,
  createdAt: new Date(review.createdAt),
  updatedAt: new Date(review.updatedAt),
  fields: review.fields.map(field => ({
    ...field,
    createdAt: new Date(field.createdAt),
    updatedAt: new Date(field.updatedAt),
  })),
});

interface ReviewColumnsParams {
  onEditClick: (row: ReviewRow) => void;
  onDeleteClick: (row: ReviewRow) => void;
}

export function reviewsColumns({
  onEditClick,
  onDeleteClick,
}: ReviewColumnsParams): GridColDef<ReviewRow>[] {
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
      renderCell: (params: GridRenderCellParams<ReviewRow>) => {
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
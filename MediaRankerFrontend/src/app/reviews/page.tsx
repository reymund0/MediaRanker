"use client";

import AddIcon from "@mui/icons-material/Add";
import { BaseDataGrid } from "@/lib/components/data-grid/base-data-grid";
import { Box, Stack, Typography } from "@mui/material";
import { useQuery } from "@/lib/api/use-query";
import { useUser } from "@/lib/auth/user-provider";
import { ReviewDto } from "./contracts";
import { useState, useEffect } from "react";
import { ReviewsRow, mapReviewsToRow, reviewsColumns } from "./grid-utils";
import { useRouter } from "next/navigation";
import { useMutation } from "@/lib/api/use-mutation";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";

export default function ReviewsPage() {
  // Get reviews from API.
  // Display in reviews table, ordered by overall rank.
  // Would be cool to filter by media types.
  // Need a new page to actually add/edit reviews.
  
  const [rows, setRows] = useState<ReviewsRow[]>([]);
  const [deleteRowId, setDeleteRowId] = useState<number | undefined>(undefined);
  const { showSuccess, showError } = useAlert();

  const router = useRouter();

  const user = useUser();
  const userId = user.userId;

  const { data: reviews, isLoading: isReviewsLoading, isError: isReviewsError } = useQuery<ReviewDto[]>({
    route: "api/Reviews",
    queryKey: ["reviews"],
    enabled: !!userId,
  });

  const { mutate: deleteMutation } = useMutation<number, void>({
    route: (id: number) => `api/Reviews/${id}`,
    method: "DELETE",
  });

  useEffect(() => {
    const updateRows = async () => {
      if (!reviews) {
        return;
      }
      setRows(reviews.map(mapReviewsToRow));
    };
    updateRows();
  }, [reviews]);

  const columns = reviewsColumns({
    onEditClick: (row) => {
      // TODO: Would be nice to store a cookie with the users filter info so if they hit back button they come back to the same place.
      router.push(`/reviews/${row.id}`);
    },
    onDeleteClick: (row) => {
      if (!row.id) {
        setRows((prev) => prev.filter((r) => r.id !== row.id));
        return;
      }
      setDeleteRowId(row.id);
    },
  });

  const onDeleteConfirm = () => {
    setDeleteRowId(undefined);
    deleteMutation(deleteRowId!, {
      onSuccess: () => {
        setRows((prev) => prev.filter((r) => r.id !== deleteRowId));
        showSuccess("Review deleted successfully");
      },
      onError: (error) => {
        showError(error.message);
      },
    });
  };

  return (
    <Box sx={{ flex: 1, px: 3, py: 3 }}>
      <Stack
        direction="row"
        alignItems="center"
        justifyContent="space-between"
        sx={{ mb: 2 }}
      >
        <Box>
          <Typography variant="h4" component="h1">
            Reviews
          </Typography>
          <Typography color="text.secondary">
            Manage your reviews.
          </Typography>
        </Box>

        <PrimaryButton startIcon={<AddIcon />} onClick={() => router.push("/reviews/new")}>
          Add Review
        </PrimaryButton>
      </Stack>
      <BaseDataGrid
        columns={columns}
        rows={rows}
        loading={isReviewsLoading}
        error={isReviewsError}
      />
      <BaseDialog
        open={deleteRowId !== undefined}
        title="Delete Review"
        onClose={() => setDeleteRowId(undefined)}
        onConfirm={onDeleteConfirm}
      >
        {`Are you sure you want to delete the review for ${rows.find((r) => r.id === deleteRowId)?.mediaTitle}?`}
      </BaseDialog>
    </Box>
  );
}
"use client";

import { Box, CircularProgress, Stack, Typography } from "@mui/material";
import { ALL_MEDIA_TYPES, MEDIA_TYPE_LABELS, MediaType } from "@/lib/contracts/shared";
import { useUser } from "@/lib/auth/user-provider";
import { ReviewRow } from "./_components/review-row";
import { PageCard } from "@/lib/components/layout/page-card";

export default function ReviewsPage() {
  const { userId } = useUser();

  return (
    <PageCard>
      <Box sx={{ mb: 2 }}>
        <Typography variant="h4">Reviews</Typography>
        <Typography color="text.secondary">
          Browse and manage your reviews by media type.
        </Typography>
      </Box>

      <Stack direction="column" gap={4}>
        {ALL_MEDIA_TYPES.map((mt) => (
          <ReviewRow
            key={mt}
            mediaType={mt}
            mediaTypeLabel={MEDIA_TYPE_LABELS[mt as MediaType]}
            userId={userId!}
          />
        ))}
      </Stack>
    </PageCard>
  );
}

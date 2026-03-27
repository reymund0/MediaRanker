"use client";

import { Box, CircularProgress, Stack, Typography } from "@mui/material";
import { MediaTypeDto } from "@/lib/contracts/shared";
import { useQuery } from "@/lib/api/use-query";
import { ReviewRow } from "./review-row";
import { useUser } from "@/lib/auth/user-provider";

export default function ReviewsPage() {
  const { userId } = useUser();

  const { data: mediaTypes, isLoading, isError } = useQuery<MediaTypeDto[]>({
    route: "/api/mediaTypes",
    queryKey: ["media-types"],
    enabled: !!userId,
  });

  if (isLoading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError) {
    return (
      <Typography color="error" sx={{ p: 3 }}>
        Failed to load media types.
      </Typography>
    );
  }

  return (
    <Stack direction="column" gap={4} sx={{ px: 3, py: 3 }}>
      {(mediaTypes ?? []).map((mediaType) => (
        <ReviewRow key={mediaType.id} label={mediaType.name} mediaTypeId={mediaType.id} />
      ))}
    </Stack>
  );
}
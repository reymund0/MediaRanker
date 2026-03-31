"use client";

import { Box, Card, CardContent, CircularProgress, Stack, Typography } from "@mui/material";
import { MediaTypeDto } from "@/lib/contracts/shared";
import { useQuery } from "@/lib/api/use-query";
import { ReviewRow } from "./_components/review-row";
import { useUser } from "@/lib/auth/user-provider";

export default function ReviewsPage() {
  const { userId } = useUser();

  const { data: mediaTypes, isLoading, isError } = useQuery<MediaTypeDto[]>({
    route: "/api/mediaTypes",
    queryKey: ["media-types"],
    enabled: !!userId,
  });

  return (
    <Box sx={{ flex: 1, py: 3, maxWidth: "1400px", width: "100%", mx: "auto" }}>
      <Card>
        <CardContent sx={{ p: 3 }}>
          <Box sx={{ mb: 2 }}>
            <Typography variant="h4" component="h1">
              Reviews
            </Typography>
            <Typography color="text.secondary">
              Browse and manage your reviews by media type.
            </Typography>
          </Box>

          {isLoading ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
              <CircularProgress />
            </Box>
          ) : isError ? (
            <Typography color="error">Failed to load media types.</Typography>
          ) : (
            <Stack direction="column" gap={4}>
              {(mediaTypes ?? []).map((mediaType) => (
                <ReviewRow
                  key={mediaType.id}
                  label={mediaType.name}
                  mediaTypeId={mediaType.id}
                />
              ))}
            </Stack>
          )}
        </CardContent>
      </Card>
    </Box>
  );
}
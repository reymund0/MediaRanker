"use client";

import { Stack } from "@mui/material";
import { useState, useEffect } from "react";
import { MediaTypeDto } from "@/lib/contracts/shared";
import { useQuery } from "@/lib/api/use-query";
import { ReviewRow } from "./review-row";
import { useUser } from "@/lib/auth/user-provider";

export default function ReviewsPage() {

  const {userId} = useUser();
  const [mediaTypes, setMediaTypes] = useState<MediaTypeDto[]>([]);

  const { data: mediaTypesData, isLoading: mediaTypesLoading, error: mediaTypesError } = useQuery<MediaTypeDto[]>({
    route: "/api/mediatypes",
    queryKey: ["media-types"],
    enabled: !!userId
  });

  useEffect(() => {
    if (mediaTypesData) {
      setMediaTypes(mediaTypesData);
    }
  }, [mediaTypesData]);


  return (
    <Stack direction="column" gap={2}>
      {mediaTypes.map((mediaType) => (
        <ReviewRow key={mediaType.id} label={mediaType.name} queryRoute={`/api/reviews/byMediaType/${mediaType.id}`} />
      ))}
    </Stack>
  )

}
"use client";

import { BaseSelect, BaseSelectValue } from "@/lib/components/inputs/select/base-select";
import { Box, Stack, Typography } from "@mui/material";
import { useQuery } from "@/lib/api/use-query";
import { MediaTypeDto } from "@/lib/contracts/shared";
import { useEffect, useState } from "react";
import { BaseSelectOption } from "@/lib/components/inputs/select/base-select";
import { useUser } from "@/lib/auth/user-provider";
import { UnreviewedMediaDto } from "../contracts";
import { BaseAutocomplete } from "@/lib/components/inputs/autocomplete/base-autocomplete";
import { ErrorCard } from "@/lib/components/surfaces/card/error-card";


export default function NewReviewPage() {
  const { userId } = useUser();

  const [mediaTypeOptions, setMediaTypeOptions] = useState<BaseSelectOption[]>([]);
  const [selectedMediaType, setSelectedMediaType] = useState<BaseSelectValue>('');
  const [unreviewedMediaOptions, setUnreviewedMediaOptions] = useState<BaseSelectOption[]>([]);
  const [selectedUnreviewedMedia, setSelectedUnreviewedMedia] = useState<BaseSelectValue>('');

  const { data: mediaTypes, error: mediaTypesError, isLoading: mediaTypesLoading } = useQuery<MediaTypeDto[]>({
    route: 'api/mediaTypes',
    queryKey: ['mediaTypes'],
    enabled: !!userId
  });

  useEffect(() => {
    if (mediaTypes) {
      setMediaTypeOptions(mediaTypes.map(item => ({ id: item.id, label: item.name })));
    }
  }, [mediaTypes]);

  const { data: unreviewedMedia, isLoading: unreviewedMediaLoading, error: unreviewedMediaError } = useQuery<UnreviewedMediaDto[]>({
    route: `api/Reviews/unreviewedByType?mediaTypeId=${selectedMediaType}`,
    queryKey: ['unreviewedMedia', selectedMediaType],
    enabled: !!userId && !!selectedMediaType
  });

  useEffect(() => {
    if (unreviewedMedia) {
      setUnreviewedMediaOptions(unreviewedMedia.map(item => ({ id: item.id, label: item.title, imageUrl: item.coverImageUrl })));
    }
  }, [unreviewedMedia]);
  



  return (
    <Box>
      <Stack
        direction="column"
        alignItems="center"
        sx={{ mb: 2 }}
      >
        <Typography variant="h4">New Review</Typography>
        <Typography>Select a type and use the search to start a new media review.</Typography>
        <Typography variant="body2" color="text.secondary">(Search results displays only unreviewed media.)</Typography>

        <Box sx={{ width: '20rem', m: 2 }}>
          { mediaTypesError ? (
            <ErrorCard title="Error loading media types" message={mediaTypesError.message} />
          ) : (
            <BaseSelect
              options={mediaTypeOptions}
              label="Select a type"
              value={selectedMediaType}
              isLoading={mediaTypesLoading || !mediaTypeOptions.length}
              onChange={(e) => {
                setSelectedMediaType(e.target.value);
              }}
            />
          )}
        </Box>
        {
          selectedMediaType && (
            <Box sx={{ width: '40rem', m: 2 }}>
              { unreviewedMediaError ? (
                <ErrorCard title="Error loading unreviewed media" message={unreviewedMediaError.message} />
              ) : (
                <BaseAutocomplete
                  label="Search for media"
                  options={unreviewedMediaOptions}
                  onChange={(e) => setSelectedUnreviewedMedia(e.target.value)}
                  isLoading={unreviewedMediaLoading}
                />
              )}
            </Box>
          )
        }
      </Stack>
    </Box>
  )
}
"use client";

import { Button, Stack, Typography } from "@mui/material";
import { BaseAutocomplete } from "@/lib/components/inputs/autocomplete/base-autocomplete";
import {
  BaseSelect,
  BaseSelectOption,
} from "@/lib/components/inputs/select/base-select";
import { TemplateDto } from "@/lib/contracts/shared";
import { UnreviewedMediaDto } from "../contracts";
import { usePagedQuery } from "@/lib/api/use-paged-query";
import { useQuery } from "@/lib/api/use-query";
import { useState } from "react";
import { useUser } from "@/lib/auth/user-provider";
import { ReviewFormValues } from "./review-card-utils";
import { TemplateFieldDisplay } from "./review-card-edit";

type ReviewCardNewStepsProps = {
  mediaType: string;
  onNewReview: (
    review: ReviewFormValues,
    mediaTitle: string,
    templateFields: TemplateFieldDisplay[],
  ) => void;
  onCancel: () => void;
};

type NewReviewStep = "select-media" | "select-template";

export function ReviewCardNewSteps({
  mediaType,
  onNewReview,
  onCancel,
}: ReviewCardNewStepsProps) {
  const { userId } = useUser();
  const [selectedMedia, setSelectedMedia] =
    useState<BaseSelectOption<UnreviewedMediaDto> | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [currentStep, setCurrentStep] = useState<NewReviewStep>("select-media");

  const {
    items: unreviewedMedia,
    isLoading: isUnreviewedMediaLoading,
  } = usePagedQuery<UnreviewedMediaDto>({
    route: "/api/reviews/unreviewedByType",
    routeParams: { mediaType },
    queryKey: ["unreviewedMedia", mediaType],
    enabled: currentStep === "select-media",
    pageSize: 10,
    pageRequest: {
      searchField: "title",
      searchTerm: searchInput,
    },
  });

  const {
    data: templates,
    isLoading: isTemplatesLoading,
  } = useQuery<TemplateDto[]>({
    route: `/api/templates/${mediaType}`,
    queryKey: ["templates", mediaType],
    enabled: currentStep === "select-template",
  });

  if (currentStep === "select-media") {
    return (
      <Stack
        direction="column"
        sx={{ height: "100%", px: 1.5 }}
        gap={2}
        justifyContent="center"
      >
        <Typography variant="subtitle2">Select Media</Typography>
        <BaseAutocomplete<UnreviewedMediaDto>
          label="Search media"
          options={unreviewedMedia.map((m) => ({
            id: m.id,
            label: m.title,
            metadata: m,
          }))}
          isLoading={isUnreviewedMediaLoading}
          searchInput={searchInput}
          onSearchChange={setSearchInput}
          onSelectOption={(option) => {
            if (option?.metadata) {
              setSelectedMedia(option);
              setCurrentStep("select-template");
            }
          }}
        />
      </Stack>
    );
  }

  if (currentStep === "select-template") {
    return (
      <Stack
        direction="column"
        sx={{ height: "100%", p: 1.5 }}
        gap={2}
        justifyContent="center"
      >
        <Typography variant="subtitle2">Select Template</Typography>
        <BaseSelect
          label="Template"
          value=""
          options={(templates ?? []).map((t) => ({
            id: t.id,
            label: t.name,
            metadata: t,
          }))}
          isLoading={isTemplatesLoading}
          onChange={(e) => {
            const id = Number(e.target.value);
            const tmpl = templates?.find((t) => t.id === id) ?? null;
            if (tmpl) {
              onNewReview(
                {
                  fields: tmpl.fields.reduce(
                    (acc, field) => {
                      acc[field.id] = 5;
                      return acc;
                    },
                    {} as Record<string, number>,
                  ),
                  id: 0,
                  mediaId: selectedMedia!.metadata!.id,
                  templateId: id,
                },
                selectedMedia?.metadata?.title ?? "",
                tmpl.fields,
              );
            }
          }}
        />
        <Button size="small" variant="text" onClick={onCancel}>
          Back
        </Button>
      </Stack>
    );
  }

  return null;
}

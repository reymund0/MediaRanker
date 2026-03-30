"use client";

import { Button, Stack, Typography } from "@mui/material";
import { BaseAutocomplete } from "@/lib/components/inputs/autocomplete/base-autocomplete";
import { BaseSelect } from "@/lib/components/inputs/select/base-select";
import { TemplateDto, TemplateFieldDto } from "@/lib/contracts/shared";
import { UnreviewedMediaDto } from "./contracts";
import { useQuery } from "@/lib/api/use-query";
import { useState } from "react";
import { useUser } from "@/lib/auth/user-provider";
import { ReviewFormValues } from "./review-card-utils";
import { TemplateFieldDisplay } from "./review-card-edit";


type ReviewCardNewStepsProps = {
  mediaTypeId: number;
  onNewReview: (review: ReviewFormValues, mediaTitle: string, tempalteFields: TemplateFieldDisplay[]) => void;
  onCancel: () => void;
};

type NewReviewStep = "select-media" | "select-template";

export function ReviewCardNewSteps({
  mediaTypeId,
  onCancel,
  onNewReview,
}: ReviewCardNewStepsProps) {

  console.log("ReviewCardNewSteps rendered")
  console.log("mediaTypeId", mediaTypeId)

  const {userId} = useUser();

  const [selectedUnreviewedMediaId, setSelectedUnreviewedMediaId] = useState<number | undefined>(undefined);
  
  const [currentStep, setCurrentStep] = useState<NewReviewStep>("select-media");


  const { data: unreviewedMedia, isLoading: unreviewedLoading } = useQuery<UnreviewedMediaDto[]>({
    route: `/api/reviews/unreviewedByType?mediaTypeId=${mediaTypeId ?? 0}`,
    queryKey: ["unreviewed", mediaTypeId],
    enabled: !!userId,
  });

  const { data: templates, isLoading: templatesLoading } = useQuery<TemplateDto[]>({
    route: `/api/templates/${mediaTypeId ?? 0}`,
    queryKey: ["templates-by-type", mediaTypeId],
    enabled: !!userId && !!selectedUnreviewedMediaId,
  });
  if (currentStep === "select-media") {
    return (
      <Stack direction="column" sx={{ height: "100%", px: 1.5 }} gap={2} justifyContent="center">
        <Typography variant="subtitle2">Select Media</Typography>
        <BaseAutocomplete<UnreviewedMediaDto>
          label="Search media"
          options={(unreviewedMedia ?? []).map((m) => ({ id: m.id, label: m.title, metadata: m }))}
          isLoading={unreviewedLoading}
          onSelectOption={(option) => {
            if (option?.metadata) {
              setSelectedUnreviewedMediaId(option.metadata.id);
              setCurrentStep("select-template");
            }
          }}
        />
      </Stack>
    );
  }

  if (currentStep === "select-template") {
    return (
      <Stack direction="column" sx={{ height: "100%", p: 1.5 }} gap={2} justifyContent="center">
        <Typography variant="subtitle2">Select Template</Typography>
        <BaseSelect
          label="Template"
          options={(templates ?? []).map((t) => ({ id: t.id, label: t.name, metadata: t }))}
          isLoading={templatesLoading}
          onChange={(e) => {
            const id = Number(e.target.value);
            const tmpl = templates?.find((t) => t.id === id) ?? null;
            if (tmpl) {
              onNewReview({
                fields: tmpl.fields.reduce((acc, field) => {
                  acc[field.id] = 5;
                  return acc;
                }, {} as Record<string, number>),
                id: 0,
                mediaId: selectedUnreviewedMediaId!,
                templateId: id,
              }, 
              unreviewedMedia?.find((m) => m.id === selectedUnreviewedMediaId)?.title ?? "", 
              tmpl.fields);
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

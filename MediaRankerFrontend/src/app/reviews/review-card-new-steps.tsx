"use client";

import { Button, Stack, Typography } from "@mui/material";
import { BaseAutocomplete } from "@/lib/components/inputs/autocomplete/base-autocomplete";
import { BaseSelect } from "@/lib/components/inputs/select/base-select";
import { TemplateDto } from "@/lib/contracts/shared";
import { UnreviewedMediaDto } from "./contracts";
import { NewStep } from "./review-card-constants";

type ReviewCardNewStepsProps = {
  newStep: NewStep;
  setNewStep: (step: NewStep) => void;
  unreviewedMedia: UnreviewedMediaDto[] | undefined;
  unreviewedLoading: boolean;
  setSelectedMedia: (media: UnreviewedMediaDto) => void;
  templates: TemplateDto[] | undefined;
  templatesLoading: boolean;
  setSelectedTemplate: (template: TemplateDto) => void;
  selectedTemplateId: number | undefined;
  onReadyForReview: () => void;
  onCancel: () => void;
};

export function ReviewCardNewSteps({
  newStep,
  setNewStep,
  unreviewedMedia,
  unreviewedLoading,
  setSelectedMedia,
  templates,
  templatesLoading,
  setSelectedTemplate,
  selectedTemplateId,
  onCancel,
  onReadyForReview,
}: ReviewCardNewStepsProps) {
  if (newStep === "select-media") {
    return (
      <Stack direction="column" sx={{ height: "100%", p: 1.5 }} gap={2} justifyContent="center">
        <Typography variant="subtitle2">Select Media</Typography>
        <BaseAutocomplete<UnreviewedMediaDto>
          label="Search media"
          options={(unreviewedMedia ?? []).map((m) => ({ id: m.id, label: m.title, metadata: m }))}
          isLoading={unreviewedLoading}
          onSelectOption={(option) => {
            if (option?.metadata) {
              setSelectedMedia(option.metadata);
              setNewStep("select-template");
            }
          }}
        />
        <Button size="small" variant="text" onClick={onCancel}>
          Cancel
        </Button>
      </Stack>
    );
  }

  if (newStep === "select-template") {
    return (
      <Stack direction="column" sx={{ height: "100%", p: 1.5 }} gap={2} justifyContent="center">
        <Typography variant="subtitle2">Select Template</Typography>
        <BaseSelect
          label="Template"
          options={(templates ?? []).map((t) => ({ id: t.id, label: t.name, metadata: t }))}
          isLoading={templatesLoading}
          value={selectedTemplateId ?? ""}
          onChange={(e) => {
            const id = Number(e.target.value);
            const tmpl = templates?.find((t) => t.id === id) ?? null;
            if (tmpl) {
              setSelectedTemplate(tmpl);
              onReadyForReview();
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

"use client";

import CancelIcon from "@mui/icons-material/Cancel";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import ImageNotSupportedIcon from "@mui/icons-material/ImageNotSupported";
import {
  Box,
  Button,
  CircularProgress,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { BaseStarRating } from "@/lib/components/inputs/rating/base-star-rating";
import { FormStarRating } from "@/lib/components/inputs/rating/form-star-rating";
import { FormTextField } from "@/lib/components/inputs/text-field/form-text-field";
import { BaseAutocomplete } from "@/lib/components/inputs/autocomplete/base-autocomplete";
import { BaseSelect } from "@/lib/components/inputs/select/base-select";
import { useQuery } from "@/lib/api/use-query";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { TemplateDto } from "@/lib/contracts/shared";
import { ReviewDto, ReviewUpsertRequest, UnreviewedMediaDto } from "./contracts";

export const CARD_WIDTH = 260;
export const CARD_GAP = 16;
const CARD_HEIGHT = 380;
const COVER_HEIGHT = Math.round(CARD_HEIGHT * 0.6);
const INFO_HEIGHT = CARD_HEIGHT - COVER_HEIGHT;

// ---------------------------------------------------------------------------
// Form schema for edit mode
// ---------------------------------------------------------------------------

const buildReviewSchema = (fieldIds: number[]) =>
  z.object({
    reviewTitle: z.string().optional(),
    notes: z.string().optional(),
    fields: z.object(
      Object.fromEntries(
        fieldIds.map((id) => [
          String(id),
          z
            .number({ message: "Score required" })
            .min(1, "Min 1")
            .max(10, "Max 10"),
        ]),
      ),
    ),
  });

type ReviewFormValues = {
  reviewTitle?: string;
  notes?: string;
  fields: Record<string, number>;
};

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

type ExistingCardProps = {
  review: ReviewDto;
  isNew?: never;
  mediaTypeId?: never;
  onSave?: never;
  onCancel?: never;
  onUpdate: (updated: ReviewDto) => void;
  onDelete: (reviewId: number) => void;
};

type NewCardProps = {
  review?: never;
  isNew: true;
  mediaTypeId: number;
  onSave: (review: ReviewDto) => void;
  onCancel: () => void;
  onUpdate?: never;
  onDelete?: never;
};

export type ReviewCardProps = ExistingCardProps | NewCardProps;

type CardFace = "front" | "back-view" | "back-edit";
type NewStep = "select-media" | "select-template" | "edit";

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function ReviewCard(props: ReviewCardProps) {
  const { showSuccess, showError } = useAlert();

  // ---- State ----
  const [face, setFace] = useState<CardFace>(props.isNew ? "back-edit" : "front");
  const [newStep, setNewStep] = useState<NewStep>("select-media");
  const [selectedMedia, setSelectedMedia] = useState<UnreviewedMediaDto | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<TemplateDto | null>(null);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  // ---- Derived ----
  const review = props.review ?? null;
  const isNew = !!props.isNew;

  const templateForEdit = selectedTemplate;
  const fieldIds = isNew
    ? (templateForEdit?.fields.map((f) => f.id) ?? [])
    : (review?.fields.map((f) => f.templateFieldId) ?? []);

  // ---- Queries (new card flow) ----
  const { data: unreviewedMedia, isLoading: unreviewedLoading } = useQuery<UnreviewedMediaDto[]>({
    route: `/api/reviews/unreviewedByType?mediaTypeId=${props.mediaTypeId ?? 0}`,
    queryKey: ["unreviewed", props.mediaTypeId],
    enabled: isNew,
  });

  const { data: templates, isLoading: templatesLoading } = useQuery<TemplateDto[]>({
    route: `/api/templates/${props.mediaTypeId ?? 0}`,
    queryKey: ["templates-by-type", props.mediaTypeId],
    enabled: isNew,
  });

  // ---- Mutations ----
  const { mutate: upsertReview, isPending: isSaving } = useMutation<ReviewUpsertRequest, ReviewDto>({
    route: "/api/reviews",
    method: "POST",
  });

  const { mutate: deleteReview, isPending: isDeleting } = useMutation<number, void>({
    route: (id) => `/api/reviews/${id}`,
    method: "DELETE",
  });

  // ---- Form ----
  const schema = buildReviewSchema(fieldIds);
  const methods = useForm<ReviewFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      reviewTitle: review?.reviewTitle ?? "",
      notes: review?.notes ?? "",
      fields: Object.fromEntries(
        (review?.fields ?? []).map((f) => [String(f.templateFieldId), f.value]),
      ),
    },
  });

  // Reset form when review data changes (after save/update)
  useEffect(() => {
    if (review) {
      methods.reset({
        reviewTitle: review.reviewTitle ?? "",
        notes: review.notes ?? "",
        fields: Object.fromEntries(
          review.fields.map((f) => [String(f.templateFieldId), f.value]),
        ),
      });
    }
  }, [review, methods]);

  // Reset form when template is selected in new card flow
  useEffect(() => {
    if (templateForEdit && isNew) {
      methods.reset({
        reviewTitle: "",
        notes: "",
        fields: Object.fromEntries(
          templateForEdit.fields.map((f) => [String(f.id), 0]),
        ),
      });
    }
  }, [templateForEdit, isNew, methods]);

  // ---- Handlers ----
  const handleSave = methods.handleSubmit((data) => {
    const mediaId = isNew ? selectedMedia!.id : review!.mediaId;
    const templateId = isNew ? selectedTemplate!.id : review!.templateId;

    const request: ReviewUpsertRequest = {
      id: isNew ? null : review!.id,
      mediaId,
      templateId,
      reviewTitle: data.reviewTitle?.trim() || null,
      notes: data.notes?.trim() || null,
      consumedAt: null,
      fields: Object.entries(data.fields).map(([templateFieldId, value]) => ({
        templateFieldId: Number(templateFieldId),
        value: value as number,
      })),
    };

    upsertReview(request, {
      onSuccess: (saved) => {
        showSuccess("Review saved");
        if (isNew) {
          props.onSave!(saved);
        } else {
          props.onUpdate!(saved);
          setFace("back-view");
        }
      },
      onError: (err) => showError(err.message),
    });
  });

  const handleDelete = () => {
    if (!review) return;
    deleteReview(review.id, {
      onSuccess: () => {
        showSuccess("Review deleted");
        setShowDeleteConfirm(false);
        props.onDelete!(review.id);
      },
      onError: (err) => {
        showError(err.message);
        setShowDeleteConfirm(false);
      },
    });
  };

  // ---- Cover image helper ----
  const coverUrl = isNew ? selectedMedia?.coverImageUrl : review?.mediaCoverImageUrl;
  const mediaTitle = isNew ? (selectedMedia?.title ?? "") : (review?.mediaTitle ?? "");

  // ---- Render helpers ----
  const renderCoverOrPlaceholder = () => (
    <Box
      sx={{
        width: "100%",
        height: COVER_HEIGHT,
        overflow: "hidden",
        flexShrink: 0,
        bgcolor: "action.hover",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      {coverUrl ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={coverUrl}
          alt={mediaTitle}
          style={{ width: "100%", height: "100%", objectFit: "cover" }}
        />
      ) : (
        <ImageNotSupportedIcon sx={{ fontSize: 56, color: "text.disabled" }} />
      )}
    </Box>
  );

  // ---- Front face ----
  const renderFront = () => (
    <Box
      onClick={() => setFace("back-view")}
      sx={{ cursor: "pointer", height: "100%", display: "flex", flexDirection: "column" }}
    >
      {renderCoverOrPlaceholder()}
      <Box
        sx={{
          height: INFO_HEIGHT,
          px: 1.5,
          py: 1,
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          gap: 0.5,
        }}
      >
        <Typography variant="subtitle2" noWrap title={review?.mediaTitle}>
          {review?.mediaTitle}
        </Typography>
        <BaseStarRating
          value={review?.overallScore ?? 0}
          onChange={() => {}}
          disabled
          size="small"
        />
      </Box>
    </Box>
  );

  // ---- Back view face ----
  const renderBackView = () => (
    <Stack direction="column" sx={{ height: "100%", p: 1.5, overflow: "auto" }} gap={1}>
      <Typography variant="subtitle1" fontWeight={600} noWrap>
        {review?.mediaTitle}
      </Typography>
      {review?.reviewTitle && (
        <Typography variant="body2" color="text.secondary">
          {review.reviewTitle}
        </Typography>
      )}
      {review?.notes && (
        <Typography variant="body2" sx={{ whiteSpace: "pre-wrap" }}>
          {review.notes}
        </Typography>
      )}
      <Box>
        <Typography variant="caption" color="text.secondary">
          Overall
        </Typography>
        <BaseStarRating
          value={review?.overallScore ?? 0}
          onChange={() => {}}
          disabled
          size="small"
        />
      </Box>
      {review?.fields.map((field) => (
        <Box key={field.templateFieldId}>
          <Typography variant="caption" color="text.secondary">
            {field.templateFieldName}
          </Typography>
          <BaseStarRating
            value={field.value}
            onChange={() => {}}
            disabled
            size="small"
          />
        </Box>
      ))}
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mt: "auto", pt: 1 }}>
        <Button size="small" variant="text" onClick={() => setFace("front")}>
          Back
        </Button>
        <Button size="small" variant="outlined" onClick={() => setFace("back-edit")}>
          Edit
        </Button>
      </Stack>
    </Stack>
  );

  // ---- Back edit face (new card: step-based) ----
  const renderNewCardSteps = () => {
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
          <Button size="small" variant="text" onClick={props.onCancel}>
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
            value={selectedTemplate?.id ?? ""}
            onChange={(e) => {
              const id = Number(e.target.value);
              const tmpl = templates?.find((t) => t.id === id) ?? null;
              setSelectedTemplate(tmpl);
              if (tmpl) setNewStep("edit");
            }}
          />
          <Button size="small" variant="text" onClick={() => setNewStep("select-media")}>
            Back
          </Button>
        </Stack>
      );
    }

    return null;
  };

  const renderEditFields = () => {
    const fieldList = isNew
      ? (templateForEdit?.fields ?? []).map((f) => ({ id: f.id, name: f.name }))
      : (review?.fields ?? []).map((f) => ({ id: f.templateFieldId, name: f.templateFieldName }));

    return (
      <FormProvider {...methods}>
        <Stack
          component="form"
          direction="column"
          sx={{ height: "100%", p: 1.5, overflow: "auto" }}
          gap={1.5}
          onSubmit={handleSave}
        >
          <Typography variant="subtitle2" noWrap>
            {isNew ? selectedMedia?.title : review?.mediaTitle}
          </Typography>
          <FormTextField<ReviewFormValues>
            name="reviewTitle"
            label="Review title"
            size="small"
          />
          <FormTextField<ReviewFormValues>
            name="notes"
            label="Notes"
            size="small"
            multiline
            minRows={2}
          />
          {fieldList.map((field) => (
            <FormStarRating<ReviewFormValues>
              key={field.id}
              name={`fields.${field.id}` as `fields.${string}`}
              label={field.name}
            />
          ))}
          <Stack direction="row" justifyContent="flex-end" sx={{ mt: "auto", pt: 1 }}>
            <Button
              type="submit"
              size="small"
              variant="contained"
              disabled={isSaving}
              loading={isSaving}
            >
              Save
            </Button>
          </Stack>
          {!isNew && (
            <Button
              size="small"
              variant="text"
              onClick={() => setFace("back-view")}
            >
              Cancel
            </Button>
          )}
        </Stack>
      </FormProvider>
    );
  };

  const renderBackEdit = () => {
    if (isNew && newStep !== "edit") {
      return renderNewCardSteps();
    }
    return renderEditFields();
  };

  // ---- Top-right action icon ----
  const renderTopRightAction = () => {
    if (face === "back-edit") {
      if (isNew) {
        return (
          <IconButton
            size="small"
            onClick={props.onCancel}
            sx={{ position: "absolute", top: 6, right: 6, zIndex: 1, color: "error.main" }}
          >
            <CancelIcon fontSize="small" />
          </IconButton>
        );
      }
      return (
        <IconButton
          size="small"
          onClick={() => setShowDeleteConfirm(true)}
          disabled={isDeleting}
          sx={{ position: "absolute", top: 6, right: 6, zIndex: 1, color: "error.main" }}
        >
          {isDeleting ? <CircularProgress size={16} /> : <DeleteOutlineIcon fontSize="small" />}
        </IconButton>
      );
    }
    return null;
  };

  // ---- Card container ----
  return (
    <>
      <Box
        sx={{
          position: "relative",
          width: CARD_WIDTH,
          minWidth: CARD_WIDTH,
          height: CARD_HEIGHT,
          borderRadius: 2,
          border: "1px solid",
          borderColor: "divider",
          overflow: "hidden",
          bgcolor: "background.paper",
          flexShrink: 0,
        }}
      >
        {renderTopRightAction()}
        {face === "front" && renderFront()}
        {face === "back-view" && renderBackView()}
        {face === "back-edit" && renderBackEdit()}
      </Box>

      {showDeleteConfirm && (
        <BaseDialog
          open
          title="Delete Review"
          confirmLabel="Delete"
          confirmLoading={isDeleting}
          onConfirm={handleDelete}
          onClose={() => setShowDeleteConfirm(false)}
        >
          Are you sure you want to delete your review for{" "}
          <strong>{review?.mediaTitle}</strong>?
        </BaseDialog>
      )}
    </>
  );
}

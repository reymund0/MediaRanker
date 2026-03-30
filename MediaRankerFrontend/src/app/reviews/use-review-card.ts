"use client";

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@/lib/api/use-query";
import { useMutation } from "@/lib/api/use-mutation";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { TemplateDto } from "@/lib/contracts/shared";
import { ReviewDto, ReviewUpsertRequest, UnreviewedMediaDto } from "./contracts";
import { buildReviewSchema } from "./review-card-schema";
import {
  CardState,
  NewStep,
  ReviewCardProps,
  ReviewFormValues,
} from "./review-card-constants";

export function useReviewCard(props: ReviewCardProps) {
  const { showSuccess, showError } = useAlert();

  // ---- State ----
  const [state, setState] = useState<CardState>(props.isNew ? "edit" : "view");
  const [newStep, setNewStep] = useState<NewStep>("select-media");
  const [selectedMedia, setSelectedMedia] = useState<UnreviewedMediaDto | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<TemplateDto | null>(null);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  // ---- Derived ----
  const review = props.review ?? null;
  const isNew = !!props.isNew;

  const fieldIds = isNew
    ? (selectedTemplate?.fields.map((f) => f.id) ?? [])
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
    if (selectedTemplate && isNew) {
      methods.reset({
        reviewTitle: "",
        notes: "",
        fields: Object.fromEntries(
          selectedTemplate.fields.map((f) => [String(f.id), 0]),
        ),
      });
    }
  }, [selectedTemplate, isNew, methods]);

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
          setState("detailed-view");
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

  return {
    // State
    state,
    setState,
    newStep,
    setNewStep,
    selectedMedia,
    setSelectedMedia,
    selectedTemplate,
    setSelectedTemplate,
    showDeleteConfirm,
    setShowDeleteConfirm,
    // Derived
    review,
    isNew,
    // Query data
    unreviewedMedia,
    unreviewedLoading,
    templates,
    templatesLoading,
    // Mutation state
    isSaving,
    isDeleting,
    // Form
    methods,
    // Handlers
    handleSave,
    handleDelete,
  };
}

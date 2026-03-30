"use client";

import CancelIcon from "@mui/icons-material/Cancel";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import { Box, CircularProgress, IconButton } from "@mui/material";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { useReviewCard } from "./use-review-card";
import { ReviewCardPreview } from "./review-card-preview";
import { ReviewCardDetailView } from "./review-card-detailed-view";
import { ReviewCardEdit } from "./review-card-edit";
import { ReviewCardNewSteps } from "./review-card-new-steps";
import { CARD_WIDTH, CARD_HEIGHT } from "./review-card-constants";
import type { ReviewCardProps } from "./review-card-constants";

export function ReviewCard(props: ReviewCardProps) {
  const {
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
    review,
    isNew,
    unreviewedMedia,
    unreviewedLoading,
    templates,
    templatesLoading,
    isSaving,
    isDeleting,
    methods,
    handleSave,
    handleDelete,
  } = useReviewCard(props);

  const fieldList = isNew
    ? (selectedTemplate?.fields ?? []).map((f) => ({ id: f.id, name: f.name }))
    : (review?.fields ?? []).map((f) => ({ id: f.templateFieldId, name: f.templateFieldName }));

  const mediaTitle = isNew ? (selectedMedia?.title ?? "") : (review?.mediaTitle ?? "");

  const RenderReviewEditCard = () => {
    if (isNew && newStep !== "edit") {
      return (
        <ReviewCardNewSteps
          newStep={newStep}
          setNewStep={setNewStep}
          unreviewedMedia={unreviewedMedia}
          unreviewedLoading={unreviewedLoading}
          setSelectedMedia={setSelectedMedia}
          templates={templates}
          templatesLoading={templatesLoading}
          setSelectedTemplate={setSelectedTemplate}
          selectedTemplateId={selectedTemplate?.id}
          onReadyForReview={() => setState("edit")}
          onCancel={props.onCancel!}
        />
      );
    }
    return (
      <ReviewCardEdit
        mediaTitle={mediaTitle}
        fieldList={fieldList}
        methods={methods}
        isSaving={isSaving}
        isNew={isNew}
        onSave={handleSave}
        onCancel={() => setState("detailed-view")}
      />
    );
  };

  const renderTopRightAction = () => {
    if (state !== "edit") return null;
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
  };

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
        {state === "view" && review && (
          <ReviewCardPreview review={review} onClick={() => setState("detailed-view")} />
        )}
        {state === "detailed-view" && review && (
          <ReviewCardDetailView
            review={review}
            onBack={() => setState("view")}
            onEdit={() => setState("edit")}
          />
        )}
        {state === "edit" && RenderReviewEditCard()}
      </Box>

      {showDeleteConfirm && review && (
        <BaseDialog
          open
          title="Delete Review"
          confirmLabel="Delete"
          confirmLoading={isDeleting}
          onConfirm={handleDelete}
          onClose={() => setShowDeleteConfirm(false)}
        >
          Are you sure you want to delete your review for{" "}
          <strong>{review.mediaTitle}</strong>?
        </BaseDialog>
      )}
    </>
  );
}

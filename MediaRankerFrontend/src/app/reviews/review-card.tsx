"use client";

import CancelIcon from "@mui/icons-material/Cancel";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import { Box, CircularProgress, IconButton } from "@mui/material";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { useReviewCard } from "./use-review-card";
import { ReviewCardFront } from "./review-card-front";
import { ReviewCardBackView } from "./review-card-back-view";
import { ReviewCardBackEdit } from "./review-card-back-edit";
import { ReviewCardNewSteps } from "./review-card-new-steps";
import { CARD_WIDTH, CARD_HEIGHT } from "./review-card-constants";
export { CARD_WIDTH, CARD_GAP } from "./review-card-constants";
export type { ReviewCardProps } from "./review-card-constants";

export function ReviewCard(props: import("./review-card-constants").ReviewCardProps) {
  const {
    face,
    setFace,
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

  const renderBackEdit = () => {
    if (isNew && newStep !== "edit") {
      return (
        <ReviewCardNewSteps
          newStep={newStep}
          unreviewedMedia={unreviewedMedia}
          unreviewedLoading={unreviewedLoading}
          templates={templates}
          templatesLoading={templatesLoading}
          selectedTemplateId={selectedTemplate?.id}
          onMediaSelect={(media) => {
            setSelectedMedia(media);
            setNewStep("select-template");
          }}
          onTemplateSelect={(tmpl) => {
            setSelectedTemplate(tmpl);
            setNewStep("edit");
          }}
          onBackToMedia={() => setNewStep("select-media")}
          onCancel={props.onCancel!}
        />
      );
    }
    return (
      <ReviewCardBackEdit
        mediaTitle={mediaTitle}
        fieldList={fieldList}
        methods={methods}
        isSaving={isSaving}
        isNew={isNew}
        onSave={handleSave}
        onCancel={() => setFace("back-view")}
      />
    );
  };

  const renderTopRightAction = () => {
    if (face !== "back-edit") return null;
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
        {face === "front" && review && (
          <ReviewCardFront review={review} onFlip={() => setFace("back-view")} />
        )}
        {face === "back-view" && review && (
          <ReviewCardBackView
            review={review}
            onBack={() => setFace("front")}
            onEdit={() => setFace("back-edit")}
          />
        )}
        {face === "back-edit" && renderBackEdit()}
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

"use client";

import { REVIEW_QUERY_ROOT } from "../reviews/_components/review-query";

import { useState } from "react";
import {
  Add,
  ContentCopy,
  EditOutlined,
  LockOutlined,
} from "@mui/icons-material";
import {
  Box,
  Button,
  Card,
  Chip,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import { useQueryClient } from "@tanstack/react-query";
import { useQuery } from "@/lib/api/use-query";
import { useMutation } from "@/lib/api/use-mutation";
import { useUser } from "@/lib/auth/user-provider";
import { MEDIA_TYPE_LABELS, MediaType } from "@/lib/contracts/shared";
import { PageContainer } from "@/lib/components/layout/page-container";
import { BaseDialog } from "@/lib/components/feedback/dialog/base-dialog";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { TemplateDto, TemplateUpsertRequest } from "./contracts";
import { TemplateEditor } from "./template-edit-modal";

export default function TemplatesPage() {
  const { userId } = useUser();
  const client = useQueryClient();
  const { showSuccess, showError } = useAlert();
  const [draft, setDraft] = useState<TemplateUpsertRequest>();
  const [editorKey, setEditorKey] = useState(0);
  const [pendingSave, setPendingSave] = useState<TemplateUpsertRequest>();
  const [removing, setRemoving] = useState<TemplateUpsertRequest>();
  const {
    data: templates = [],
    isLoading,
    error,
  } = useQuery<TemplateDto[]>({
    route: "/api/templates",
    queryKey: ["templates"],
    enabled: !!userId,
  });
  const save = useMutation<TemplateUpsertRequest, TemplateDto>({
    route: "/api/templates",
    method: "POST",
  });
  const remove = useMutation<number, void>({
    route: (id) => `/api/templates/${id}`,
    method: "DELETE",
  });
  const open = (template?: TemplateDto, duplicate = false) => {
    setEditorKey((key) => key + 1);
    setDraft({
      id: duplicate ? null : (template?.id ?? null),
      name: template ? template.name + (duplicate ? " (copy)" : "") : "",
      description: template?.description ?? null,
      mediaType: template?.mediaType ?? MediaType.VideoGame,
      fields: template
        ? [...template.fields]
            .sort((a, b) => a.position - b.position)
            .map((field, position) => ({
              id: duplicate ? null : field.id,
              name: field.name,
              position,
            }))
        : [],
    });
  };
  const submit = async (data: TemplateUpsertRequest) => {
    try {
      const saved = await save.mutateAsync(data);
      await client.cancelQueries({ queryKey: ["templates"], exact: true });
      client.setQueryData<TemplateDto[]>(["templates"], (previous) => [
        ...(previous ?? []).filter((template) => template.id !== saved.id),
        saved,
      ]);
      client.invalidateQueries({ queryKey: ["templates"] });
      client.invalidateQueries({ queryKey: REVIEW_QUERY_ROOT });
      setPendingSave(undefined);
      setDraft(undefined);
      showSuccess("Template saved");
    } catch (error) {
      showError((error as Error).message);
    }
  };
  return (
    <PageContainer>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: "400px minmax(0, 1fr)",
          gap: 5,
          alignItems: "start",
        }}
      >
        <Stack spacing={3}>
          <Box>
            <Typography variant="overline" color="primary.light">
              Templates
            </Typography>
            <Typography variant="h1" sx={{ fontSize: 44 }}>
              What you score
            </Typography>
            <Typography color="text.secondary" sx={{ mt: 1 }}>
              A template sets which scores a review asks for. Each media type
              uses its own.
            </Typography>
          </Box>
          <Button
            variant="outlined"
            startIcon={<Add />}
            onClick={() => open()}
            sx={{ borderStyle: "dashed" }}
          >
            New template
          </Button>
          {isLoading && <Typography>Loading templates…</Typography>}
          {error && (
            <Typography role="alert" color="error">
              {error.message}
            </Typography>
          )}
          {templates.map((template) => (
            <Card
              key={template.id}
              sx={{
                p: 2.5,
                borderColor:
                  draft?.id === template.id ? "primary.light" : "divider",
                bgcolor:
                  draft?.id === template.id
                    ? "action.selected"
                    : "background.paper",
              }}
            >
              <Stack
                direction="row"
                justifyContent="space-between"
                alignItems="center"
              >
                <Typography fontWeight={600}>{template.name}</Typography>
                {template.isSystem ? (
                  <Chip size="small" icon={<LockOutlined />} label="Built-in" />
                ) : draft?.id === template.id ? (
                  <Chip
                    size="small"
                    label="Editing"
                    sx={{ bgcolor: "action.selected", color: "secondary.main" }}
                  />
                ) : null}
              </Stack>
              <Typography variant="caption" color="text.secondary">
                {MEDIA_TYPE_LABELS[template.mediaType]} ·{" "}
                {template.fields.length} scores
              </Typography>
              <Stack
                direction="row"
                useFlexGap
                flexWrap="wrap"
                spacing={0.75}
                sx={{ mt: 1.5 }}
              >
                {[...template.fields]
                  .sort((a, b) => a.position - b.position)
                  .map((field) => (
                    <Chip size="small" key={field.id} label={field.name} />
                  ))}
              </Stack>
              <Divider sx={{ my: 1.5 }} />
              <Stack
                direction="row"
                justifyContent="space-between"
                alignItems="center"
              >
                <Typography variant="caption" color="text.secondary">
                  {template.isSystem
                    ? "Built-in templates are read-only."
                    : "Your custom template"}
                </Typography>
                <Stack direction="row" spacing={1}>
                  {!template.isSystem && draft?.id !== template.id && (
                    <Button
                      size="small"
                      startIcon={<EditOutlined />}
                      onClick={() => open(template)}
                    >
                      Edit
                    </Button>
                  )}
                  <Button
                    variant="outlined"
                    size="small"
                    startIcon={<ContentCopy />}
                    onClick={() => open(template, true)}
                  >
                    Duplicate
                  </Button>
                </Stack>
              </Stack>
            </Card>
          ))}
        </Stack>
        {draft ? (
          <TemplateEditor
            key={editorKey}
            draft={draft}
            onSubmit={async (data) => {
              const removedFields = draft.fields.filter(
                (field) =>
                  field.id !== null &&
                  !data.fields.some((next) => next.id === field.id),
              );
              if (draft.id && removedFields.length > 0) setPendingSave(data);
              else await submit(data);
            }}
            onCancel={() => setDraft(undefined)}
            onDelete={draft.id ? () => setRemoving(draft) : undefined}
          />
        ) : (
          <Box
            sx={{
              p: 6,
              border: "1px dashed",
              borderColor: "divider",
              borderRadius: 1.5,
              mt: 1,
              textAlign: "center",
            }}
          >
            <Typography variant="h5">Make it your own</Typography>
            <Typography color="text.secondary" sx={{ mt: 1 }}>
              Choose a template to edit, or duplicate a built-in template to
              start.
            </Typography>
          </Box>
        )}
      </Box>
      {pendingSave && (
        <BaseDialog
          open
          danger
          title="Remove scores from this template?"
          closeLabel="Cancel"
          confirmLabel="Remove scores and save"
          confirmLoading={save.isPending}
          onClose={() => {
            if (!save.isPending) setPendingSave(undefined);
          }}
          onConfirm={() => {
            void submit(pendingSave);
          }}
        >
          Removing{" "}
          {draft?.fields
            .filter(
              (field) =>
                field.id !== null &&
                !pendingSave.fields.some((next) => next.id === field.id),
            )
            .map((field) => field.name)
            .join(", ")}{" "}
          deletes those scores from existing reviews and recalculates their
          overall score. Reviews left with no scores are deleted. This action
          cannot be undone.
        </BaseDialog>
      )}
      {removing?.id && (
        <BaseDialog
          open
          danger
          title={`Delete “${removing.name}”?`}
          closeLabel="Cancel"
          confirmLabel="Delete template"
          confirmLoading={remove.isPending}
          onClose={() => {
            if (!remove.isPending) setRemoving(undefined);
          }}
          onConfirm={() =>
            remove.mutate(removing.id!, {
              onSuccess: () => {
                client.invalidateQueries({ queryKey: ["templates"] });
                client.invalidateQueries({ queryKey: REVIEW_QUERY_ROOT });
                setDraft(undefined);
                setRemoving(undefined);
                showSuccess("Template deleted");
              },
              onError: (error) => showError(error.message),
            })
          }
        >
          This deletes the template and its reviews. This action cannot be
          undone.
        </BaseDialog>
      )}
    </PageContainer>
  );
}

import type { ReviewFieldUpsertRequest } from "../contracts";

export function isReviewScore(value: unknown): value is number {
  return (
    typeof value === "number" &&
    Number.isFinite(value) &&
    Number.isInteger(value) &&
    value >= 1 &&
    value <= 10
  );
}

export function mapReviewScoreFields(
  orderedFields: readonly { id: number }[],
  values: Readonly<Record<string, unknown>> | null | undefined,
): ReviewFieldUpsertRequest[] | null {
  if (!values) return null;

  const requests: ReviewFieldUpsertRequest[] = [];
  for (const field of orderedFields) {
    const value = values[field.id];
    if (!isReviewScore(value)) return null;
    requests.push({ templateFieldId: field.id, value });
  }

  return requests;
}

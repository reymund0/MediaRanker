import assert from "node:assert/strict";
import test from "node:test";
import {
  isReviewScore,
  mapReviewScoreFields,
} from "../../../src/app/reviews/_components/review-score-values.ts";

test("isReviewScore accepts finite integer scores from 1 through 10", () => {
  for (let score = 1; score <= 10; score += 1) {
    assert.equal(isReviewScore(score), true);
  }

  for (const value of [0, 11, -1, 1.5, NaN, Infinity, -Infinity, null, "5", undefined]) {
    assert.equal(isReviewScore(value), false);
  }
});

test("mapReviewScoreFields preserves field order and ignores unrelated values", () => {
  const orderedFields = [{ id: 12 }, { id: 4 }, { id: 9 }];
  const values = { "4": 8, "9": 3, "12": 10, extra: 6 };

  assert.deepEqual(mapReviewScoreFields(orderedFields, values), [
    { templateFieldId: 12, value: 10 },
    { templateFieldId: 4, value: 8 },
    { templateFieldId: 9, value: 3 },
  ]);
});

test("mapReviewScoreFields rejects null, missing, and invalid field values", () => {
  const orderedFields = [{ id: 1 }, { id: 2 }];

  for (const values of [
    null,
    undefined,
    {},
    { "1": 4, "2": null },
    { "1": 4, "2": NaN },
    { "1": 4, "2": 2.5 },
    { "1": 4, "2": 0 },
    { "1": 4, "2": 11 },
    { "1": 4, "2": "5" },
  ]) {
    assert.equal(mapReviewScoreFields(orderedFields, values), null);
  }
});

test("mapReviewScoreFields does not mutate its inputs", () => {
  const orderedFields = [{ id: 2 }, { id: 1 }];
  const values = { "1": 6, "2": 7, extra: 9 };
  const originalFields = structuredClone(orderedFields);
  const originalValues = structuredClone(values);

  mapReviewScoreFields(orderedFields, values);

  assert.deepEqual(orderedFields, originalFields);
  assert.deepEqual(values, originalValues);
});

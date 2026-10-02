import assert from "node:assert/strict";
import test from "node:test";
import { roundToEven } from "./review-rounding.mjs";

test("roundToEven resolves half values to the nearest even score", () => {
  assert.equal(roundToEven(2.5), 2);
  assert.equal(roundToEven(3.5), 4);
  assert.equal(roundToEven(8.5), 8);
  assert.equal(roundToEven(9.5), 10);
});

test("roundToEven keeps values on either side of a half", () => {
  assert.equal(roundToEven(8.49), 8);
  assert.equal(roundToEven(8.51), 9);
});

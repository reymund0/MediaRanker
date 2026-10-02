import test from "node:test";
import assert from "node:assert/strict";
import {
  consumeResetIdentifier,
  getPasswordUpdateError,
  getRecoveryRequestErrorOutcome,
  getResetConfirmationError,
  getSignInDestination,
  saveResetIdentifier,
} from "../../../src/lib/auth/password-decisions.ts";

function makeStorage() {
  const entries = new Map();
  return {
    entries,
    getItem: (key) => entries.get(key) ?? null,
    setItem: (key, value) => entries.set(key, value),
    removeItem: (key) => entries.delete(key),
  };
}

test("sign-in only enters protected content after a completed result", () => {
  const result = (isSignedIn, signInStep) =>
    getSignInDestination({ isSignedIn, nextStep: { signInStep } });

  assert.equal(result(true, "DONE"), "authenticated");
  assert.equal(result(false, "RESET_PASSWORD"), "reset-password");
  assert.equal(result(false, "CONFIRM_SIGN_UP"), "confirm-signup");
  assert.equal(result(false, "DONE"), "unsupported");
  assert.equal(result(true, "RESET_PASSWORD"), "unsupported");
  assert.equal(result(false, "CONFIRM_SIGN_IN_WITH_NEW_PASSWORD_REQUIRED"), "unsupported");
});

test("request errors that can reveal account status use one neutral outcome", () => {
  for (const name of [
    "UserNotFoundException",
    "NotAuthorizedException",
    "InvalidParameterException",
    "LimitExceededException",
    "TooManyRequestsException",
  ]) {
    assert.equal(getRecoveryRequestErrorOutcome(name), "neutral");
  }

  assert.equal(getRecoveryRequestErrorOutcome("NetworkError"), "retry");
  assert.equal(getRecoveryRequestErrorOutcome(undefined), "retry");
});

test("confirmation and signed-in change errors never use raw provider text", () => {
  assert.match(getResetConfirmationError("CodeMismatchException"), /incorrect/);
  assert.match(getResetConfirmationError("ExpiredCodeException"), /expired/);
  assert.match(getResetConfirmationError("InvalidPasswordException"), /requirements/);
  assert.match(getPasswordUpdateError("NotAuthorizedException"), /current password/);
  assert.match(getPasswordUpdateError("InvalidPasswordException"), /requirements/);
  assert.match(getPasswordUpdateError("LimitExceededException"), /Wait/);
  assert.match(getPasswordUpdateError("UserUnAuthenticatedException"), /sign in/);
  assert.doesNotMatch(getPasswordUpdateError("provider-detail-123"), /provider-detail-123/);
});

test("identifier handoff stores no code or password and is consumed once", () => {
  const storage = makeStorage();
  saveResetIdentifier(storage, "person@example.com", 1000);
  assert.equal(storage.entries.size, 1);
  const payload = JSON.parse([...storage.entries.values()][0]);
  assert.deepEqual(Object.keys(payload).sort(), ["expiresAt", "identifier"]);
  assert.equal(consumeResetIdentifier(storage, 1001), "person@example.com");
  assert.equal(consumeResetIdentifier(storage, 1002), undefined);
  assert.equal(storage.entries.size, 0);
});

test("expired or malformed handoff cannot prefill recovery", () => {
  const storage = makeStorage();
  saveResetIdentifier(storage, "person@example.com", 1000);
  assert.equal(consumeResetIdentifier(storage, 301000), undefined);
  assert.equal(storage.entries.size, 0);

  storage.setItem("media-ranker.reset-identifier", "bad-json");
  assert.equal(consumeResetIdentifier(storage, 1000), undefined);
  assert.equal(storage.entries.size, 0);
});

test("unavailable tab storage leaves manual recovery available", () => {
  const storage = {
    getItem: () => {
      throw new Error("blocked");
    },
    removeItem: () => {},
  };
  assert.equal(consumeResetIdentifier(storage, 1000), undefined);
});

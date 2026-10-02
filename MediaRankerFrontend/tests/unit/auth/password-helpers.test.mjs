import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import { createRequire } from "node:module";
import {
  consumeResetIdentifier,
  getPasswordUpdateError,
  getRecoveryRequestErrorOutcome,
  getResetConfirmationError,
  getSignInDestination,
  saveResetIdentifier,
} from "../../../src/lib/auth/password-decisions.ts";

const require = createRequire(import.meta.url);
const ts = require("typescript");
const helperSource = readFileSync(
  new URL("../../../src/app/auth/helpers.tsx", import.meta.url),
  "utf8",
);
const helperCode = ts.transpileModule(helperSource, {
  compilerOptions: { module: ts.ModuleKind.CommonJS },
}).outputText;

function loadHelpers(auth, localTestActive = false) {
  const exports = {};
  const modules = {
    "aws-amplify/auth": auth,
    "@/lib/auth/local-test-auth": {
      isLocalTestAuthActive: () => localTestActive,
    },
    "@/lib/auth/password-decisions": {
      getPasswordUpdateError,
      getRecoveryRequestErrorOutcome,
      getResetConfirmationError,
    },
  };

  runInNewContext(helperCode, {
    exports,
    require: (name) => {
      assert.ok(Object.hasOwn(modules, name), `Unexpected import: ${name}`);
      return modules[name];
    },
  });
  return exports;
}

function providerError(name) {
  return Object.assign(new Error("private provider detail"), { name });
}

test("request and resend pass only the identifier to Cognito", async () => {
  const calls = [];
  const helpers = loadHelpers({
    resetPassword: async (input) => {
      calls.push(input);
      return { nextStep: { resetPasswordStep: "CONFIRM_RESET_PASSWORD_WITH_CODE" } };
    },
  });

  const request = await helpers.handleResetPassword("test-user");
  const resend = await helpers.handleResetPassword("test-user");

  assert.equal(request.success, true);
  assert.equal(resend.success, true);
  assert.equal(request.data.nextStep.resetPasswordStep, "CONFIRM_RESET_PASSWORD_WITH_CODE");
  assert.equal(calls.length, 2);
  assert.deepEqual(calls.map(({ username }) => username), ["test-user", "test-user"]);
  assert.ok(calls.every((input) => Object.keys(input).join() === "username"));
});

test("request hides account status and exposes service failures without provider text", async () => {
  for (const name of ["UserNotFoundException", "TooManyRequestsException"]) {
    const helpers = loadHelpers({
      resetPassword: async () => { throw providerError(name); },
    });
    const result = await helpers.handleResetPassword("test-user");
    assert.equal(result.success, true);
    assert.equal(result.error, undefined);
  }

  const helpers = loadHelpers({
    resetPassword: async () => { throw providerError("NetworkError"); },
  });
  const result = await helpers.handleResetPassword("test-user");
  assert.equal(result.success, false);
  assert.match(result.error, /try again/i);
  assert.doesNotMatch(result.error, /private provider detail/);
});

test("invalid and expired recovery codes return distinct safe messages", async () => {
  for (const [name, expected] of [
    ["CodeMismatchException", /incorrect/],
    ["ExpiredCodeException", /expired/],
  ]) {
    const helpers = loadHelpers({
      confirmResetPassword: async () => { throw providerError(name); },
    });
    const result = await helpers.handleConfirmResetPassword(
      "test-user",
      "synthetic-code",
      "synthetic-new-password",
    );
    assert.equal(result.success, false);
    assert.match(result.error, expected);
    assert.doesNotMatch(result.error, /private provider detail|synthetic-code|synthetic-new-password/);
  }
});

test("successful recovery submits the expected Cognito fields", async () => {
  let submitted;
  const helpers = loadHelpers({
    confirmResetPassword: async (input) => { submitted = input; },
  });

  const result = await helpers.handleConfirmResetPassword(
    "test-user",
    "synthetic-code",
    "synthetic-new-password",
  );

  assert.equal(result.success, true);
  assert.equal(submitted.username, "test-user");
  assert.equal(submitted.confirmationCode, "synthetic-code");
  assert.equal(submitted.newPassword, "synthetic-new-password");
  assert.deepEqual(Object.keys(submitted).sort(), [
    "confirmationCode", "newPassword", "username",
  ]);
});

test("required-reset sign-in hands off only the identifier", async () => {
  const helpers = loadHelpers({
    signIn: async () => ({
      isSignedIn: false,
      nextStep: { signInStep: "RESET_PASSWORD" },
    }),
  });
  const signIn = await helpers.handleLogin({
    usernameOrEmail: "test-user",
    password: "synthetic-current-password",
  });
  assert.equal(signIn.success, true);
  assert.equal(getSignInDestination(signIn.data), "reset-password");

  const entries = new Map();
  const storage = {
    getItem: (key) => entries.get(key) ?? null,
    setItem: (key, value) => entries.set(key, value),
    removeItem: (key) => entries.delete(key),
  };
  saveResetIdentifier(storage, "test-user", 1000);
  assert.doesNotMatch([...entries.values()][0], /synthetic-current-password/);
  assert.equal(consumeResetIdentifier(storage, 1001), "test-user");
  assert.equal(entries.size, 0);
});

test("signed-in password change is blocked for the local test identity", async () => {
  let called = false;
  const helpers = loadHelpers({
    updatePassword: async () => { called = true; },
  }, true);
  const result = await helpers.handleUpdatePassword(
    "synthetic-current-password",
    "synthetic-new-password",
  );
  assert.equal(result.success, false);
  assert.match(result.error, /local test user/);
  assert.equal(called, false);
});

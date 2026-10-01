export type SignInDestination =
  | "authenticated"
  | "reset-password"
  | "confirm-signup"
  | "unsupported";

export function getSignInDestination(result: {
  isSignedIn: boolean;
  nextStep: { signInStep: string };
}): SignInDestination {
  const step = result.nextStep.signInStep;

  if (result.isSignedIn && step === "DONE") {
    return "authenticated";
  }

  if (!result.isSignedIn && step === "RESET_PASSWORD") {
    return "reset-password";
  }

  if (!result.isSignedIn && step === "CONFIRM_SIGN_UP") {
    return "confirm-signup";
  }

  return "unsupported";
}

const NEUTRAL_RECOVERY_ERRORS = new Set([
  "UserNotFoundException",
  "NotAuthorizedException",
  "InvalidParameterException",
  "LimitExceededException",
  "TooManyRequestsException",
]);

export function getRecoveryRequestErrorOutcome(
  errorName: string | undefined,
): "neutral" | "retry" {
  return errorName && NEUTRAL_RECOVERY_ERRORS.has(errorName)
    ? "neutral"
    : "retry";
}

export function getResetConfirmationError(errorName: string | undefined): string {
  switch (errorName) {
    case "CodeMismatchException":
      return "The code is incorrect. Check it and try again.";
    case "ExpiredCodeException":
      return "The code has expired. Request a new code and try again.";
    case "InvalidPasswordException":
      return "The new password does not meet the account's password requirements.";
    case "LimitExceededException":
    case "TooManyRequestsException":
      return "Too many attempts. Wait a few minutes and try again.";
    default:
      return "We couldn't reset the password. Please try again.";
  }
}

export function getPasswordUpdateError(errorName: string | undefined): string {
  switch (errorName) {
    case "NotAuthorizedException":
      return "The current password was rejected. Check it and try again, or sign in again if your session expired.";
    case "InvalidPasswordException":
      return "The new password does not meet the account's password requirements.";
    case "LimitExceededException":
    case "TooManyRequestsException":
      return "Too many attempts. Wait a few minutes and try again.";
    case "UserUnAuthenticatedException":
    case "AuthTokenConfigException":
      return "Your session has expired. Please sign in again.";
    default:
      return "We couldn't change the password. Please try again.";
  }
}

const RESET_IDENTIFIER_KEY = "media-ranker.reset-identifier";
const RESET_IDENTIFIER_TTL_MS = 5 * 60 * 1000;

type HandoffStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

export function saveResetIdentifier(
  storage: HandoffStorage,
  identifier: string,
  nowMs: number,
): void {
  storage.setItem(
    RESET_IDENTIFIER_KEY,
    JSON.stringify({ identifier, expiresAt: nowMs + RESET_IDENTIFIER_TTL_MS }),
  );
}

export function consumeResetIdentifier(
  storage: HandoffStorage,
  nowMs: number,
): string | undefined {
  let value: string | null;
  try {
    value = storage.getItem(RESET_IDENTIFIER_KEY);
    storage.removeItem(RESET_IDENTIFIER_KEY);
  } catch {
    return undefined;
  }

  if (!value) {
    return undefined;
  }

  try {
    const handoff: unknown = JSON.parse(value);
    if (
      typeof handoff === "object" &&
      handoff !== null &&
      "identifier" in handoff &&
      "expiresAt" in handoff &&
      typeof handoff.identifier === "string" &&
      handoff.identifier.length > 0 &&
      typeof handoff.expiresAt === "number" &&
      handoff.expiresAt > nowMs
    ) {
      return handoff.identifier;
    }
  } catch {
    // Ignore malformed tab state and let the user enter an identifier.
  }

  return undefined;
}

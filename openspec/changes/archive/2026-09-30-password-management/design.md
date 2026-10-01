## Context

See [proposal.md](proposal.md) for the motivation and [specs/account-passwords/spec.md](specs/account-passwords/spec.md) for the behavior contract. The Next.js frontend already uses AWS Amplify Auth v6, React Hook Form, Zod, shared form controls, and an app-level alert. Login, signup, and signup confirmation are public routes listed in `UserProvider`; `/auth/*` hides the navbar. The user menu currently offers logout only. A localhost-only test identity has no Cognito password.

The existing Cognito pool accepts email as a username alias, recovers through verified email before verified phone, and requires passwords of at least eight characters. The app client has user-existence error suppression enabled. No pool or server change is needed for these flows.

## Goals / Non-Goals

**Goals:**
- Reuse the frontend's auth forms and helper result pattern while keeping recovery available without a session and password change available only to Cognito users with a session.
- Treat Amplify sign-in results as a state transition: only completed authentication enters the app.
- Keep passwords and reset codes out of URLs, logs, and browser storage.

**Non-Goals:**
- Implement the separate `CONFIRM_SIGN_IN_WITH_NEW_PASSWORD_REQUIRED` temporary-password challenge or other MFA/challenge UIs.
- Add a backend auth endpoint, new dependency, Cognito policy change, or local-test-user password.

## Decisions

### Call Amplify Auth from the existing frontend auth helpers

Add small helpers around `resetPassword`, `confirmResetPassword`, and `updatePassword` in `src/app/auth/helpers.tsx`, following its `AuthResult` convention. The reset helper will surface the `ResetPasswordOutput` next step so the page can handle `CONFIRM_RESET_PASSWORD_WITH_CODE` and `DONE`. A backend proxy would duplicate Cognito's public client flow and require new API and error-handling code without adding a capability.

### Use a two-step public recovery screen

Add a single `/auth/reset-password` page with request-code and confirm-code states. A login link opens the request state. The submitted username or email stays in component state through confirmation; a refresh returns to the request state, where the user can request a new code. A `RESET_PASSWORD` sign-in result opens the request state with the identifier prefilled and waits for the user to send a code. To avoid putting the identifier in the URL, pass only that identifier through tab session storage with a five-minute expiry. Consume and remove it in a mount effect, never during render; on a missing or expired handoff, leave the form's current value alone so a second StrictMode effect cannot erase a valid prefill. Never store passwords or codes. This approach avoids a second route and email addresses in browser history; the trade-off is that refreshing the confirmation step restarts the request UI.

The screen will use the existing centered auth card, `FormTextField`, buttons, and alert pattern. The identifier is editable in the request state and read-only in the code state; a change-account action returns to the request state and clears the code fields. Show neutral delivery instructions and provide resend and back-to-login actions. Use `username`, `one-time-code`, and `new-password` autocomplete values on the corresponding fields. Move focus to the code-step heading or code field after a successful request, and announce errors through the existing alert pattern. Add the route to both the auth-page redirect and public-route lists in `UserProvider`. A separate confirmation route, as used for signup, was considered but would require an identifier in the URL or another handoff mechanism without improving this flow.

### Put signed-in change under a protected account route

Add `/account/change-password` and link to it from `UserDropdown`. This path keeps the navbar and uses the existing default protected-route behavior. The form asks for current, new, and confirmation passwords; it stays on the page after success, clears all password fields, and shows a success alert. Use `current-password` and `new-password` autocomplete values and supply the signed-in username to password managers. Expose resolved auth state and the existing local-test identity state through `UserProvider`; render the form only after auth resolves for a Cognito user. Hide the menu action for the local test identity and redirect its direct visits to `/reviews`. The update helper must also refuse a local-test request before calling Amplify, even if the page guard is bypassed. A dropdown dialog was considered, but a page gives validation and errors room without increasing menu state complexity.

### Branch on the sign-in next step before navigating

The login page currently redirects whenever `handleLogin` returns without throwing, even if `signIn` reports an unfinished step. Use `isSignedIn`/`nextStep.signInStep` from `SignInOutput`: completed sign-in proceeds to `/reviews`; `RESET_PASSWORD` opens the recovery screen; `CONFIRM_SIGN_UP` stays signed out and points to the existing `/auth/confirm-signup` page, where the user can re-enter the identifier; every other incomplete step stays outside protected content and shows an explicit unsupported-step message. The temporary-password challenge requires `confirmSignIn` and is outside this change, so it must not be mistaken for success.

### Normalize recovery errors without exposing account status

For the request and resend steps, show the same neutral acknowledgment for accepted or simulated delivery. Treat `UserNotFoundException`, `NotAuthorizedException`, account-eligibility `InvalidParameterException`, `LimitExceededException`, and `TooManyRequestsException` as neutral results: a distinct throttling response could reveal that a real account was rate limited while an unknown identifier received simulated delivery. The acknowledgment says a code may arrive and recommends waiting before retrying if none does. Map network and other service failures to a retry message without claiming code delivery. For confirmation, distinguish `CodeMismatchException`, `ExpiredCodeException`, and `InvalidPasswordException` with actionable copy; use a generic retry message for other failures. For signed-in `updatePassword`, map `NotAuthorizedException` to incorrect current password or expired session, `InvalidPasswordException` to password-policy guidance, rate limits to retry later, and unauthenticated/session errors to sign in again; other failures get a generic retry message. Never show raw provider messages or log submitted values. Keep the existing eight-character client validation used by signup, while allowing Cognito to enforce its full policy. Disable repeated submission while a request is pending; service throttling remains authoritative.

### Verify without a new test dependency

Keep sign-in-step decisions, recovery error mapping, and handoff expiry in standalone `src/lib/auth/password-decisions.ts` with only relative imports and no JSX, path aliases, or Amplify runtime imports. Pass storage and the current time into the handoff functions so expiry can be tested without a browser. This workspace has Node 24.20.0, which can run the `.mjs` test importing that `.ts` module with `node --test src/lib/auth/password-decisions.test.mjs` from `MediaRankerFrontend`; run `npx tsc --noEmit` for type checking. Use the local browser to check directly reachable routes, form validation, focus, and the local-test guard without sending live Cognito mutations. Full request, resend, confirmation, and signed-in change behavior against Cognito remains gated on explicit approval for a dedicated test account. The repository has no frontend auth-stubbing harness, so browser claims must state which states were actually reached.

## Risks / Trade-offs

- **Refresh during code entry loses in-memory step state** → Return to the request form with the identifier editable; resend a code rather than persisting a code or password.
- **Cognito can simulate code delivery for an unknown account** → Keep request and resend copy neutral and never display raw account-related provider errors or delivery details as proof of an account.
- **Local test auth appears authenticated but has no Cognito password** → Hide the menu entry and guard direct navigation to the account page.
- **Unimplemented Amplify sign-in steps can occur later** → Fail closed on incomplete results and show a clear next action rather than redirecting into the app.
- **A password reset or change does not explicitly revoke sessions on other devices** → This change does not call Cognito global sign-out; account-wide session revocation is a separate security feature.

## Migration Plan

Deploy frontend changes without a data migration or AWS configuration update. Existing login and signup remain available. Rollback removes the new routes/menu entry and restores the prior frontend behavior; Cognito account data is unchanged by deployment.

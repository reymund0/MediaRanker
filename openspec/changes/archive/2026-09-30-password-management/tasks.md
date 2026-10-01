## 1. Signed-out recovery

- [x] 1.1 Add Amplify reset-request, resend, and reset-confirmation helpers with identical neutral responses for account-related and throttling errors and distinct service-retry/code errors; verify pure error-mapping decisions in `password-decisions.test.mjs` without logging codes or passwords.
- [x] 1.2 Add the public two-step `/auth/reset-password` form and a login-page recovery link using existing auth controls; verify directly reachable navigation, validation, focus, and back-to-login states in the local browser. Keep the identifier read-only during code entry and clear sensitive fields when changing it; reserve Cognito request/resend/confirmation outcomes for the approved live test in 4.2.
- [x] 1.3 Register the recovery route in `UserProvider`; verify a signed-out direct visit stays on the page and signed-in Cognito/local-test visits redirect to `/reviews`.

## 2. Signed-in password change

- [x] 2.1 Add the Amplify password-update helper with a local-test guard and safe current-password, policy, rate-limit, session, and generic error mapping, plus the protected `/account/change-password` form; verify typed helper behavior and current/new/confirmation validation locally. Reserve wrong-current-password, success, and field-clearing outcomes for the approved live test in 4.2.
- [x] 2.2 Link the form from `UserDropdown` and expose resolved/local-test auth state; render the form only for resolved Cognito auth. Verify the action is absent for the local test user and a direct local-test visit redirects to `/reviews` without a Cognito password request. Verify the action appears for a Cognito user in the approved live test in 4.2.

## 3. Required reset during sign-in

- [x] 3.1 Branch login on `SignInOutput.isSignedIn` and `nextStep.signInStep`, passing the submitted identifier to recovery only for `RESET_PASSWORD` and pointing `CONFIRM_SIGN_UP` to the existing confirmation page; verify completed, required-reset, signup-confirmation, and other unfinished decisions with Node's built-in test runner.
- [x] 3.2 Consume and clear the identifier-only recovery handoff in a mount effect with a five-minute expiry; verify prefill, consumption, expiry, a second effect pass, and no password or code in URL or browser storage with injected-storage tests.

## 4. Integration checks

- [x] 4.1 Run targeted frontend lint, `npx tsc --noEmit`, and `node --test src/lib/auth/password-decisions.test.mjs` from `MediaRankerFrontend` on the workspace's Node 24.20.0; verify they finish without errors and review the diff for leaked account details, codes, or passwords. Record which browser states were reached and which require live Cognito approval.
- [x] 4.2 Combine the approved dedicated-account checks of request, resend, successful recovery, and signed-in change with automated Amplify-helper simulations for invalid/expired codes and required-reset handoff. Inspect the recovery and login route handlers against the user-visible spec, and record which branches were not exercised end to end against live Cognito.

Live verification on the dedicated test account covered request, resend, successful recovery, wrong-current-password feedback, and successful signed-in change with cleared fields. An invalid-code attempt stayed on the code-entry form, but its transient message was not captured. Signed-in Cognito visits to the recovery route redirected to `/reviews`. A later local frontend session with `NEXT_PUBLIC_ENABLE_LOCAL_TEST_LOGIN=true` verified signed-out direct recovery access, local-test redirects from both password routes, the absence of Change password in the local-test menu, and the Cognito-only menu action; the backend was not running during those route/menu checks. `password-helpers.test.mjs` simulates Cognito responses at the actual helper boundary, covering request, resend, invalid and expired code messages, successful confirmation, and required-reset sign-in handoff. Together with `password-decisions.test.mjs` and inspection of the page handlers, this verifies the remaining code paths without another credential change. Expired-code handling and required-reset navigation were not exercised end to end against live Cognito; the first AWS admin reset attempt was denied for `MediaRankerCli`, and the user chose agent-only testing instead of further manual credential entry.

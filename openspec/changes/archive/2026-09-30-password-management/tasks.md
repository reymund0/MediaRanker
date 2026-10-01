## 1. Signed-out recovery

- [x] 1.1 Add Amplify reset-request, resend, and reset-confirmation helpers with identical neutral responses for account-related and throttling errors and distinct service-retry/code errors; verify pure error-mapping decisions in `password-decisions.test.mjs` without logging codes or passwords.
- [x] 1.2 Add the public two-step `/auth/reset-password` form and a login-page recovery link using existing auth controls; verify directly reachable navigation, validation, focus, and back-to-login states in the local browser. Keep the identifier read-only during code entry and clear sensitive fields when changing it; reserve Cognito request/resend/confirmation outcomes for the approved live test in 4.2.
- [ ] 1.3 Register the recovery route in `UserProvider`; verify a signed-out direct visit stays on the page and signed-in Cognito/local-test visits redirect to `/reviews`.

## 2. Signed-in password change

- [x] 2.1 Add the Amplify password-update helper with a local-test guard and safe current-password, policy, rate-limit, session, and generic error mapping, plus the protected `/account/change-password` form; verify typed helper behavior and current/new/confirmation validation locally. Reserve wrong-current-password, success, and field-clearing outcomes for the approved live test in 4.2.
- [ ] 2.2 Link the form from `UserDropdown` and expose resolved/local-test auth state; render the form only for resolved Cognito auth. Verify the action is absent for the local test user and a direct local-test visit redirects to `/reviews` without a Cognito password request. Verify the action appears for a Cognito user in the approved live test in 4.2.

## 3. Required reset during sign-in

- [x] 3.1 Branch login on `SignInOutput.isSignedIn` and `nextStep.signInStep`, passing the submitted identifier to recovery only for `RESET_PASSWORD` and pointing `CONFIRM_SIGN_UP` to the existing confirmation page; verify completed, required-reset, signup-confirmation, and other unfinished decisions with Node's built-in test runner.
- [x] 3.2 Consume and clear the identifier-only recovery handoff in a mount effect with a five-minute expiry; verify prefill, consumption, expiry, a second effect pass, and no password or code in URL or browser storage with injected-storage tests.

## 4. Integration checks

- [x] 4.1 Run targeted frontend lint, `npx tsc --noEmit`, and `node --test src/lib/auth/password-decisions.test.mjs` from `MediaRankerFrontend` on the workspace's Node 24.20.0; verify they finish without errors and review the diff for leaked account details, codes, or passwords. Record which browser states were reached and which require live Cognito approval.
- [ ] 4.2 After explicit approval for password-changing operations on a dedicated Cognito test account, exercise request, resend, invalid/expired code, successful recovery, signed-in change, and required-reset navigation; verify the user-visible results match the spec.

Live verification on the dedicated test account covered request, resend, successful recovery, wrong-current-password feedback, and successful signed-in change with cleared fields. An invalid-code attempt stayed on the code-entry form, but its transient message was not captured; expired-code behavior was not exercised. The required-reset branch was not exercised because AWS denied `AdminResetUserPassword` to `MediaRankerCli`, and the user chose to skip that check. Signed-in Cognito visits to the recovery route redirected to `/reviews`. The local-test runtime checks in 1.3 and 2.2 remain open.

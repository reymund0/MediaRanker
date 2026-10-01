## Why

Users cannot recover a forgotten password or change a known password in MediaRanker. The login page also treats an unfinished Cognito sign-in as successful, so a required password reset can send the user toward a protected page instead of recovery.

## What Changes

- Add a signed-out password recovery flow from login: request a code using a username or email, enter the code and a new password, resend a code when needed, and return to login after success.
- Add a signed-in password change screen reachable from the user menu for Cognito users.
- Route Cognito's `RESET_PASSWORD` sign-in step into the recovery flow and only navigate to the app when sign-in is complete. Keep other unfinished sign-in steps from being treated as success.
- Use neutral recovery messaging that does not disclose whether an account exists, and show actionable validation and code errors.

## Capabilities

### New Capabilities

- `account-passwords`: Password recovery, signed-in password changes, and required-reset handling during sign-in.

### Modified Capabilities

None.

## Impact

Frontend auth pages and helpers, the auth route guard, and the user menu will change. The flows use the existing AWS Amplify Auth integration and Cognito user pool; no backend endpoint, new dependency, or AWS configuration change is expected.

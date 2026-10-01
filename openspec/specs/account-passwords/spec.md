# Account Passwords Specification

## Purpose

Allow MediaRanker users to recover a forgotten password, change a known password, and complete a required reset during sign-in without being mistaken for an authenticated user.

## Requirements

### Requirement: Signed-out password recovery entry
The system SHALL provide a password recovery entry from the login screen and allow signed-out users to start recovery with a username or email address.

#### Scenario: Open recovery from login
- **WHEN** a signed-out user selects the password recovery link on the login screen
- **THEN** the user can enter a username or email address without being redirected to login

#### Scenario: Signed-in user opens recovery
- **WHEN** a signed-in Cognito or local-test user opens the recovery page
- **THEN** the system redirects the user to `/reviews`

### Requirement: Private recovery request
The system SHALL request a recovery code for the supplied identifier and display an acknowledgment that does not reveal whether the account exists or is eligible for recovery.

#### Scenario: Recovery request accepted
- **WHEN** a user submits a nonempty username or email address and the recovery service accepts the request
- **THEN** the system presents a code-entry step with neutral instructions to check the available recovery channel

#### Scenario: Identifier has no recoverable account
- **WHEN** the recovery service returns a simulated code delivery or an account-related error for an unknown, disabled, or ineligible identifier
- **THEN** the system does not reveal the account's existence or status in its message

#### Scenario: Recovery service unavailable
- **WHEN** a recovery request fails because the service is unavailable
- **THEN** the system presents a retryable error without claiming that a code was delivered

#### Scenario: Recovery request is throttled
- **WHEN** the recovery service limits a request or resend attempt
- **THEN** the system shows the same neutral acknowledgment as for an accepted request, including advice to wait before retrying if no code arrives, without revealing whether the identifier belongs to an account

### Requirement: Complete password recovery
The system SHALL let a user submit the recovery code, a new password, and matching confirmation. The new password SHALL meet the current minimum length of eight characters. Password values SHALL be masked, and passwords and recovery codes SHALL be excluded from URLs, browser storage, and logs.

#### Scenario: Valid code and password
- **WHEN** a user submits a valid code with a matching new password that satisfies the password policy
- **THEN** the system completes the reset, offers a return to login, and does not treat the user as signed in

#### Scenario: Invalid or expired code
- **WHEN** a user submits an invalid or expired recovery code
- **THEN** the system keeps the user in the recovery flow and presents an actionable error

#### Scenario: Invalid new password
- **WHEN** a user submits a new password that is too short or does not match its confirmation
- **THEN** the system identifies the invalid field and does not submit the reset

#### Scenario: Use a different identifier during recovery
- **WHEN** a user chooses to change the identifier after reaching the code-entry step
- **THEN** the system returns to the request step and clears the code and new-password fields before another request

### Requirement: Resend recovery code
The system SHALL let a user request another recovery code for the current identifier and prevent duplicate submissions while a request is pending.

#### Scenario: Resend requested
- **WHEN** a user requests another code from the code-entry step
- **THEN** the system initiates another recovery request and shows neutral confirmation or an actionable retry error

### Requirement: Signed-in password change
The system SHALL provide a password-change action to signed-in Cognito users. It SHALL require the current password, a new password of at least eight characters, and matching confirmation; it SHALL not offer this action to the local test user.

#### Scenario: Change password successfully
- **WHEN** a signed-in Cognito user submits the correct current password and a valid matching new password
- **THEN** the system changes the password, clears the password fields, and shows success without forcing a sign-out

#### Scenario: Current password rejected
- **WHEN** the password-change request rejects the current password
- **THEN** the system keeps the user on the password-change screen and shows an actionable error

#### Scenario: New password rejected by Cognito policy
- **WHEN** Cognito rejects a password that passed the client's minimum-length check
- **THEN** the system keeps the user on the password-change screen and shows password-policy guidance without displaying the raw provider error

#### Scenario: User is not signed in
- **WHEN** a signed-out user attempts to open the password-change screen
- **THEN** the system redirects the user to login

#### Scenario: Local test user is active
- **WHEN** the local development test identity is active
- **THEN** the system does not offer or submit a Cognito password-change action, and a direct visit to the change page redirects to `/reviews`

### Requirement: Required reset during sign-in
The system SHALL only treat a sign-in as successful when authentication is complete. It SHALL route a `RESET_PASSWORD` sign-in step into password recovery using the submitted identifier, and SHALL not navigate to protected content for other unfinished sign-in steps.

#### Scenario: Cognito requires password reset
- **WHEN** a login attempt returns a `RESET_PASSWORD` step
- **THEN** the system opens the recovery request step with the submitted identifier prefilled, waits for the user to request a code, and does not mark the user signed in

#### Scenario: Signup is unfinished
- **WHEN** a login attempt returns a `CONFIRM_SIGN_UP` step
- **THEN** the system keeps the user signed out and directs the user to the existing signup-confirmation page

#### Scenario: Sign-in is complete
- **WHEN** a login attempt completes authentication
- **THEN** the system navigates the user to the authenticated app

#### Scenario: Different unfinished sign-in step
- **WHEN** a login attempt returns an unfinished step other than `RESET_PASSWORD`
- **THEN** the system remains outside protected content and presents a clear next-action message

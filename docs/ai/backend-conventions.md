# Backend Conventions

## ProblemDetails responses
- Every non-OK HTTP response must return RFC 7807 ProblemDetails JSON with `type`, `title`, `status`, `detail`, and optional `instance`/extensions.
- Controllers/services should populate `detail` with the user-facing string; frontend hooks only surface this field while logging the full object.
- Include any correlation metadata (e.g., `errorId`) inside `problemDetails.extensions` for easier troubleshooting.

## Serilog logging
- Structured logging is handled by Serilog; prefer `ILogger` templates so properties stay queryable.
- Domain/validation failures: log at `Warning` with the domain-specific `type` and user context when available.
- Unexpected exceptions: log at `Error` with `errorId`, then include the same identifier in the ProblemDetails payload so clients can reference it when reporting issues.

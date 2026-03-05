# Backend Conventions

## Project Structure (Modular Monolith)

The server is organized into feature modules under `MediaRankerServer/Modules/`.

- **Module Structure**: Each module contains its own Controllers, Services, Models, and Entities.
- **Shared Infrastructure**: `MediaRankerServer/Shared/` contains cross-cutting concerns like `DomainException` and common extensions.
- **Module Registration**: Each module has a `<Name>Module.cs` registration class. Add new modules to `Program.cs` via `builder.Services.Add<Name>Module()`.
- **Inter-Module Communication**: Use MediatR for in-process events instead of direct service injection to maintain decoupling.

## ProblemDetails responses
- Every non-OK HTTP response must return RFC 7807 ProblemDetails JSON with `type`, `title`, `status`, `detail`, and optional `instance`/extensions.
- Controllers/services should populate `detail` with the user-facing string; frontend hooks only surface this field while logging the full object.
- Include any correlation metadata (e.g., `errorId`) inside `problemDetails.extensions` for easier troubleshooting.

## Serilog logging
- Structured logging is handled by Serilog; prefer `ILogger` templates so properties stay queryable.
- Domain/validation failures: log at `Warning` with the domain-specific `type` and user context when available.
- Unexpected exceptions: log at `Error` with `errorId`, then include the same identifier in the ProblemDetails payload so clients can reference it when reporting issues.

## Validation
- Request validation lives in FluentValidation `AbstractValidator<T>` classes (e.g., `TemplateUpsertRequestValidator`).
- Services/controllers resolve `IValidator<T>` via DI and throw `DomainException` with the existing `type` values (e.g., `template_validation_error`) when validation fails so ProblemDetails stays consistent.

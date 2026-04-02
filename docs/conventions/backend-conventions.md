# Backend Conventions

## Project Structure (Modular Monolith)

The server is organized into feature modules under `MediaRankerServer/Modules/`.

- **Module Structure**: Each module contains its own Controllers, Services, Contracts, and Data concerns.
- **Persistence Layout**: Keep module persistence artifacts under `Modules/<Module>/Data/`:
  - `Data/Entities` for EF entities/configurations
  - `Data/Views` for keyless read-model/view entities and view SQL artifacts
  - `Data/Seeds` for module-owned seed SQL
- **Shared Infrastructure**: `MediaRankerServer/Shared/` contains cross-cutting concerns like `DomainException` and common extensions.
- **Module Registration**: Each module has a `<Name>Module.cs` registration class. Add new modules to `Program.cs` via `builder.Services.Add<Name>Module()`.
- **Inter-Module Communication**: Use MediatR for in-process events instead of direct service injection to maintain decoupling.

## Foreign Key + Index Pattern
- Keep DB foreign keys within module-owned tables only.
- Do not create cross-module foreign keys.
- For cross-module references, persist scalar IDs (for example, `MediaId`, `TemplateId`) and create explicit indexes on those columns for query performance.

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
- Keep lightweight validators and mappers colocated with their related request/contract class (same file or same folder) so discovery stays straightforward.
- Services/controllers resolve `IValidator<T>` via DI and throw `DomainException` with the existing `type` values (e.g., `template_validation_error`) when validation fails so ProblemDetails stays consistent.

## Hosted Services (Scheduled Background Jobs)
- Use `IHostedService`/`BackgroundService` for recurring server-side jobs (for example, daily cleanup) instead of controller-triggered execution.
- Keep hosted services orchestration-focused: schedule/timing, scoped dependency resolution, logging, and cancellation handling.
- Resolve scoped dependencies per run via `IServiceScopeFactory`; do not inject scoped services directly into hosted service constructors.
- Keep business/domain logic in module services and event handlers; hosted services should invoke those abstractions rather than duplicate rules.
- Make job behavior configuration-driven with `IOptions<T>` (for example: enabled flag, thresholds, schedule-related settings).
- Wrap each run in exception handling, log start/finish plus success/failure counts, and continue the schedule unless cancellation is requested.

## File Upload Lifecycle (Module + Files Module)
- The upload flow is two-phase and module-driven:
  1. Frontend asks a module endpoint to start an upload.
  2. Module validates request and calls `IFileService.StartUploadAsync(...)` to get `UploadId` + pre-signed upload URL.
  3. Frontend uploads the binary directly to S3 using the pre-signed URL.
  4. Frontend calls a module endpoint to confirm upload completion.
  5. Module validates and calls `IFileService.FinishUploadAsync(...)`, transitioning `FileUploadState` from `Uploading` to `Uploaded`.
  6. Frontend later submits the module save/upsert request with the `uploadId` attached.

- Files in `Uploaded` state are temporary and may be removed by daily cleanup if never copied into module-owned data.
- Each module must copy file metadata it needs by calling `IFileService.MarkUploadCopiedAsync(uploadId, userId, ...)` during its own save flow, then persist the returned `FileDto` data in module-owned entities.
- If a module does not copy upload data out of the Files module, it risks losing the file reference during cleanup.
- The Files module owns upload state tracking (`Uploading`, `Uploaded`, `Copied`, `Deleted`); feature modules own business validation and when upload IDs become part of domain models.

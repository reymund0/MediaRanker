---
trigger: always_on
---

# MediaRanker — AI Editor Context

This file provides repository context for AI coding assistants (Cursor, Copilot, Windsurf, ChatGPT, etc.).
It documents the stack (with exact versions), repo layout, project goals, and key conventions so edits stay consistent.

---

## Project Overview

**MediaRanker** is a personal web application for reviewing and ranking media.

Product intent:
- **User-defined review templates** (template JSON stored in the DB).
- Reviews use a **1–10 numeric rating** (UI may display as **0.5-star increments** on a 1–5 star component).
- Media entities are **initially user-scoped** with internal IDs (external media provider IDs may be added later).
- Rankings are **per-user and per-template**, with planned views such as:
  - Top 10 by rating
  - Top 10 by relative ranking

Current implementation status:
- **Auth plumbing only** (no domain features implemented yet).
- Authorization model: **any authenticated user** (no roles/scopes yet).

---

## Repository Layout (top-level)

- `.github/`
  - `workflows/` (CI workflows)
- `MediaRankerFrontend/` — Next.js + React frontend
  - `src/app/` (App Router)
    - `auth/`
    - `home/`
    - `test/`
  - `src/lib/`
    - `components/`
    - `hooks/`
    - `providers/`
- `MediaRankerServer/` — ASP.NET Core Web API (.NET 9)
  - `Controllers/`
  - `Services/`
  - `Data/Entities/`
  - `Migrations/`
  - `Properties/`
- Docker/compose:
  - Server project references `docker-compose.dcproj` and uses a compose file with `mediarankerserver` + `postgres`.

Avoid editing build artifacts:
- `MediaRankerFrontend/.next/`
- `**/bin/`
- `**/obj/`

---

## Tech Stack and Versions (ground truth)

### Backend — MediaRankerServer (.NET Web API)
- **.NET / TargetFramework**: `net9.0`
- **Nullable**: enabled
- **ImplicitUsings**: enabled
- **User Secrets**: enabled (UserSecretsId present)

NuGet packages:
- `Microsoft.AspNetCore.Authentication.JwtBearer` **9.0.13**
- `Microsoft.AspNetCore.OpenApi` **9.0.10**
- `Microsoft.EntityFrameworkCore` **9.0.11**
- `Microsoft.EntityFrameworkCore.Design` **9.0.11** (PrivateAssets=all)
- `Microsoft.EntityFrameworkCore.Tools` **9.0.11** (PrivateAssets=all)
- `Npgsql.EntityFrameworkCore.PostgreSQL` **9.0.4**
- `EFCore.NamingConventions` **9.0.0**
- `Scalar.AspNetCore` **2.12.38**
- `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` **1.23.0**

### Frontend — MediaRankerFrontend (Next.js App)
- **Next.js**: **16.0.3**
- **React**: **19.2.0**
- **React DOM**: **19.2.0**
- **TypeScript**: **^5.9.3**
- **ESLint**: **^9.39.2**
- **Prettier**: **^3.8.1**

Core libraries:
- **AWS Amplify**: **^6.16.0**
- **@tanstack/react-query**: **^5.90.21**
- **react-hook-form**: **^7.71.1**
- **@hookform/resolvers**: **^5.2.2**
- **zod**: **^4.3.6**
- **MUI (@mui/material)**: **^7.3.7**
- **Emotion**:
  - `@emotion/react` **^11.14.0**
  - `@emotion/styled` **^11.14.1**

Frontend scripts:
- `dev`: `next dev`
- `build`: `next build`
- `start`: `next start`
- `lint`: `eslint`
- `format`: `prettier --write .`

---

## Local Dev (how you actually run it)

Primary dev flow:
- Frontend: **http://localhost:3000** (`next dev`)
- Backend: run via **`dotnet run`** (preferred dev mode)

Docker Compose is available primarily for **Postgres** (and optionally for the server container), but day-to-day backend dev is `dotnet run`.

---

## Docker Compose (local persistence)

Services:
- **postgres**:
  - Image: `postgres:16`
  - DB: `mediarank`
  - User: `admin`
  - Password: `password`
  - Port mapping: `5432:5432`
  - Volume: `pgdata:/var/lib/postgresql/data`
  - Healthcheck: `pg_isready -U admin -d mediarank`
- **mediarankerserver** (optional containerized server):
  - Builds from `MediaRankerServer/Dockerfile`
  - Port mapping: `8080:8080` (HTTP), `8081:8081` (HTTPS if enabled)
  - Env var connection string:
    - `ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=mediarank;Username=admin;Password=password;Timeout=15;Command Timeout=30`
  - Depends on postgres healthcheck

AI editor guidance:
- Do not hardcode credentials outside docker-compose/sample configs.
- If adding new services, keep them optional and documented.

---

## Backend Runtime Behavior (Program.cs current behavior)

### Default auth requirement
All controllers require authentication by default:
- `options.Filters.Add(new AuthorizeFilter());`

Meaning:
- New endpoints should be authenticated unless explicitly marked `[AllowAnonymous]`.

### CORS
CORS policy name: **AllowFrontend**
- Allowed origins are read from **`appsettings.Development.json`**:
  - `Cors:AllowedOrigins` (string array)
- Policy:
  - `.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader()`
- Middleware: `app.UseCors("AllowFrontend")`

Important:
- During local dev, allowed origins must include: `http://localhost:3000`.

### OpenAPI + Scalar
Development only:
- `app.MapOpenApi();`
- `app.MapScalarApiReference();`
- `/` redirects to `/scalar/v1`

Non-development:
- `app.UseHttpsRedirection();`

### Authentication: AWS Cognito JWT Bearer
Frontend → backend token usage:
- The frontend sends the **Cognito Access Token**:
  - `Authorization: Bearer <ACCESS_TOKEN>`

Backend validation:
- Uses `JwtBearerDefaults.AuthenticationScheme`
- Authority format:
  - `https://cognito-idp.{AWS:Region}.amazonaws.com/{AWS:CognitoUserPoolId}`
- Token validation:
  - Issuer: validated against `authority`
  - Audience: validated against `AWS:CognitoClientId` (client id)
  - Lifetime: validated
  - Signing keys: validated
  - Clock skew: 5 minutes
- `RequireHttpsMetadata = true`

Authorization model:
- **Any authenticated user** (no roles/scopes yet).

---

## Persistence / EF Core Conventions

- DB: PostgreSQL (via EF Core + Npgsql)
- DbContext: `PostgreSQLContext` using connection string `DefaultConnection`
- Naming convention: `UseSnakeCaseNamingConvention()` is enabled

AI editor guidance:
- Keep schema changes migration-driven.
- Prefer snake_case in DB; keep C# names idiomatic.

---

## AI Editing Guardrails

- Avoid editing build artifacts: `MediaRankerFrontend/.next`, `**/bin`, `**/obj`.
- Avoid introducing large new architectural frameworks unless requested.
- Keep auth centralized:
  - Frontend obtains Cognito tokens via Amplify.
  - Backend validates JWTs via JwtBearer + Cognito authority.
- Default endpoints should remain authenticated. Use `[AllowAnonymous]` intentionally.

---

## Current Frontend Conventions (keep consistent)

### Theme and styling
- Theme is centralized in `MediaRankerFrontend/src/app/theme.ts` and currently uses `palette.mode = "dark"`.
- Primary accent is anchored on `#7C3AED`; avoid hardcoded one-off color values in components when a theme token exists.
- MUI link integration is centralized via `LinkBehavior` (`MuiLink.component` + `MuiButtonBase.LinkComponent`).

### Global layout and navigation
- App composition in `src/app/layout.tsx` wraps content in this order:
  - `ThemeProvider` → `CssBaseline` → `QueryClientProvider` → `AlertProvider` → `UserProvider` → `BaseLayout`
- Navbar visibility is controlled in `src/lib/components/layout/base-layout.tsx`:
  - Hide navbar on `/auth/*` routes.
  - Show navbar on non-auth routes.
- Current top-level nav links in `AppNavbar` are:
  - `/home`
  - `/test`
- User menu includes:
  - `/settings` route (currently a placeholder page)
  - Logout action

### Alert architecture (standardized)
- Use `useAlert()` from `src/lib/components/feedback/alert/alert-provider.tsx` for page/component alerts.
- Alert rendering is centralized at app level via `AlertProvider` with a single active alert at a time.
- `BaseAlert` behavior:
  - Default auto-dismiss by severity: success `3000ms`, info/warning `5000ms`, error `7000ms`
  - `persist` disables auto-dismiss
  - `autoHideDurationMs` overrides defaults
- Prefer provider-based alerts over local inline alert state for new UI work.

### Existing pages already aligned with provider alerts
- `auth/confirm-signup`
- `auth/login`
- `auth/signup`
- `home`
- `test`

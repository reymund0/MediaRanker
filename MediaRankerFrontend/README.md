# MediaRankerFrontend

Next.js frontend for MediaRanker

## Frontend Highlights

- Reusable component library with Base and Form variants for consistent UX and integration with `react-hook-form` (e.g. `BaseTextField`, `FormTextField`, `BaseFileUpload`, `FormFileUpload`).
- App-wide provider composition for theming, authentication/session state, alerts, and TanStack Query setup.
- Typed API hooks using TanStack Query with consistent error handling using ProblemDetails.
- Card based reviews page grouped by media type with guided creation, detailed views, and inline editing.
- Template management with drag and drop field reordering.
- Media entry management with direct to s3 cover image upload.
- Centralized theming with shared MUI tokens and component level overrides.

## Libraries

| Library | Used For | Why I chose this? |
|---|---|---|
| **React 19 / Next.js 16** | React framework — App Router, SSR/SSG, file-based routing | A Familiar React framework with built in routing and app structure that let me move quickly |
| **MUI (Material UI) 7** | Component library — buttons, text fields, dialogs, layout, theming | To provide a component library with a consistent look and feel so I don't have to build everything from scratch |
| **MUI X Data Grid** | Feature-rich data grid for list views (templates, media) | To support CRUD heavy management screens with built-in sorting, filtering, and pagination |
| **MUI X Date Pickers** | Date picker components for date input fields | Consistent UX with the MUI library so I didn't need to maintain my own |
| **TanStack React Query** | Server-state management — caching, refetching, mutation lifecycle | A newer alternative to Apollo that I wanted to try because it looked developer friendly |
| **react-hook-form** | Form state management | Widely used library that integrates well with MUI and compatible with Zod |
| **zod** | Schema-based form validation (integrated via `@hookform/resolvers`) | One of the most widely adopted validation libraries with a functional API that resembles FluentValidation |
| **aws-amplify** | Cognito authentication flows — sign-up, login, token management | Industry standard for AWS Cognito integration |
| **dnd-kit** | Drag-and-drop interactions for sortable lists (e.g. template field ordering) | Out of the box animations and functionality so I didn't need to implement this myself |
| **date-fns** | Date formatting and timezone utilities | To provide consistent date handling across the application |

## Configuration

Copy `.env.example` to `.env.local` and fill in the required values.

`.env.local` is gitignored — never commit credentials.

## App Routes

| Route | Description |
|---|---|
| `/auth/*` | Sign-up, login, confirm-signup |
| `/home` | Landing page (placeholder, currently only used to sign out) |
| `/templates` | Template management — CRUD via DataGrid |
| `/media` | Media entry management — CRUD with cover image upload via DataGrid |
| `/reviews` | Review management grouped by media type, including creation, detailed view, editing, and deletion |
| `/test` | Development-only test page |

## Shared Component Library (`src/lib/components`)

Reusable components follow a **Base/Form** pattern:
- **Base** components wrap MUI primitives with app-specific defaults and styling.
- **Form** variants integrate with `react-hook-form` for controlled form usage.

Organized to match the MUI documentation structure for easier maintenance and discovery.

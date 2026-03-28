# MediaRankerFrontend

Next.js frontend for MediaRanker

## Libraries

| Library | Used For | Why I chose this? |
|---|---|---|
| **Next.js 16** | React framework — App Router, SSR/SSG, file-based routing | Used at my prior company and enjoyed the developer experience |
| **React 19** | UI library | My most familiar Web Framework|
| **MUI (Material UI) 7** | Component library — buttons, text fields, dialogs, layout, theming | To provide a component library with a consistent look and feel so I don't have to build everything from scratch |
| **MUI X Data Grid** | Feature-rich data grid for list views (templates, media, reviews) | |
| **MUI X Date Pickers** | Date picker components for date input fields | |
| **TanStack React Query** | Server-state management — caching, refetching, mutation lifecycle | A newer alternative to Apollo that I wanted to try because it looked developer friendly |
| **react-hook-form** | Form state management | Widely used library that integrates well with MUI |
| **zod** | Schema-based form validation (integrated via `@hookform/resolvers`) | One of the most widely used validation libraries that I hadn't used before |
| **aws-amplify** | Cognito authentication flows — sign-up, login, token management | |
| **dnd-kit** | Drag-and-drop interactions for sortable lists (e.g. template field ordering) | Out of the box animations and functionality so I didn't need to write this myself |
| **date-fns** | Date formatting and timezone utilities | Wanted to simplify server-client date handling and the library looked easy to use |

## Configuration

Copy `.env.example` to `.env.local` and fill in the required values.

`.env.local` is gitignored — never commit credentials.

## App Routes

| Route | Description |
|---|---|
| `/auth/*` | Sign-up, login, confirm-signup (navbar hidden) |
| `/home` | Landing page (placeholder, currently only used to sign out) |
| `/templates` | Template management — CRUD via DataGrid |
| `/media` | Media entry management — CRUD with cover image upload via DataGrid |
| `/reviews` | Review creation and editing |
| `/test` | Development-only test page |

## Shared Component Library (`src/lib/components`)

Reusable components follow a **Base/Form** pattern:
- **Base** components wrap MUI primitives with app-specific defaults and styling.
- **Form** variants integrate with `react-hook-form` for controlled form usage.

Organized to match the MUI documentation structure for easier maintenance and discovery.

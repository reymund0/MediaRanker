# Frontend Conventions Reference

This document contains non-always-on frontend details for MediaRanker.

## Theme and Styling

- Theme is centralized in `MediaRankerFrontend/src/app/theme.ts`.
- Current mode is dark.
- Prefer theme tokens over one-off hardcoded values.
- **Desktop scope**: The application is designed primarily for desktop. The approved automatic-cover-art change includes narrow-screen checks for media cover controls, review cards, and Credits; keep those adjustments local to the affected flows.
- **Grid/Layout Props**: Avoid broad responsive redesigns. Use breakpoints only where an approved flow requires them; media controls stack on narrow screens and the grid scrolls horizontally instead of compressing its columns.

## Custom Component Library Pattern

- Custom UI components under `MediaRankerFrontend/src/lib/components` are MUI-based wrappers/extensions.
- For app-wide theming or reusable behavior, create/extend a **Base** component first.
  - Example naming: `BaseAlert`, `BaseButton`, `BaseTextField`.
- When controlled/form-specific behavior is needed, create a **Form** variant on top of the base component.
  - Example naming: `FormTextField`, `FormSelect`.
- Keep naming and folder organization aligned with MUI docs conventions where practical.
- Prefer evolving shared base/form components over adding one-off inline MUI usage for repeated patterns.
- For async autocomplete/search scenarios, keep server filtering in the caller via controlled search input and pass results into the base component. The base autocomplete should not apply client-side filtering when options already come from the server.

## Layout and Navigation

- App composition in `src/app/layout.tsx`:
  - `AppRouterCacheProvider` -> `ThemeProvider` -> `CssBaseline` -> `QueryClientProvider` -> `AlertProvider` -> `UserProvider` -> `BaseLayout`
  - Keep the installed MUI Next.js cache provider around the theme to collect streamed server styles consistently during hydration.
- Navbar visibility:
  - Hide on `/auth/*`
  - Show on non-auth routes
- Current top-level nav links:
  - `/media`
  - `/templates`
  - `/reviews`
  - `/credits` (TMDB branding/disclaimer and IGDB attribution)
- User menu includes:
  - Change password for signed-in Cognito users
  - logout action

## Password management

- `/auth/reset-password` is public for signed-out recovery. A required-reset sign-in step opens its request form with only the submitted identifier handed off through short-lived session storage.
- `/account/change-password` is available only after Cognito authentication resolves. The local test user has no Cognito password: hide the menu action and redirect a direct visit to `/reviews`.
- Keep passwords and recovery codes out of URLs, browser storage, and logs. Show neutral recovery-request responses for account-related failures, and map Cognito password errors to safe, actionable messages instead of displaying raw provider text.

## Alerts

- Use `useAlert()` from `src/lib/components/feedback/alert/alert-provider.tsx`.
- Alert rendering is app-level and single-active-alert.
- `BaseAlert` defaults:
  - success: `3000ms`
  - info/warning: `5000ms`
  - error: `7000ms`
- `persist` disables auto-dismiss.
- `autoHideDurationMs` overrides defaults.

## API error handling

- `httpRequest` parses non-OK responses as RFC 7807 ProblemDetails, logs the full object (plus route/method/body), and throws `ProblemDetailsError`.
- Callers should rely on `error.message` for user-friendly text and avoid re-parsing the payload.

## API hooks

- `useQuery<TResponse>` is GET-only and should be used for read scenarios.
- `useMutation<TRequest, TResponse>` is for write scenarios (`POST`/`PUT`/`DELETE`) and supports dynamic route builders (`route: (data) => string`).
- `usePagedQuery<T>` is for GET endpoints that return `PageResult<T>` and accept `PageRequest` query params. Use it for paged/searchable list reads instead of manually assembling paging URLs at each callsite.
- For MUI DataGrid tables backed by `PageResult<T>`, use `usePaginatedDatagrid` to keep server pagination, sorting, and filtering state mapped to `PageRequest`.
- When a hook constructs a request URL internally, keep the React Query key aligned with the generated route/query params so cached data cannot drift from the actual request.
- When `usePagedQuery` disables fetching because search input is below `minSearchChars`, callers should expect empty items rather than stale cached results.
- Keep request/response contracts explicit at hook callsites to preserve strong typing for mutation data and callbacks.
- `usePendingCoverRefresh` polls pending displayed artwork every two seconds for at most 30 seconds per active view; hidden, terminal, and unmounted views stop polling.
- Review mutations cancel the exact in-flight review query before reconciling both the query cache and local card state, so an older artwork refresh cannot overwrite a successful save/delete.

## Automatic cover display

- Use the shared `CoverImage` for provider images: lazy loading, descriptive alt text, and an accessible placeholder after failure. A changed URL resets the failed-image state.
- Treat nullable cover URLs and terminal cover statuses as placeholders. Manual media creation/editing does not request file uploads.
- The local development test login requires explicit frontend/backend opt-in and loopback access; startup and security boundaries are documented in `dev-commands.md`.

## Dialog and form pattern

- Use `BaseDialog` for non-form confirmation flows (e.g., delete confirmations).
- Use `FormDialog<T>` for modal forms that need `react-hook-form` context and built-in submit state handling.
- `FormDialog` confirm state should remain tied to form validity/dirty state to prevent accidental empty submissions.

## Sortable form arrays

- For drag-and-drop ordering in form-managed arrays, use `FormDnDList` (`react-hook-form` + `useFieldArray` + `dnd-kit`).
- Persist ordering using array index mapped to backend `position` on submit.

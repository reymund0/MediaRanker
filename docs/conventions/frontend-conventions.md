# Frontend Conventions Reference

This document contains non-always-on frontend details for MediaRanker.

## Theme and Styling

- Theme is centralized in `MediaRankerFrontend/src/app/theme.ts`.
- Current mode is dark. Amethyst uses Bricolage Grotesque for display headings, Geist for body/controls, and Geist Mono for numeric scores/ranks/counts. Load fonts through `next/font/google`; keep colors and shared control styling in `theme.ts`.
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
- Library (`/reviews`), Catalog (`/media`), and Templates (`/templates`) are the primary navigation. Credits (`/credits`) is available from the account menu and attribution footer.
- The account menu shows the signed-in name, Cognito-only Change password, and Sign out.
- Ctrl+K focuses Catalog search.
- Use `PageContainer` for the shared 1280px maximum width including 40px desktop gutters; `PageCard` remains a compatibility wrapper.

## Password management

- `/auth/reset-password` is public for signed-out recovery. A required-reset sign-in step opens its request form with only the submitted identifier handed off through short-lived session storage.
- `/account/change-password` is available only after Cognito authentication resolves. The local test user has no Cognito password: hide the menu action and redirect a direct visit to `/reviews`.
- Keep passwords and recovery codes out of URLs, browser storage, and logs. Show neutral recovery-request responses for account-related failures, and map Cognito password errors to safe, actionable messages instead of displaying raw provider text.

## Alerts

- Use `useAlert()` from `src/lib/components/feedback/alert/alert-provider.tsx`.
- App-level alerts render as bottom-right toasts. Preserve the existing single-active-alert behavior and durations.
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
- Review mutations cancel the exact in-flight review query before reconciling its cache. `ReviewExperienceProvider` coordinates creation and detail/edit drawers; drawers read the current cached review. Keep cover polling keys based on visible IDs so status updates do not restart the bounded window. Library polling pauses while the modal review drawer owns refresh.

## Automatic cover display

- Use the shared `CoverImage` for provider images: lazy loading, descriptive alt text, and an accessible placeholder after failure. A changed URL resets the failed-image state.
- `CoverTile` wraps `CoverImage` with pending and monogram fallbacks. Use `showTitle={false}` for compact thumbnails. `AuthShell` uses the anonymous ready-cover showcase and a typographic fallback when empty or unavailable.
- Treat nullable cover URLs and terminal cover statuses as placeholders. Manual media creation/editing does not request file uploads.
- The local development test login requires explicit frontend/backend opt-in and loopback access; startup and security boundaries are documented in `dev-commands.md`.

## Dialog and form pattern

- Use nullable `BaseScoreInput`/`FormScoreInput` for 1–10 scores and `ScoreBar` for read-only display. New reviews begin unrated; overall previews match server midpoint-to-even rounding.
- Creation and drawer edit share `ReviewWritingFields`: ordered two-column scores, a two-row multiline display headline, and full-width Notes starting at eight rows with no maximum and a word count. Keep action footers outside the scrolling form body.
- Use `BaseDialog` for non-form confirmation flows (e.g., delete confirmations).
- Use `FormDialog<T>` for modal forms that need `react-hook-form` context and built-in submit state handling.
- `FormDialog` confirm state should remain tied to form validity/dirty state to prevent accidental empty submissions.

## Sortable form arrays

- For drag-and-drop ordering in form-managed arrays, use `FormDnDList` (`react-hook-form` + `useFieldArray` + `dnd-kit`).
- Persist ordering using array index mapped to backend `position` on submit.

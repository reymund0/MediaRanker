# Frontend Conventions Reference

This document contains non-always-on frontend details for MediaRanker.

## Theme and Styling

- Theme is centralized in `MediaRankerFrontend/src/app/theme.ts`.
- Current mode is dark.
- Prefer theme tokens over one-off hardcoded values.
- **Desktop Only**: The application is **not** mobile-friendly and is not intended for multiple screen sizes.
- **Grid/Layout Props**: Do not use multiple breakpoints (e.g., `xs`, `md`, `lg`) in MUI `Grid` or Box components. Always default to `xs` if a breakpoint property is required by the component, as the layout does not need to respond to screen size changes.

## Custom Component Library Pattern

- Custom UI components under `MediaRankerFrontend/src/lib/components` are MUI-based wrappers/extensions.
- For app-wide theming or reusable behavior, create/extend a **Base** component first.
  - Example naming: `BaseAlert`, `BaseButton`, `BaseTextField`.
- When controlled/form-specific behavior is needed, create a **Form** variant on top of the base component.
  - Example naming: `FormTextField`, `FormSelect`.
- Keep naming and folder organization aligned with MUI docs conventions where practical.
- Prefer evolving shared base/form components over adding one-off inline MUI usage for repeated patterns.

## Layout and Navigation

- App composition in `src/app/layout.tsx`:
  - `ThemeProvider` -> `CssBaseline` -> `QueryClientProvider` -> `AlertProvider` -> `UserProvider` -> `BaseLayout`
- Navbar visibility:
  - Hide on `/auth/*`
  - Show on non-auth routes
- Current top-level nav links:
  - `/media`
  - `/templates`
  - `/reviews`
- User menu includes:
  - logout action

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
- Keep request/response contracts explicit at hook callsites to preserve strong typing for mutation data and callbacks.

## Dialog and form pattern

- Use `BaseDialog` for non-form confirmation flows (e.g., delete confirmations).
- Use `FormDialog<T>` for modal forms that need `react-hook-form` context and built-in submit state handling.
- `FormDialog` confirm state should remain tied to form validity/dirty state to prevent accidental empty submissions.

## Sortable form arrays

- For drag-and-drop ordering in form-managed arrays, use `FormDnDList` (`react-hook-form` + `useFieldArray` + `dnd-kit`).
- Persist ordering using array index mapped to backend `position` on submit.

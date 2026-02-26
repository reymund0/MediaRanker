# Frontend Conventions Reference

This document contains non-always-on frontend details for MediaRanker.

## Theme and Styling

- Theme is centralized in `MediaRankerFrontend/src/app/theme.ts`.
- Current mode is dark.
- Prefer theme tokens over one-off hardcoded values.

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
  - `/home`
  - `/test`
- User menu includes:
  - `/settings` placeholder route
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

- `use-mutation.ts` parses non-OK responses as RFC 7807 ProblemDetails, logs the full object (plus route/method/body), and throws `Error` with only the `detail` field for UI display.
- Callers should rely on `error.message` for user-friendly text and avoid re-parsing the payload.

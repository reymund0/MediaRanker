# Frontend Conventions Reference

This document contains non-always-on frontend details for MediaRanker.

## Theme and Styling

- Theme is centralized in `MediaRankerFrontend/src/app/theme.ts`.
- Current mode is dark.
- Prefer theme tokens over one-off hardcoded values.

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

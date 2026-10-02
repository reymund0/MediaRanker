## Handoff

This change is designed by Claude and implemented by Codex.

| Item | Value |
| --- | --- |
| Design reference (visual source of truth) | [`design-reference/`](design-reference/README.md) in this change: open `index.html`. Twelve standalone HTML screens and states, 1440px PNG screenshots, `tokens.css`, and a README mapping each screen to its spec requirement |
| Original design canvas | https://claude.ai/artifact/XEjXsYZehjUZb8vBbxHNUj — private to the owner and not needed; `design-reference/` is a complete export of it |
| Designing Claude Code session | `eb63cc15-fbd7-4347-96a4-cbafc113176e` |
| Resume that session for design review | From the repo root `G:\Development\dotnet\MediaRanker`: `claude --resume eb63cc15-fbd7-4347-96a4-cbafc113176e` (interactive) or `claude --resume eb63cc15-fbd7-4347-96a4-cbafc113176e -p "<question or review request>"` (one-shot) |

Use the resumed session to ask design questions, settle ambiguities and request a visual review of implemented screens against `design-reference/`. That session holds the full walkthrough (screens, modals, bugs found) and the reasoning behind each decision. It does not authorize scope beyond this change.

Read `design-reference/README.md` before building any screen. It covers how to use the files (specs win over pixels, rebuild through the theme, icon mapping) and lists states that aren't drawn.

Local environment used for the walkthrough: Postgres in Docker, a migrated clone database `mediarank_ui` (the owner's `mediarank` database was one migration behind and was not modified), backend on `:5157` with local test auth, frontend on `:3000`. See `docs/conventions/dev-commands.md`. Do not migrate or reset `mediarank` without the owner's approval.

## Context

See [proposal.md](proposal.md) for the motivation and walkthrough findings, and `specs/*/spec.md` for behavior. The frontend is Next.js 16 / React 19 with MUI 7, MUI X DataGrid, React Hook Form + Zod, TanStack Query and dnd-kit. Theme lives in `src/app/theme.ts`. Shared components follow the Base/Form wrapper pattern under `src/lib/components`. The backend is an ASP.NET Core modular monolith. All controllers require authentication today, and there is no rate limiting or `IMemoryCache` registration.

Facts this design depends on:

- `Review.OverallScore` is a `short` computed as `Math.Round(average)` with .NET's default banker's rounding. Field values are integers 1–10 (the old star UI doubled half-stars).
- `ReviewService.GetReviewsByMediaTypeAsync` orders by `OverallScore` ascending.
- Title search uses a contains `ILIKE` pattern via `PagingValidator` and the module query builders, with no relevance ordering.
- Cover presentation is centralized in `ArtworkPresentation`: `ready` only when the outcome is `Ready` and not expired.
- Catalog counts: about 228k video games, 56k movies, 1.27M TV entries. Books, albums and concerts have none.

## Goals / Non-Goals

**Goals:**
- Match `design-reference/` visually through MUI theme tokens and shared components rather than one-off styles, keeping the Base/Form pattern.
- Make reviewing fast: find a title, score it, see where it ranks.
- Fix every walkthrough bug listed in the proposal.
- Keep the one anonymous endpoint minimal, cached and free of user data.

**Non-Goals:**
- Phone layouts or responsive redesign beyond what exists (owner decision).
- A dedicated accessibility pass (owner decision). Do not remove existing semantics, labels or focus handling.
- Decimal overall scores, which would need a schema change. See Open Questions.
- Template usage counts, which are not in `TemplateDto`. The designs show no usage count.
- Changes to cover resolution, provider calls or polling (owned by `automatic-cover-art`).
- New npm or NuGet packages.

## Decisions

### Implement Amethyst through the MUI theme

Put the palette, shape, typography and component overrides (Button, Dialog, Menu, TextField/OutlinedInput, Chip, Alert, Card, Tooltip, DataGrid where kept) in `theme.ts`. Add `danger` usage through MUI's `error` palette with `#C93B40` fill / `#F06B6F` text. Load fonts with `next/font/google` in `layout.tsx` and pass the CSS variables into the theme's `fontFamily`. Add a numeric style, for example a `score` typography variant on Geist Mono, for scores, ranks and counts. Add a `PageContainer` replacing `PageCard` with one max width (1280px content, 40px gutters) for every non-auth page.

Tokens (`design-reference/tokens.css` and `12-ui-kit.html`): ground `#0E0D12`, surface `#16151C`, raised `#1F1D27`, line `#2C2A36`, strong line `#3B3847`, text `#F2F0F7`, secondary text `#D3CFDE`, muted `#A29EB1`, placeholder `#7D798C`, accent fill `#8B5CF6` (hover `#9D74F7`) with white text, accent text and highlights `#A78BFA` (link hover `#C4B5FD`), accent tint `#231A3D`, accent border `#4B3585`, danger fill `#C93B40`, danger text `#F06B6F`, success `#5FD08F`. Do not use `#7C3AED` (the current theme primary) for text on the dark ground; it is below 4.5:1 there. Radii: 10px controls, 14–16px panels, 999px chips.

### Replace in-card review states with a drawer and a dialog

Delete the expand-in-place card state machine (`review-card*.tsx`). The library grid shows posters only. Selecting one opens a right-side MUI `Drawer` (600px) holding detail; Edit switches the drawer body to the edit form. "New review" (header button, empty states, catalog "Review" action) opens one `Dialog` with two steps held in a single component, so step 2 keeps the chosen title and "Change" returns to step 1 with the query intact. A catalog "Review" action opens the dialog at step 2 with that title preselected. Reuse the existing review mutations and the cache reconciliation in `review-row.tsx` (cancel the exact query, then update cache and local state).

### Score with whole numbers, unrated by default

Replace `BaseStarRating` with a `BaseScoreInput` (ten segmented buttons, value 1–10 or null) and a `FormScoreInput`, plus a read-only `ScoreBar`. New review fields start null and the Zod schema requires every field before save. The dialog previews the overall score with the server's formula, including banker's rounding (`Math.Round` to even), so the preview matches what is saved. Display `overallScore` as a whole number.

### Rank best-first on the server

Change `GetReviewsByMediaTypeAsync` to `OrderByDescending(OverallScore)` then `ThenByDescending(UpdatedAt)`, then `Id`, for a stable order. Rank is the 1-based position in that list. Ties get sequential ranks ordered by recency. Library sort options other than "Highest score" sort client-side. The "Latest review" hero is the review with the greatest `UpdatedAt` across loaded types.

### Order title search by relevance

When `SearchTerm` is present, order catalog and unreviewed-media search results by exact case-insensitive match, then prefix match, then title length, then title, before paging. Keep the contains filter. Apply it in `MediaQueryBuilder` and `UnreviewedMediaQueryBuilder`. When no search term is present, keep the existing sort behavior. Measure a short-term query (for example "the") against the TV type on the local clone before shipping. If it regresses badly, fall back to prefix-then-title and record the measurement.

### Catalog without the DataGrid filter UI

Replace the MUI DataGrid on `/media` with a custom list (header row, rows, pagination footer) driven by `usePagedQuery`, because the grid's filter popover and virtual scroller caused the search and scroll problems. Search is a debounced (about 300ms) input bound to `searchField: "title"`. Type chips switch `mediaType` and reset to page 1. Chips for types with zero titles are disabled. Show per-type counts only if `includeTotalCount` stays fast on TV, otherwise omit counts. Per-row review status comes from the user's already-cached `reviews/byMediaType` list matched by `mediaId`, so there is no new cross-module query. Edit/delete move into a row `Menu`. "Add a title" opens the existing media dialog, restyled. Show years in lists and the full date only in the edit dialog.

### Templates as cards with a side editor

Replace the DataGrid with cards and a sticky editor panel on the same page. "New template" and "Duplicate" open the editor with an unsaved draft held only in editor state; the template list is never mutated, which fixes the blank-row bug. Duplicate copies name (plus " (copy)"), media type, description and field names without IDs. Built-in templates show a "Built-in" badge and explain that they are read-only. The editor title reads "New template" or "Edit template" to match the action. Keep `FormDnDList` for ordering.

### Anonymous cover showcase

Add `GET /api/media/showcase`, marked `[AllowAnonymous]`, in `MediaController` backed by a Media service method:

- Response: up to 20 `{ title, coverImageUrl }` items. No IDs, types, dates or user data.
- Source: media of any type whose cover presents as `ready` through `ArtworkPresentation`. Never derived from reviews. Never registers artwork demand or calls a provider.
- Selection: deterministic per UTC date, for example ordering eligible rows by a hash of `(media id, date)` and taking 20, so every visitor sees the same set that day.
- Cache: register `AddMemoryCache` and cache the day's result keyed by UTC date. A miss queries once; concurrent misses may query more than once (acceptable).
- Empty or failed result: the frontend renders typographic fallback tiles; the endpoint never fails the auth page.

The auth pages call it with the existing `useQuery` without gating `enabled` on `userId`. Signed out, `sessionToken` is undefined and `httpRequest` omits the Authorization header. Use a long `staleTime` because the set changes daily.

### Visual review loop

After each task group, take screenshots of the affected screens at 1440×900 on the local app and ask the designing session (Handoff) to review them against `design-reference/`. Treat its notes as review feedback, not new scope.

## Risks / Trade-offs

- **First anonymous endpoint** → Returns only public provider image URLs and titles, cached per day, no user data, no provider calls. Covered by an integration test asserting 200 without auth and the exact response shape.
- **Relevance ordering cost on large types** → Measure before shipping; prefix-only fallback documented above.
- **Per-type counts on 1.27M TV rows** → Optional; omit if slow.
- **Conflicts with `password-management`** → It edits `login/page.tsx`, `helpers.tsx`, `user-provider.tsx` and `user-dropdown.tsx`. Land it first and restyle on top of it without changing its behavior.
- **Removing DataGrid from Media/Templates** → Lose built-in column sorting UI. The Catalog keeps relevance or title order; column sorting is not required by the spec.

## Migration Plan

The owner approved one data-only migration, `AddEssentialsTemplates`, for five missing built-in categories. Existing Video Games template IDs and field positions remain unchanged. Seed values live in a versioned module-owned artifact; rollback refuses to remove a template used by reviews because cross-module references have no database FK. No schema changes are included. Deploy backend and frontend together, since the frontend's showcase call and best-first rank depend on the new backend behavior; an older frontend keeps working against the new backend. Rollback reverts both. `@mui/x-data-grid` and the `/test` page can stay; removing them is separate cleanup.

## Open Questions

- Should the overall score store one decimal (schema change from `short` to `numeric(3,1)` plus migration)? Deferred; the owner decides separately.
- Should the Ctrl+K shortcut ship? Default: yes, focusing Catalog search from any authenticated page. Drop it if it conflicts with browser or OS bindings in testing.


## Owner-approved long-review writing follow-up
Creation step 2 retains its 860px dialog, chosen title and template controls. Scores use a two-column grid in field order, followed by a divider and full-width writing fields. Headline is labeled, multiline with a two-row maximum, display typography and a focus-accented bottom border. Notes starts at eight rows and grows without a maximum; a muted numeric word count appears only for nonempty notes. Creation and drawer edit share this markup, with overall/progress and Save/Cancel outside the scrolling body. Drawer detail retains full pre-wrapped notes. This adds no backend, API or schema behavior.

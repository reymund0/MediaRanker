## Why

MediaRanker's UI is a stock MUI purple theme wrapped around data grids and fixed-size cards. A full walkthrough of every screen, modal and popup (2026-09-30) found that the core reviewing flow is cramped and confusing, the catalog is hard to search, and several interactions are broken. The owner approved a visual overhaul, designed on a Claude Design canvas, to be implemented by Codex.

Walkthrough findings this change addresses:

- Reviews are 200px cards that expand in place into a 300px form with nested scrolling. The cover disappears in detail view, the page reflows, and six media-type rows render even though Books, Albums and Concerts have no catalog entries.
- New reviews prefill every score at 2.5 stars, so "not rated" looks identical to "mediocre". Step 2 forgets the chosen title, its "Back" cancels the whole card, and the template must be picked even when only one exists.
- After an update, the detail view shows scores in API order rather than template order (Sound, Story, Graphics, Gameplay).
- `GET /api/reviews/byMediaType` orders by overall score ascending, so the "ranking" is worst-first.
- Media search is three clicks deep (column menu → Filter), the popover covers the first result, results are alphabetical with punctuation first, IMDb year-only dates render as a fake "July 1st, YYYY", and the grid's 1px-overflowing scroller swallows the mouse wheel.
- "Add Template" opens a dialog titled "Edit Template" and inserts a blank row into the table. Built-in templates show disabled icons without explanation.
- Destructive confirmations use the primary purple button. Purple links on the dark card have low contrast. Page widths differ per screen (760/1100/1400px). Credits sits in the primary nav.
- "Resend Code" on confirm-signup stays disabled even with a prefilled username.

## What Changes

- Replace the theme with the "Amethyst" visual system: cool violet-black ground, one violet accent (the existing MediaRanker purple family) reserved for scores and the single primary action, red for destructive actions, Bricolage Grotesque / Geist / Geist Mono type. Unify the app shell, navigation (Library, Catalog, Templates), page width and footer attribution.
- Rebuild Reviews as **Library**: a best-first ranked poster grid per media type, a latest-review hero, type filter chips, and a single empty-state prompt instead of empty rows.
- Replace in-card detail/edit with a **review drawer**, and in-card creation with a **two-step new-review dialog**: rich title search, automatic template selection, and whole-number 1–10 scoring that starts unrated.
- Rebuild Media as a search-first **Catalog** with year-only dates, designed cover states (ready, pending, missing), per-row review status and an overflow menu for edit/delete.
- Rebuild **Templates** as cards with a side editor, with a "Duplicate" path for built-in templates.
- Redesign auth screens with a split layout and a **public cover showcase** on the sign-in wall, backed by a new anonymous endpoint that returns only already-resolved provider cover URLs and titles.
- Add owner-approved built-in starter templates for Movies, TV shows, Books, Albums and Concerts, preserving the existing Video Games template and its reviews.
- Give new-review step 2 and drawer edit a shared full-width long-review writing layout with pinned actions.
- Fix the walkthrough bugs listed above.

## Capabilities

### New Capabilities

- `visual-design-system`: Amethyst tokens, typography, app shell, navigation, buttons, dialogs, toasts and attribution placement.
- `review-library`: Ranked library, review drawer, new-review dialog and whole-number scoring.
- `catalog-browsing`: Search-first catalog, cover states, row review status and title management.
- `template-management`: Template cards, built-in duplication and the side editor.
- `sign-in-cover-showcase`: Redesigned auth screens and the anonymous cover showcase endpoint.

### Modified Capabilities

None as archived specs. The in-flight `automatic-cover-art` change owns cover resolution; this change restyles how resolved, pending and missing covers are displayed without changing resolution or polling behavior.

## Impact

- Frontend: `src/app/theme.ts`, layout and navbar, every page under `src/app`, shared components under `src/lib/components`. Fonts load via `next/font/google` (built into Next; no new package).
- Backend: one new `[AllowAnonymous]` Media endpoint with an in-memory daily cache, best-first review ordering, and relevance ordering for title search. No schema changes. The owner-approved data-only `AddEssentialsTemplates` migration inserts five system templates and twenty ordered score fields.
- Out of scope by owner decision: phone layouts and a dedicated accessibility pass.
- Sequencing: `password-management` touches the same auth pages and user menu and landed first in main commit 9181491 (PR #46).

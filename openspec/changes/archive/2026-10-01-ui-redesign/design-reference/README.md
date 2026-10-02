# ui-redesign — design reference

The visual source of truth for the `ui-redesign` change. Open `index.html` in a browser for the gallery. Each screen is a standalone HTML file in `screens/` with a matching 1440px PNG in `screenshots/`.

These files are exported from the design canvas, which is private to the owner, so everything Codex needs is here. Questions or a visual review: resume the designing session described in `../design.md` (Handoff).

## How to use these files

- **Pixels guide, specs decide.** If a screen and a `specs/*/spec.md` requirement disagree, the spec wins. Ask the designing session if it isn't clear.
- **Rebuild, don't copy.** The markup uses inline styles so each file stands alone. Implement through `theme.ts` (values in `tokens.css`) and the shared Base/Form components listed in `../tasks.md`, not by pasting styles.
- **Hover and focus states** are in each file's `<style>` block (for example `.btn-primary:hover`, `.row:hover`, the 2px focus outline). Static screenshots don't show them.
- **Icons:** use `@mui/icons-material` equivalents: Search, Add, Close, MoreHoriz, EditOutlined, DeleteOutline, DragIndicator, LockOutlined, ContentCopy, Visibility, Check, ChevronLeft/ChevronRight, ExpandMore, Movie (empty-state tile). The logo (three ascending bars on a violet tile) stays custom SVG; it's in every header.
- **Covers are stand-ins.** Coloured tiles with titles stand in for real IGDB/TMDB art. In the app every 2:3 tile shows the real cover through `CoverImage`, with the pending and missing states from the UI kit.
- **Sample data.** Stellar Blade and Metal Gear Solid 4 are the owner's real reviews. Other reviews, scores and ranks are illustrative. Catalog counts are real as of 2026-09-30.
- **Scores are whole numbers** (stored `short`). Ties rank by most recent update, which is why Elden Ring (9) ranks above Hollow Knight (9).
- **Out of scope:** phone layouts and a dedicated accessibility pass.

## Screens

| File | Shows | Spec |
| --- | --- | --- |
| `01-library.html` | Library: type chips, latest-review highlight, ranked posters (#1 outlined), empty-type prompt, attribution footer | review-library: best-first rankings, media type filter, latest review; visual-design-system: shell, attribution |
| `02-review-drawer.html` | Review drawer over the Library: cover, rank, overall, ordered field bars, headline, notes, dates, red Delete, Edit | review-library: review drawer |
| `03-new-review-search.html` | New review step 1: type switch, focused search, relevance-ordered results, reviewed titles dimmed with score | review-library: new-review dialog |
| `04-new-review-scoring.html` | Step 2, partly scored: chosen title with Change, auto-picked template, headline/notes, 1–10 inputs, "Not rated", Save disabled | review-library: whole-number scoring |
| `05-new-review-ready.html` | Step 2, all scored: preview 8 (average 8.5 rounds half to even), Save enabled | review-library: whole-number scoring |
| `06-new-review-no-results.html` | Step 1 with no matches | review-library: new-review dialog |
| `07-catalog-search.html` | Catalog search "elden ring": reviewed rows with score/rank and View, pending-cover row with Review, open overflow menu | catalog-browsing: search-first, readable rows, cover states, manage titles |
| `08-catalog-browse.html` | Catalog without a search: ready, pending and missing cover rows | catalog-browsing: cover states |
| `09-catalog-no-results.html` | Catalog with no matches | catalog-browsing: search-first |
| `10-templates.html` | Templates: built-in card with Duplicate, saved custom template selected, side editor | template-management |
| `11-sign-in.html` | Sign in: cover wall (showcase covers in the app, typographic fallback), labels above fields, password toggle, dev-only local user | sign-in-cover-showcase |
| `12-ui-kit.html` | Amethyst UI kit: colours, type, buttons, fields, scores, covers, toasts, confirm dialog, account menu | visual-design-system |

## Not drawn, follow the spec

- **Sign up, confirm signup, reset password:** same split layout and form style as `11-sign-in.html`, with their existing fields and flows.
- **Review drawer edit mode:** the drawer body becomes the step 2 form from `04`, without the title header or "Change", with Cancel/Save in the drawer footer.
- **"New template" or "Duplicate" editor:** the `10` editor titled "New template", with no card selected and no new card in the list.
- **Add/edit media dialog:** the `12` dialog style with the existing fields (title, release date, media type).
- **Library with no reviews:** an empty state using the `01` empty-type prompt style with a "New review" button.

# TV hierarchy design reference

These are the Claude Design sources for the five screens in this change, copied from the owner's canvas on 2026-10-01. Each `.dc.html` file is one screen.

They don't open as plain HTML. They use the Claude Design runtime (`support.js`, which isn't included) and template syntax:

- `{{name}}` inserts a value computed in the `renderVals()` script at the bottom of the file.
- `<sc-for list="{{rows}}" as="r">` repeats its contents for each item.
- `<sc-if value="{{flag}}">` shows its contents when the flag is true.

Read them as precise specs:

- **Markup and inline styles:** layout, spacing, type sizes and Amethyst token colors (hex values match `src/app/theme.ts`).
- **The `renderVals()` script:** the sample data, the states each row can be in (reviewed or unreviewed, expanded or collapsed, cover ready, pending or missing), and the interactions (expand, "Show more", pick, Change).

Implement them with the app's MUI theme and shared components, not by copying inline styles.

The owner can show you the screens running on the canvas, and can send screenshots on request.

| File | Screen | Clickable on the canvas |
| --- | --- | --- |
| `CatalogTV.dc.html` | Catalog, TV tab | Yes: expand series and seasons, "Show more", search, ⋯ menu |
| `LibraryTV.dc.html` | Library, TV tab | No |
| `ReviewDetailEpisode.dc.html` | Review drawer for an episode | Links only |
| `ReviewDetailSeries.dc.html` | Review drawer for a series | Links only |
| `NewReviewTV.dc.html` | New-review dialog for TV | Yes: All results, series view, seasons, pick an episode or the series, scoring |

## Known differences from the spec (the spec wins)

- **Episode batch size:** the mocks load 10 episodes at a time; the spec uses 25.
- **Run years:** "2022–" in the mocks means first to latest season year, "2022–2025". The catalog has no "ongoing" flag.
- **Sample data:** reviews, headlines, notes and ranks are illustrative. Counts and Breaking Bad's season 1–2 episode titles are real.
- **Fallback titles:** episodes without real sample titles use IMDb's fallback naming, "Episode #s.e".

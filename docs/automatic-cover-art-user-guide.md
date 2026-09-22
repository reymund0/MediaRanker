# Automatic cover art: local user guide

## Open and sign in

Open [MediaRanker](http://localhost:3000/auth/login) and select **Use local test user**. No Cognito account or password is needed for this development mode. Refresh preserves the login within the tab; Logout clears it. Reviews made in this mode belong to the shared local test identity.

## Browse artwork

Open [Media](http://localhost:3000/media), choose a media type, and browse or search the catalog. Movies and TV use TMDB artwork matched through IMDb identity. Games use IGDB identities and cover references. Seasons and episodes share their series poster.

The current local catalog includes five IGDB games (four Thief titles and Baldur's Gate), The Matrix, and Breaking Bad sample records from the bounded provider check. Cached cover references can display while provider lookups are disabled.

When lookups are enabled, browsing or reviewing a supported title requests missing or expired artwork automatically. The visible list refreshes pending covers every two seconds for up to 30 seconds. Missing, unsupported, or failed images show a placeholder. Artwork availability does not prevent reviewing a title.

## Create or edit media

Use the Media page's create/edit controls to enter the title, type, and release date. There is no cover-upload step. Editing metadata preserves an existing automatic cover association. Manually created titles without a supported provider identity show a placeholder; creating a title does not automatically match it by name.

## Write reviews

Open [Reviews](http://localhost:3000/reviews), select a media type, and add a review by choosing an unreviewed title. Artwork accompanies the title automatically. Background artwork refresh preserves your unsaved review text and existing card order.

## Credits and setup

[Credits](http://localhost:3000/credits) lists the image providers and attribution.

For this local session, new TMDB/IGDB artwork requests and IMDb/IGDB catalog imports remain disabled. Existing cached references still render until they expire. Credentials were validated during the bounded live check, but starting the app does not enable provider jobs or import the full catalog.

See [development commands and provider setup](conventions/dev-commands.md) for restart commands, flags, cache behavior, and import controls. Bulk ingestion still requires the recorded performance measurements before increasing budgets.

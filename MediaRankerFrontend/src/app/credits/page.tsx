"use client";

import { Box, Link, Stack, Typography } from "@mui/material";
import { PageContainer } from "@/lib/components/layout/page-container";

const TMDB_LOGO_URL =
  "https://www.themoviedb.org/assets/v4/logos/v2/blue_square_2-d537fb228cf3ded904ef09b136fe3fec72548ebc1fea3fbbd1ad9e36364db38b.svg";

export default function CreditsPage() {
  return (
    <PageContainer>
      <Stack spacing={3}>
        <Box>
          <Typography variant="h4" component="h1">
            Credits
          </Typography>
          <Typography color="text.secondary">
            Media Ranker uses these services to identify titles and display
            cover art.
          </Typography>
        </Box>

        <Stack spacing={1.5} alignItems="flex-start">
          <Box
            component="img"
            src={TMDB_LOGO_URL}
            alt="The Movie Database (TMDB)"
            sx={{ width: 96, height: 96, objectFit: "contain" }}
          />
          <Typography>
            This product uses the TMDB API but is not endorsed or certified by
            TMDB.
          </Typography>
          <Link
            href="https://www.themoviedb.org"
            target="_blank"
            rel="noreferrer"
          >
            The Movie Database (TMDB)
          </Link>
        </Stack>

        <Stack spacing={1} alignItems="flex-start">
          <Typography variant="h6">IGDB</Typography>
          <Typography color="text.secondary">
            Game metadata and available game cover artwork are provided by IGDB.
          </Typography>
          <Link href="https://www.igdb.com" target="_blank" rel="noreferrer">
            Visit IGDB
          </Link>
        </Stack>
      </Stack>
    </PageContainer>
  );
}

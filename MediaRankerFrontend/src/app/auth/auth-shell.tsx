"use client";

import { ReactNode } from "react";
import { Box, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
import { useQuery } from "@/lib/api/use-query";
import { AppWordmark } from "@/lib/components/layout/app-wordmark";
import { CoverImage } from "@/lib/components/data-display/cover-image";

const fallbackTitles = [
  "Metal Gear Solid 4",
  "Heat",
  "Elden Ring",
  "The Wire",
  "Hollow Knight",
  "Spirited Away",
  "Stellar Blade",
  "Alien",
  "Hades",
  "Twin Peaks",
  "Celeste",
  "Dune",
  "Cadence of Hyrule",
  "Blade Runner",
  "The Last of Us",
  "Portal 2",
  "Arrival",
  "Disco Elysium",
  "Ocarina of Time",
  "Interstellar",
];

export function AuthShell({ children }: { children: ReactNode }) {
  const { data } = useQuery<{ title: string; coverImageUrl: string }[]>({
    route: "/api/media/showcase",
    queryKey: ["media-showcase"],
    staleTime: 3_600_000,
    retry: false,
  });
  const covers = data?.length
    ? data
    : fallbackTitles.map((title) => ({ title, coverImageUrl: "" }));
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: "54% 46%",
        minHeight: "100dvh",
      }}
    >
      <Box
        sx={{
          position: "relative",
          overflow: "hidden",
          borderRight: "1px solid",
          borderColor: "divider",
          minHeight: "100dvh",
        }}
      >
        <Box
          aria-hidden="true"
          sx={{
            position: "absolute",
            inset: "-100px -90px",
            display: "grid",
            gridTemplateColumns: "repeat(5, 1fr)",
            gap: 2,
            transform: "rotate(-7deg)",
            opacity: 0.32,
          }}
        >
          {Array.from({ length: 20 }, (_, index) => {
            const cover = covers[index % covers.length];
            return (
              <Box
                key={index}
                sx={{
                  aspectRatio: "2/3",
                  bgcolor: (theme) =>
                    alpha(
                      [
                        theme.palette.primary.dark,
                        theme.palette.success.dark,
                        theme.palette.warning.dark,
                        theme.palette.error.dark,
                        theme.palette.secondary.dark,
                      ][index % 5],
                      0.5,
                    ),
                  border: "1px solid",
                  borderColor: "divider",
                  borderRadius: 1.25,
                  overflow: "hidden",
                  position: "relative",
                }}
              >
                {cover.coverImageUrl ? (
                  <CoverImage
                    src={cover.coverImageUrl}
                    alt={cover.title}
                    sx={{ width: "100%", height: "100%", objectFit: "cover" }}
                    placeholderSx={{ width: "100%", height: "100%" }}
                    placeholder={
                      <Typography variant="h6" sx={{ p: 2, lineHeight: 1.1 }}>
                        {cover.title}
                      </Typography>
                    }
                  />
                ) : (
                  <Typography
                    variant="h6"
                    sx={{
                      position: "absolute",
                      bottom: 16,
                      left: 16,
                      right: 12,
                      lineHeight: 1.1,
                    }}
                  >
                    {cover.title}
                  </Typography>
                )}
              </Box>
            );
          })}
        </Box>
        <Box
          sx={{
            position: "absolute",
            inset: 0,
            background: (theme) =>
              `linear-gradient(180deg, transparent 20%, ${alpha(theme.palette.background.default, 0.8)} 60%, ${theme.palette.background.default} 90%)`,
          }}
        />
        <Box sx={{ position: "absolute", bottom: 64, left: 64, right: 44 }}>
          <Box sx={{ mb: 3 }}>
            <AppWordmark />
          </Box>
          <Typography
            variant="h1"
            sx={{ maxWidth: 620, fontSize: 60, lineHeight: 1.04 }}
          >
            Rank everything you play and watch.
          </Typography>
          <Typography sx={{ mt: 2.5, maxWidth: 510 }} color="text.secondary">
            Scores out of ten, templates you control, and one list that’s
            actually yours.
          </Typography>
        </Box>
      </Box>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          px: 6,
          py: 5,
        }}
      >
        <Box
          sx={{
            width: "100%",
            maxWidth: 400,
            "& .MuiCard-root": {
              background: "none",
              border: 0,
              boxShadow: "none",
              overflow: "visible",
            },
            "& .MuiCardContent-root": { p: 0 },
            "& .MuiCardContent-root:last-child": { pb: 0 },
            "& .MuiOutlinedInput-root": { minHeight: 48 },
            "& h1": { textAlign: "left", fontSize: 40, mb: 0 },
            "& h1 + p": { textAlign: "left", mt: -1, mb: 0.75 },
            "& .MuiButton-root": { minHeight: 46 },
          }}
        >
          {children}
        </Box>
      </Box>
    </Box>
  );
}

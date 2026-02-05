"use client";
import { Box, Typography } from "@mui/material";

export default function Home() {
    return (
        <Box
            sx={{
                minHeight: "100vh",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                bgcolor: "#f5f5f5",
            }}
        >
            <Box
                sx={{
                    bgcolor: "white",
                    p: 4,
                    borderRadius: 2,
                    boxShadow: 1,
                    textAlign: "center",
                }}
            >
                <Typography variant="h3" component="h1" gutterBottom>
                    Welcome to Media Ranker
                </Typography>
                <Typography variant="body1" color="text.secondary">
                    You are successfully logged in!
                </Typography>
            </Box>
        </Box>
    );
}

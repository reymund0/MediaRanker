"use client";

import { useState } from "react";
import { useUser } from "@/lib/auth/user-provider";
import { useMutation } from "@/lib/api/use-mutation";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";
import { BaseStarRanking } from "@/lib/components/inputs/rankings/base-star-ranking";
import { Box, Divider, Stack, Typography } from "@mui/material";

export default function Test() {
  const userContext = useUser();
  const { showInfo, showSuccess, showError, closeAlert } = useAlert();
  const [rating, setRating] = useState<number>(5);

  const helloWorldMutation = useMutation<void, string>({
    route: "/api/test/helloWorld",
    method: "POST",
    onSuccess: (data) => {
      showSuccess(`Success! ${data}`);
    },
    onError: (error) => {
      showError(error.message || "Hello World error message not present");
    },
  });

  const domainErrorMutation = useMutation<void, never>({
    route: "/api/test/domainError",
    method: "POST",
    onSuccess: () => {
      showSuccess("Domain error endpoint unexpectedly succeeded.");
    },
    onError: (error) => {
      showError(error.message || "Domain error message not present");
    },
  });

  const unexpectedErrorMutation = useMutation<void, never>({
    route: "/api/test/unexpectedError",
    method: "POST",
    onSuccess: () => {
      showSuccess("Unexpected error endpoint unexpectedly succeeded.");
    },
    onError: (error) => {
      showError(error.message || "Unexpected error message not present");
    },
  });

  const callEndpoint = (mutation: { mutate: () => void }, label: string) => {
    closeAlert();
    showInfo(`Calling ${label}...`);
    mutation.mutate();
  };

  console.log("userContext", userContext);

  return (
    <Box sx={{ p: 4, display: "flex", flexDirection: "column", gap: 4 }}>
      <Typography variant="h4">Test Page</Typography>
      
      <Stack spacing={2} direction="column">
        <Typography variant="h6" sx={{ mb: 2 }}>Ranking Component Demo</Typography>
        
        <Box sx={{ display: "flex", flexDirection: "column", width: "10%" }}>
          <BaseStarRanking 
            label="Media Rating" 
            value={rating} 
            onChange={setRating} 
          />
        </Box>
        <Typography variant="body2" sx={{ mt: 1, color: "text.secondary" }}>
          Current State Value: {rating}
        </Typography>
      </Stack>

      <Divider />

      <Box>
        <Typography variant="h6" sx={{ mb: 2 }}>API Test Actions</Typography>
        <Typography variant="body2" sx={{ mb: 2 }}>
          Signed in as{" "}
          {userContext.username
            ? `(${userContext.username})`
            : "(not authenticated)"}
        </Typography>
        
        <Box sx={{ display: "flex", gap: 2, flexWrap: "wrap" }}>
          <PrimaryButton
            onClick={() => callEndpoint(helloWorldMutation, "/api/test/helloWorld")}
            disabled={helloWorldMutation.isPending}
          >
            Call Hello World
          </PrimaryButton>
          
          <PrimaryButton
            onClick={() =>
              callEndpoint(domainErrorMutation, "/api/test/domainError")
            }
            disabled={domainErrorMutation.isPending}
          >
            Trigger Domain Error
          </PrimaryButton>
          
          <PrimaryButton
            onClick={() =>
              callEndpoint(unexpectedErrorMutation, "/api/test/unexpectedError")
            }
            disabled={unexpectedErrorMutation.isPending}
          >
            Trigger Unexpected Error
          </PrimaryButton>
        </Box>
      </Box>
    </Box>
  );
}

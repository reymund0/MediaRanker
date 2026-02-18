"use client";
import { useUser } from "@/lib/auth/user-provider";
import { useMutation } from "@/lib/api/use-mutation";
import { ApiError } from "@/lib/api/types";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";

export default function Test() {
  const { user } = useUser();
  const { showInfo, showSuccess, showError, closeAlert } = useAlert();

  const helloWorldMutation = useMutation<string>({
    route: "/api/test/helloWorld",
    method: "POST",
    onSuccess: (data) => {
      showSuccess(`Success! ${data}`);
    },
    onError: (error: ApiError) => {
      showError(error.message || "Request failed");
    },
  });

  const onTestClick = () => {
    closeAlert();
    showInfo("Calling /api/test/helloWorld...");
    helloWorldMutation.mutate();
  };

  console.log("user", user);

  return (
    <div>
      Test page signed in as{" "}
      {user ? `(${user.username})` : "(not authenticated)"}
      <br />
      <br />
      <PrimaryButton
        onClick={onTestClick}
        disabled={helloWorldMutation.isPending}
      >
        Test Button
      </PrimaryButton>
    </div>
  );
}

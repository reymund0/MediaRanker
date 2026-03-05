"use client";
import { useUser } from "@/lib/auth/user-provider";
import { useMutation } from "@/lib/api/use-mutation";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";
import { useAlert } from "@/lib/components/feedback/alert/alert-provider";

export default function Test() {
  const userContext = useUser();
  const { showInfo, showSuccess, showError, closeAlert } = useAlert();

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
    <div>
      Test page signed in as{" "}
      {userContext.username ? `(${userContext.username})` : "(not authenticated)"}
      <br />
      <br />
      <PrimaryButton
        onClick={() => callEndpoint(helloWorldMutation, "/api/test/helloWorld")}
        disabled={helloWorldMutation.isPending}
      >
        Call Hello World
      </PrimaryButton>
      <br />
      <br />
      <PrimaryButton
        onClick={() =>
          callEndpoint(domainErrorMutation, "/api/test/domainError")
        }
        disabled={domainErrorMutation.isPending}
      >
        Trigger Domain Error
      </PrimaryButton>
      <br />
      <br />
      <PrimaryButton
        onClick={() =>
          callEndpoint(unexpectedErrorMutation, "/api/test/unexpectedError")
        }
        disabled={unexpectedErrorMutation.isPending}
      >
        Trigger Unexpected Error
      </PrimaryButton>
    </div>
  );
}

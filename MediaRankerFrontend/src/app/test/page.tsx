"use client";
import { useUser } from "@/lib/providers/user-provider";
import { useMutation, ApiError } from "@/lib/hooks/use-mutation";
import { PrimaryButton } from "@/lib/components/inputs/button/primary-button";

export default function Test() {
  const { user } = useUser();
  console.log(user);
  const helloWorldMutation = useMutation<string>({
    route: "/api/test/helloWorld",
    method: "POST",
    onSuccess: (data) => {
      console.log("Success!", data);
    },
    onError: (error: ApiError) => {
      console.log("Error!", error.message, error.errors);
    },
  });

  return (
    <div>
      Test page signed in as {user ? `(${user.username})` : "(not authenticated)"}
      <br />
      <br />
      <PrimaryButton onClick={() => helloWorldMutation.mutate()}>Test Button</PrimaryButton>
    </div>
  );
}
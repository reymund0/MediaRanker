"use client";
import { useUser } from "@/lib/hooks/user-context";

export default function Test() {
  const { user } = useUser();
  console.log(user);
  return <div>Test page signed in as {user ? `(${user.username})` : "(not authenticated)"}</div>;
}
"use client";
import { Amplify } from "aws-amplify";
import { useEffect, useRef } from "react";

function configureAmplify() {
  // We are using Cognito for authentication.
  Amplify.configure({
    Auth: {
      Cognito: {
        userPoolId: process.env.NEXT_PUBLIC_COGNITO_USER_POOL_ID!,
        userPoolClientId: process.env.NEXT_PUBLIC_COGNITO_CLIENT_ID!,
        loginWith: {
          email: true,
        },
      },
    },
  });
}

export function AmplifyProvider({ children }: { children: React.ReactNode }) {
  const setupDone = useRef(false);

  useEffect(() => {
    if (!setupDone.current) {
      setupDone.current = true;
      configureAmplify();
    }
  }, []);

  return <>{children}</>;
}

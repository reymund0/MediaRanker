"use client";
import { Amplify } from "aws-amplify";
import { useEffect } from "react";

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

let isConfigured = false;

export function AmplifyProvider({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    if (!isConfigured) {
      configureAmplify();
      isConfigured = true;
    }
  }, []);

  return <>{isConfigured ? children : null}</>;
}

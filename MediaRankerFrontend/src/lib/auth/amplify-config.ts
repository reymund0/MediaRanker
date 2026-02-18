import { Amplify } from "aws-amplify";

declare global {
  var __amplifyConfigured: boolean | undefined;
}

if (!globalThis.__amplifyConfigured) {
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

  globalThis.__amplifyConfigured = true;
}

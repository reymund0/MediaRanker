"use client";
import { Amplify } from "aws-amplify";
import { useEffect } from "react";

let isConfigured = false;

function configureAmplify() {
	if (isConfigured) {
		return;
	}

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
	isConfigured = true;
}

export function AmplifyProvider({ children }: { children: React.ReactNode }) {
	useEffect(() => {
		configureAmplify();
	}, []);
 
	return <>{children}</>;
}

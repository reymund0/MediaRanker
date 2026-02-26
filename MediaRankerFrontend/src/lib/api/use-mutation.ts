import {
  useMutation as useTanstackMutation,
  UseMutationOptions,
} from "@tanstack/react-query";
import { useUser } from "../auth/user-provider";
import { ProblemDetails } from "./types";

export interface UseMutationOptionsType<T> extends Omit<
  UseMutationOptions<T>,
  "mutationFn"
> {
  route: string;
  method: "POST" | "PUT" | "DELETE";
  data?: Record<string, unknown>;
}

export function useMutation<T = unknown>(options: UseMutationOptionsType<T>) {
  const user = useUser();
  const token = user.session?.tokens?.idToken?.toString();

  const mutation = useTanstackMutation<T>({
    mutationFn: async () => httpMutation<T>(options, token),
    ...options,
  });

  return mutation;
}

const httpMutation = async <T>(
  options: UseMutationOptionsType<T>,
  token: string | undefined,
): Promise<T> => {
  const url = new URL(options.route, process.env.NEXT_PUBLIC_API_URL);
  const response = await fetch(url.toString(), {
    method: options.method,
    headers: {
      "Content-Type": "application/json",
      ...(token && { Authorization: `Bearer ${token}` }),
    },
    body: options.data ? JSON.stringify(options.data) : undefined,
  });

  const body = await response.json();

  if (!response.ok) {
    const problemDetails = normalizeProblemDetails(body, response);
    console.error("API ProblemDetails", {
      problemDetails,
      route: options.route,
      method: options.method,
      body: options.data ?? null,
    });
    const message = problemDetails.detail || "Request failed";
    throw new Error(message);
  }

  return body as T;
};

const normalizeProblemDetails = (
  body: unknown,
  response: Response,
): ProblemDetails => {
  if (body && typeof body === "object") {
    return {
      status: response.status,
      ...(body as Record<string, unknown>),
    } as ProblemDetails;
  }

  // Handle generic HTTP errors (non-problem details).
  return {
    status: response.status,
    title: response.statusText || "Request failed",
    detail: undefined,
  } satisfies ProblemDetails;
};

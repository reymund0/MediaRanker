import {
  useMutation as useTanstackMutation,
  UseMutationOptions,
} from "@tanstack/react-query";
import { useUser } from "../auth/user-provider";
import { ApiResponse, ApiError } from "./types";

export interface UseMutationOptionsType<T> extends Omit<
  UseMutationOptions<T, ApiError>,
  "mutationFn"
> {
  route: string;
  method: "POST" | "PUT" | "DELETE";
  data?: Record<string, unknown>;
}

export function useMutation<T = unknown>(options: UseMutationOptionsType<T>) {
  const user = useUser();
  const token = user.session?.tokens?.idToken?.toString();

  const mutation = useTanstackMutation<T, ApiError>({
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

  const body: ApiResponse<T> = await response.json().catch(() => ({
    success: false as const,
    message: `Request failed with status ${response.status}`,
  }));

  if (!body.success) {
    throw new ApiError(body.message, body.errors);
  }

  if (!response.ok) {
    throw new ApiError(`Request failed with status ${response.status}`);
  }

  return body.data;
};

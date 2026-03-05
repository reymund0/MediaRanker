import {
  useMutation as useTanstackMutation,
  UseMutationOptions,
} from "@tanstack/react-query";
import { useUser } from "../auth/user-provider";
import { httpRequest } from "./http-utils";
import { ProblemDetailsError } from "./problem-details";

export interface UseMutationOptionsType<TRequest, TResponse> extends Omit<
  UseMutationOptions<TResponse, ProblemDetailsError, TRequest>,
  "mutationFn"
> {
  route: string;
  method: "POST" | "PUT" | "DELETE";
  data?: TRequest;
}

export const useMutation = <TRequest = unknown, TResponse = unknown>(options: UseMutationOptionsType<TRequest, TResponse>) => {
  const user = useUser();
  const token = user.sessionToken;

  const mutation = useTanstackMutation<TResponse, ProblemDetailsError, TRequest>({
    mutationFn: async (data) =>
      httpRequest<TRequest, TResponse>({ ...options, data }, token),
    ...options,
  });

  return mutation;
}



import { useUser } from "../auth/user-provider";
import { httpRequest } from "./http-utils";
import {
  useQuery as useTanstackQuery,
  UseQueryOptions,
} from "@tanstack/react-query";
import { ProblemDetailsError } from "./problem-details";

export interface UseQueryOptionsType<TResponse> extends Omit<
  UseQueryOptions<TResponse, ProblemDetailsError>,
  "queryFn" | "method"
> {
  route: string;
}

export const useQuery = <TResponse = unknown>(
  options: UseQueryOptionsType<TResponse>,
) => {
  const user = useUser();
  const token = user.sessionToken;

  const query = useTanstackQuery<TResponse, ProblemDetailsError>({
    queryFn: async () =>
      httpRequest<undefined, TResponse>({ ...options, method: "GET" }, token),
    ...options,
  });

  return query;
};

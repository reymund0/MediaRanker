import { useEffect, useRef, useState } from "react";
import { PageRequest, PageResult } from "../contracts/shared";
import { ProblemDetailsError } from "./problem-details";
import { useQuery } from "./use-query";

export type UsePagedQueryOptions = {
  route: string;
  routeParams?: Record<string, string | number | boolean | undefined | null>;
  pageRequest?: Omit<PageRequest, "page" | "pageSize">;
  queryKey: readonly unknown[];
  enabled?: boolean;
  pageSize?: number;
  debounceMs?: number;
  minSearchChars?: number;
};

export type UsePagedQueryResult<T> = {
  items: T[];
  totalCount: number;
  isLoading: boolean;
  error: ProblemDetailsError | null;
};

export function usePagedQuery<T>({
  route,
  routeParams,
  pageRequest,
  queryKey,
  enabled,
  pageSize = 25,
  debounceMs = 300,
  minSearchChars = 1,
}: UsePagedQueryOptions): UsePagedQueryResult<T> {
  const clampedPageSize = Math.min(100, Math.max(1, pageSize));
  const rawSearchTerm = pageRequest?.searchTerm ?? "";

  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState(rawSearchTerm);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const nonSearchKey = JSON.stringify({
    route,
    routeParams,
    pageRequest: { ...pageRequest, searchTerm: undefined },
  });
  const prevNonSearchKey = useRef(nonSearchKey);

  useEffect(() => {
    if (prevNonSearchKey.current !== nonSearchKey) {
      prevNonSearchKey.current = nonSearchKey;
      if (timerRef.current !== null) {
        clearTimeout(timerRef.current);
        timerRef.current = null;
      }
      setDebouncedSearchTerm(rawSearchTerm);
      return;
    }

    if (timerRef.current !== null) {
      clearTimeout(timerRef.current);
    }
    timerRef.current = setTimeout(() => {
      setDebouncedSearchTerm(rawSearchTerm);
      timerRef.current = null;
    }, debounceMs);

    return () => {
      if (timerRef.current !== null) {
        clearTimeout(timerRef.current);
        timerRef.current = null;
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rawSearchTerm, nonSearchKey]);

  const params = new URLSearchParams();

  if (routeParams) {
    for (const [k, v] of Object.entries(routeParams)) {
      if (v !== undefined && v !== null && v !== "") {
        params.set(k, String(v));
      }
    }
  }

  params.set("page", "0");
  params.set("pageSize", String(clampedPageSize));

  if (pageRequest) {
    const { sortField, sortDirection, searchField } = pageRequest;
    if (sortField) params.set("sortField", sortField);
    if (sortDirection) params.set("sortDirection", sortDirection);
    if (debouncedSearchTerm) {
      params.set("searchTerm", debouncedSearchTerm);
      if (searchField) params.set("searchField", searchField);
    }
  }

  const queryString = params.toString();
  const fullRoute = queryString ? `${route}?${queryString}` : route;

  const isFetchEnabled =
    enabled !== false && debouncedSearchTerm.length >= minSearchChars;

  const { data, isLoading, error } = useQuery<PageResult<T>>({
    route: fullRoute,
    queryKey: [...queryKey, debouncedSearchTerm, clampedPageSize],
    enabled: isFetchEnabled,
  });

  return {
    items: data?.items ?? [],
    totalCount: data?.totalCount ?? 0,
    isLoading,
    error: error ?? null,
  };
}

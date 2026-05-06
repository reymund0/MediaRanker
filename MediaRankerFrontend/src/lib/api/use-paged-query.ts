import { useEffect, useRef, useState } from "react";
import { keepPreviousData } from "@tanstack/react-query";
import { PageRequest, PageResult } from "../contracts/shared";
import { ProblemDetailsError } from "./problem-details";
import { useQuery } from "./use-query";

export type UsePagedQueryOptions = {
  route: string;
  routeParams?: Record<string, string | number | boolean | undefined | null>;
  pageRequest?: Omit<PageRequest, "pageSize" | "includeTotalCount">;
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

  // Filter key: changes only when route, routeParams, or filter criteria change.
  // Page, pageSize, sort do NOT affect this key, so they will not trigger a recount.
  const filterKey = JSON.stringify({
    route,
    routeParams,
    searchField: pageRequest?.searchField,
    searchTerm: debouncedSearchTerm,
  });

  const [storedTotalCount, setStoredTotalCount] = useState(0);
  const [lastCountedFilterKey, setLastCountedFilterKey] = useState<string | null>(null);

  const params = new URLSearchParams();

  if (routeParams) {
    for (const [k, v] of Object.entries(routeParams)) {
      if (v !== undefined && v !== null && v !== "") {
        params.set(k, String(v));
      }
    }
  }

  params.set("page", String(pageRequest?.page ?? 0));
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

  const isSearching = !!pageRequest?.searchField; // If not searching we shouldn't prevent fetching.
  const isFetchEnabled =
    enabled !== false &&
    (!isSearching || debouncedSearchTerm.length >= minSearchChars);

  const shouldIncludeCount = isFetchEnabled && filterKey !== lastCountedFilterKey;
  if (shouldIncludeCount) {
    params.set("includeTotalCount", "true");
  }

  const queryString = params.toString();
  const fullRoute = queryString ? `${route}?${queryString}` : route;

  const { data, isLoading, error } = useQuery<PageResult<T>>({
    route: fullRoute,
    queryKey: [
      ...queryKey,
      pageRequest?.page ?? 0,
      clampedPageSize,
      pageRequest?.sortField,
      pageRequest?.sortDirection,
      pageRequest?.searchField,
      debouncedSearchTerm,
      shouldIncludeCount,
    ],
    enabled: isFetchEnabled,
    // Prevent clearing old data on refetch, avoiding flickering in UI from options/rows changing.
    placeholderData: keepPreviousData,
  });

  // Capture the returned count whenever the server includes it (i.e. on filter changes).
  useEffect(() => {
    if (data?.totalCount != null) {
      setStoredTotalCount(data.totalCount);
      setLastCountedFilterKey(filterKey);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data, filterKey]);

  // When fetch is disabled, return empty rows instead of potentially cached data.
  return {
    items: isFetchEnabled ? (data?.items ?? []) : [],
    totalCount: isFetchEnabled ? storedTotalCount : 0,
    isLoading,
    error: error ?? null,
  };
}

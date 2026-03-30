import { parseJsonSafe } from "@/lib/utils";
import { ProblemDetails, ProblemDetailsError } from "./problem-details";

export interface httpRequestOptions<TRequest> {
  route: string;
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  data?: TRequest;
}

export const httpRequest = async <TRequest, TResponse>(
  options: httpRequestOptions<TRequest>,
  token: string | undefined,
): Promise<TResponse> => {
  const url = new URL(options.route, process.env.NEXT_PUBLIC_API_URL);
  const response = await fetch(url.toString(), {
    method: options.method,
    headers: {
      "Content-Type": "application/json",
      ...(token && { Authorization: `Bearer ${token}` }),
    },
    body: options.data ? JSON.stringify(options.data) : undefined,
  });

  const body = await parseJsonSafe(response);

  if (!response.ok) {
    const problemDetails = normalizeProblemDetails(body, response);
    console.error("API ProblemDetails", {
      problemDetails,
      route: options.route,
      method: options.method,
      body: options.data ?? null,
    });
    throw new ProblemDetailsError(problemDetails);
  }

  return body as TResponse;
};

const normalizeProblemDetails = (
  body: unknown,
  response: Response,
): ProblemDetails => {
  // If the response body is a problem details object, return it.
  if (body && typeof body === "object") {
    return {
      status: response.status,
      ...(body as Record<string, unknown>),
    } as ProblemDetails;
  }

  // Handle generic HTTP errors (non-problem details).
  return {
    type: "about:blank",
    title: response.statusText || "Request failed",
    status: response.status,
    detail: undefined,
  } satisfies ProblemDetails;
};

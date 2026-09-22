const STORAGE_KEY = "media-ranker.local-test-auth";
export const LOCAL_TEST_AUTH_TOKEN = "MediaRankerLocalTest";
export const LOCAL_TEST_USER_ID = "local-test-user";
export const LOCAL_TEST_USERNAME = "Local test user";

export function isLocalTestAuthAvailable(): boolean {
  if (
    process.env.NEXT_PUBLIC_ENABLE_LOCAL_TEST_LOGIN !== "true" ||
    process.env.NODE_ENV !== "development" ||
    typeof window === "undefined"
  ) {
    return false;
  }

  const hostname = window.location.hostname;
  return (
    hostname === "localhost" ||
    hostname === "127.0.0.1" ||
    hostname === "[::1]" ||
    hostname === "::1"
  );
}

export function isLocalTestAuthActive(): boolean {
  return (
    isLocalTestAuthAvailable() &&
    window.sessionStorage.getItem(STORAGE_KEY) === "true"
  );
}

export function activateLocalTestAuth(): void {
  if (isLocalTestAuthAvailable()) {
    window.sessionStorage.setItem(STORAGE_KEY, "true");
  }
}

export function clearLocalTestAuth(): void {
  if (typeof window !== "undefined") {
    window.sessionStorage.removeItem(STORAGE_KEY);
  }
}

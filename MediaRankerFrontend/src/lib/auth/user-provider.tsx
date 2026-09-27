"use client";
import { createContext, useContext, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import {
  getCurrentUser,
  AuthUser,
  fetchAuthSession,
  AuthSession,
} from "aws-amplify/auth";
import {
  isLocalTestAuthActive,
  LOCAL_TEST_AUTH_TOKEN,
  LOCAL_TEST_USER_ID,
  LOCAL_TEST_USERNAME,
} from "./local-test-auth";

const AUTH_PATHS = ["/auth/login", "/auth/signup", "/auth/confirm-signup"];
const PUBLIC_PATHS = [...AUTH_PATHS];

interface UserContextType {
  userId: string | undefined;
  username: string | undefined;
  sessionToken: string | undefined;
  isAuthenticated: boolean;
}

const UserContext = createContext<UserContextType>({
  userId: undefined,
  username: undefined,
  sessionToken: undefined,
  isAuthenticated: false,
});

export const useUser = () => useContext(UserContext);

export const UserProvider = ({ children }: { children: React.ReactNode }) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [session, setSession] = useState<AuthSession | null>(null);
  const [localTestAuth, setLocalTestAuth] = useState(false);
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    const checkAuth = async () => {
      try {
        if (isLocalTestAuthActive()) {
          setLocalTestAuth(true);
          setUser(null);
          setSession(null);
          if (AUTH_PATHS.includes(pathname)) {
            router.replace("/reviews");
          }
          return;
        }

        setLocalTestAuth(false);
        const currentUser = await getCurrentUser();
        const currentSession = await fetchAuthSession();
        setUser(currentUser);
        setSession(currentSession);
        // If user is authenticated and trying to access auth pages, redirect to home.
        if (AUTH_PATHS.includes(pathname)) {
          router.replace("/reviews");
        }
      } catch {
        setLocalTestAuth(false);
        setUser(null);
        setSession(null);
        // If user is not authenticated and trying to access non-public pages, redirect to login.
        if (!PUBLIC_PATHS.includes(pathname)) {
          router.replace("/auth/login");
        }
      }
    };

    checkAuth();
  }, [pathname, router]);

  return (
    <UserContext.Provider
      value={{
        userId: localTestAuth ? LOCAL_TEST_USER_ID : user?.userId,
        username: localTestAuth ? LOCAL_TEST_USERNAME : user?.username,
        sessionToken: localTestAuth
          ? LOCAL_TEST_AUTH_TOKEN
          : session?.tokens?.idToken?.toString(),
        isAuthenticated: localTestAuth || !!user,
      }}
    >
      {children}
    </UserContext.Provider>
  );
};

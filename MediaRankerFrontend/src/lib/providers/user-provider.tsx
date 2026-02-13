"use client";
import { createContext, useContext, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import {
  getCurrentUser,
  AuthUser,
  fetchAuthSession,
  AuthSession,
} from "aws-amplify/auth";

const AUTH_PATHS = ["/auth/login", "/auth/signup", "/auth/confirm-signup"];
const PUBLIC_PATHS = [...AUTH_PATHS, "/test"];

interface UserContextType {
  user: AuthUser | null;
  session: AuthSession | null;
  isAuthenticated: boolean;
}

const UserContext = createContext<UserContextType>({
  user: null,
  session: null,
  isAuthenticated: false,
});

export const useUser = () => useContext(UserContext);

export const UserProvider = ({ children }: { children: React.ReactNode }) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [session, setSession] = useState<AuthSession | null>(null);
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    const checkAuth = async () => {
      try {
        const currentUser = await getCurrentUser();
        const currentSession = await fetchAuthSession();
        console.log("currentUser", currentUser);
        console.log("currentSession", currentSession);
        setUser(currentUser);
        setSession(currentSession);
        // If user is authenticated and trying to access auth pages, redirect to home.
        if (AUTH_PATHS.includes(pathname)) {
          router.replace("/home");
        }
      } catch {
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
    <UserContext.Provider value={{ user, session, isAuthenticated: !!user }}>
      {children}
    </UserContext.Provider>
  );
};

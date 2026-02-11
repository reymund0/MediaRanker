"use client";
import { createContext, useContext, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { getCurrentUser, AuthUser } from "aws-amplify/auth";

const AUTH_PATHS = ["/auth/login", "/auth/signup", "/auth/confirm-signup"];
const PUBLIC_PATHS = [ ...AUTH_PATHS, "/test"];

interface UserContextType {
  user: AuthUser | null;
  isAuthenticated: boolean;
}

const UserContext = createContext<UserContextType>({
  user: null,
  isAuthenticated: false,
});

export const useUser = () => useContext(UserContext);

export const UserProvider = ({ children }: { children: React.ReactNode }) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    const checkAuth = async () => {
      try {
        const currentUser = await getCurrentUser();
        console.log("currentUser", currentUser);
        setUser(currentUser);
        // If user is authenticated and trying to access auth pages, redirect to home.
        if (AUTH_PATHS.includes(pathname)) {
          router.replace("/home");
        }
      } catch {
        setUser(null);
        // If user is not authenticated and trying to access non-public pages, redirect to login.
        if (!PUBLIC_PATHS.includes(pathname)) {
          router.replace("/auth/login");
        }
      }
    }

    checkAuth();
  }, [pathname, router]);

  return (
    <UserContext.Provider value={{ user, isAuthenticated: !!user }}>
      {children}
    </UserContext.Provider>
  );
};

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api, tokenStore, type AuthResult } from "./api";

type Session = Omit<AuthResult, "token" | "refreshToken">;

type AuthContextValue = {
  session: Session | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);
const SESSION_KEY = "bpa_session";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);

  useEffect(() => {
    const stored = localStorage.getItem(SESSION_KEY);
    if (stored && tokenStore.access()) setSession(JSON.parse(stored));
  }, []);

  // The api client fires this when a refresh fails (tokens already cleared).
  useEffect(() => {
    const onSignout = () => {
      localStorage.removeItem(SESSION_KEY);
      setSession(null);
    };
    window.addEventListener("bpa:signout", onSignout);
    return () => window.removeEventListener("bpa:signout", onSignout);
  }, []);

  function persist(result: AuthResult) {
    const { token, refreshToken, ...rest } = result;
    tokenStore.set(token, refreshToken);
    localStorage.setItem(SESSION_KEY, JSON.stringify(rest));
    setSession(rest);
  }

  const value: AuthContextValue = {
    session,
    login: async (email, password) => persist(await api.login(email, password)),
    logout: async () => {
      const refresh = tokenStore.refresh();
      if (refresh) {
        try { await api.logout(refresh); } catch { /* best effort */ }
      }
      tokenStore.clear();
      localStorage.removeItem(SESSION_KEY);
      setSession(null);
    },
    changePassword: async (currentPassword, newPassword) => {
      await api.changePassword(currentPassword, newPassword);
      // The server revoked our other tokens but kept this session valid; clear the flag.
      if (session) {
        const updated = { ...session, mustChangePassword: false };
        localStorage.setItem(SESSION_KEY, JSON.stringify(updated));
        setSession(updated);
      }
    },
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}

export const ROLE_NAMES = ["Employee", "Manager", "Admin"];

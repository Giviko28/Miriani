import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api, tokenStore, type AuthResult } from "./api";

type Session = Omit<AuthResult, "token">;

type AuthContextValue = {
  session: Session | null;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string, role: number) => Promise<void>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);
const SESSION_KEY = "bpa_session";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);

  useEffect(() => {
    const stored = localStorage.getItem(SESSION_KEY);
    if (stored && tokenStore.get()) setSession(JSON.parse(stored));
  }, []);

  function persist(result: AuthResult) {
    const { token, ...rest } = result;
    tokenStore.set(token);
    localStorage.setItem(SESSION_KEY, JSON.stringify(rest));
    setSession(rest);
  }

  const value: AuthContextValue = {
    session,
    login: async (email, password) => persist(await api.login(email, password)),
    register: async (email, password, displayName, role) =>
      persist(await api.register(email, password, displayName, role)),
    logout: () => {
      tokenStore.clear();
      localStorage.removeItem(SESSION_KEY);
      setSession(null);
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

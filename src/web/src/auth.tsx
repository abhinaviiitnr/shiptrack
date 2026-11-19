import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import type { AuthResponse, Role } from "./api";

interface AuthContextValue {
  token: string | null;
  role: Role | null;
  userId: string | null;
  signIn: (auth: AuthResponse) => void;
  signOut: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function readAuth(): AuthResponse | null {
  const token = localStorage.getItem("shiptrack.token");
  const role = localStorage.getItem("shiptrack.role") as Role | null;
  const userId = localStorage.getItem("shiptrack.userId");
  return token && role && userId ? { token, role, userId } : null;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthResponse | null>(readAuth);
  const value = useMemo<AuthContextValue>(() => ({
    token: auth?.token ?? null,
    role: auth?.role ?? null,
    userId: auth?.userId ?? null,
    signIn: (next) => {
      localStorage.setItem("shiptrack.token", next.token);
      localStorage.setItem("shiptrack.role", next.role);
      localStorage.setItem("shiptrack.userId", next.userId);
      setAuth(next);
    },
    signOut: () => {
      localStorage.removeItem("shiptrack.token");
      localStorage.removeItem("shiptrack.role");
      localStorage.removeItem("shiptrack.userId");
      setAuth(null);
    },
  }), [auth]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error("useAuth must be used within AuthProvider");
  return value;
}

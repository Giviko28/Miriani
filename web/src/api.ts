// Thin client for the .NET API. All calls go through the gateway on :5080; the JWT is
// attached automatically. The frontend never talks to the Python AI service directly.

const API_BASE = "http://localhost:5080";
const TOKEN_KEY = "bpa_token";

export type Role = "Employee" | "Manager" | "Admin";
export const ROLE_LEVEL: Record<Role, number> = { Employee: 0, Manager: 1, Admin: 2 };

export type AuthResult = {
  token: string;
  email: string;
  displayName: string;
  role: number;
  expiresAt: string;
};

export type DocumentDto = {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: number; // 0 Uploaded, 1 Processing, 2 Indexed, 3 Failed
  accessRole: number;
  createdAt: string;
};

export type Source = {
  docId: string;
  fileName: string;
  chunkIndex: number;
  distance: number;
  text: string;
};

export type AgentResult = {
  route: string;
  answer: string;
  usedContext: boolean;
  sources: Source[];
  structured: Record<string, unknown> | null;
};

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (t: string) => localStorage.setItem(TOKEN_KEY, t),
  clear: () => localStorage.removeItem(TOKEN_KEY),
};

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = tokenStore.get();
  const headers = new Headers(options.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
  if (!res.ok) {
    let message = `Request failed (${res.status})`;
    try {
      const body = await res.json();
      message = body.error ?? body.detail ?? message;
    } catch {
      /* ignore non-JSON error bodies */
    }
    throw new Error(message);
  }
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

function jsonBody(data: unknown): RequestInit {
  return { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(data) };
}

export const api = {
  register: (email: string, password: string, displayName: string, role: number) =>
    request<AuthResult>("/api/auth/register", jsonBody({ email, password, displayName, role })),

  login: (email: string, password: string) =>
    request<AuthResult>("/api/auth/login", jsonBody({ email, password })),

  listDocuments: () => request<DocumentDto[]>("/api/documents"),

  uploadDocument: (file: File, accessRole: number) => {
    const form = new FormData();
    form.append("file", file);
    form.append("accessRole", String(accessRole));
    return request<DocumentDto>("/api/documents", { method: "POST", body: form });
  },

  runAgent: (query: string) => request<AgentResult>("/api/ai/agent", jsonBody({ query })),

  listAudit: () => request<AuditEntry[]>("/api/audit?take=100"),
};

export type AuditEntry = {
  id: number;
  userId: string | null;
  action: string;
  detail: string | null;
  createdAt: string;
};

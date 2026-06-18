// Thin client for the .NET API. All calls go through the gateway on :5080; the access token
// is attached automatically and silently refreshed when it expires. The frontend never talks
// to the Python AI service directly.

const API_BASE = "http://localhost:5080";
const ACCESS_KEY = "bpa_access";
const REFRESH_KEY = "bpa_refresh";

export type Role = "Employee" | "Manager" | "Admin";
export const ROLE_LEVEL: Record<Role, number> = { Employee: 0, Manager: 1, Admin: 2 };

export type AuthResult = {
  token: string;
  refreshToken: string;
  email: string;
  displayName: string;
  role: number;
  mustChangePassword: boolean;
  expiresAt: string;
};

type TokenPair = { token: string; refreshToken: string; expiresAt: string };

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

export type UserDto = {
  id: string;
  email: string;
  displayName: string;
  role: number;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
};

export type CreateUserResult = { user: UserDto; tempPassword: string };

export type FaqDto = { id: string; question: string; sortOrder: number };

export type ChatSessionSummary = { id: string; title: string; updatedAt: string };
export type ChatMessage = {
  id: string;
  sender: "user" | "assistant";
  content: string;
  route: string | null;
  usedContext: boolean;
  sources: string | null; // JSON array string
  structured: string | null; // JSON object string
  createdAt: string;
};
export type ChatThread = { id: string; title: string; messages: ChatMessage[] };
export type SendMessageResult = {
  sessionId: string;
  title: string;
  answer: string;
  route: string;
  usedContext: boolean;
  sources: string | null;
  structured: string | null;
};

export type AuditEntry = {
  id: number;
  userId: string | null;
  action: string;
  detail: string | null;
  createdAt: string;
};

export const tokenStore = {
  access: () => localStorage.getItem(ACCESS_KEY),
  refresh: () => localStorage.getItem(REFRESH_KEY),
  set: (access: string, refresh: string) => {
    localStorage.setItem(ACCESS_KEY, access);
    localStorage.setItem(REFRESH_KEY, refresh);
  },
  clear: () => {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
  },
};

// Single in-flight refresh shared across concurrent 401s, so we never refresh-storm.
let refreshing: Promise<boolean> | null = null;

function tryRefresh(): Promise<boolean> {
  const refresh = tokenStore.refresh();
  if (!refresh) return Promise.resolve(false);
  if (!refreshing) {
    refreshing = (async () => {
      try {
        const res = await fetch(`${API_BASE}/api/auth/refresh`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ refreshToken: refresh }),
        });
        if (!res.ok) return false;
        const pair = (await res.json()) as TokenPair;
        tokenStore.set(pair.token, pair.refreshToken);
        return true;
      } catch {
        return false;
      } finally {
        setTimeout(() => (refreshing = null), 0);
      }
    })();
  }
  return refreshing;
}

async function rawRequest(path: string, options: RequestInit): Promise<Response> {
  const headers = new Headers(options.headers);
  const token = tokenStore.access();
  if (token) headers.set("Authorization", `Bearer ${token}`);
  return fetch(`${API_BASE}${path}`, { ...options, headers });
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  let res = await rawRequest(path, options);

  // Access token expired: refresh once and retry.
  if (res.status === 401 && tokenStore.refresh()) {
    const ok = await tryRefresh();
    if (ok) {
      res = await rawRequest(path, options);
    } else {
      tokenStore.clear();
      window.dispatchEvent(new Event("bpa:signout"));
    }
  }

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
  // --- auth ---
  login: (email: string, password: string) =>
    request<AuthResult>("/api/auth/login", jsonBody({ email, password })),
  logout: (refreshToken: string) => request<void>("/api/auth/logout", jsonBody({ refreshToken })),
  changePassword: (currentPassword: string, newPassword: string) =>
    request<void>("/api/auth/change-password", jsonBody({ currentPassword, newPassword })),

  // --- documents (admin) ---
  listDocuments: () => request<DocumentDto[]>("/api/documents"),
  uploadDocument: (file: File, accessRole: number) => {
    const form = new FormData();
    form.append("file", file);
    form.append("accessRole", String(accessRole));
    return request<DocumentDto>("/api/documents", { method: "POST", body: form });
  },

  // --- AI ---
  runAgent: (query: string) => request<AgentResult>("/api/ai/agent", jsonBody({ query })),

  // --- users (admin) ---
  users: {
    list: () => request<UserDto[]>("/api/users"),
    create: (email: string, displayName: string, role: number) =>
      request<CreateUserResult>("/api/users", jsonBody({ email, displayName, role })),
    resetPassword: (id: string) =>
      request<{ tempPassword: string }>(`/api/users/${id}/reset-password`, { method: "POST" }),
    update: (id: string, patch: { role?: number; isActive?: boolean }) =>
      request<UserDto>(`/api/users/${id}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(patch),
      }),
    remove: (id: string) => request<void>(`/api/users/${id}`, { method: "DELETE" }),
  },

  // --- FAQs ---
  faqs: {
    list: () => request<FaqDto[]>("/api/faqs"),
    create: (question: string, sortOrder: number) =>
      request<FaqDto>("/api/faqs", jsonBody({ question, sortOrder })),
    update: (id: string, question: string, sortOrder: number) =>
      request<FaqDto>(`/api/faqs/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ question, sortOrder }),
      }),
    remove: (id: string) => request<void>(`/api/faqs/${id}`, { method: "DELETE" }),
  },

  // --- chat history ---
  chat: {
    listSessions: () => request<ChatSessionSummary[]>("/api/chat/sessions"),
    getSession: (id: string) => request<ChatThread>(`/api/chat/sessions/${id}`),
    sendMessage: (sessionId: string | null, query: string) =>
      request<SendMessageResult>("/api/chat/message", jsonBody({ sessionId, query })),
    deleteSession: (id: string) => request<void>(`/api/chat/sessions/${id}`, { method: "DELETE" }),
  },

  // --- audit (admin) ---
  listAudit: () => request<AuditEntry[]>("/api/audit?take=100"),
};

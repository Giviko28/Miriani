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

export type JiraStatus = { configured: boolean; site: string | null };
export type JiraIssueSummary = { key: string; summary: string; status: string; issueType: string };
export type JiraIssueDetail = JiraIssueSummary & { description: string };

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
  deleteDocument: (id: string) => request<void>(`/api/documents/${id}`, { method: "DELETE" }),

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

  // --- org database connector (admin) ---
  orgDb: {
    get: () => request<{ connected: boolean; dbType: string | null; schemaJson: string | null; updatedAt: string | null }>("/api/org-db"),
    save: (dbType: string, connectionString: string) =>
      request<{ connected: boolean; dbType: string | null; schemaJson: string | null; updatedAt: string | null }>(
        "/api/org-db", jsonBody({ dbType, connectionString })),
    explore: () => request<{ summary: string }>("/api/org-db/explore", { method: "POST" }),
    disconnect: () => request<void>("/api/org-db", { method: "DELETE" }),
  },

  // --- audit (admin) ---
  listAudit: () => request<AuditEntry[]>("/api/audit?take=100"),

  // --- Jira ticket intake (read-only) ---
  jira: {
    status: () => request<JiraStatus>("/api/jira/status"),
    list: (search?: string) =>
      request<JiraIssueSummary[]>(`/api/jira/issues${search ? `?search=${encodeURIComponent(search)}` : ""}`),
    get: (key: string) => request<JiraIssueDetail>(`/api/jira/issues/${encodeURIComponent(key)}`),
  },

  // --- business-process automations ---
  // Each takes the agent's structured chat output and triggers the real-world action.
  processes: {
    status: () => request<ProcessStatus>("/api/processes/status"),

    submitLeave: (d: any, managerEmail?: string) =>
      request<{ sent: boolean; to: string; calendarAttached: boolean }>(
        "/api/processes/leave/submit",
        jsonBody({
          employeeName: d.employee_name ?? d.employeeName ?? null,
          startDate: d.start_date ?? d.startDate,
          endDate: d.end_date ?? d.endDate,
          daysRequested: Number(d.days_requested ?? d.daysRequested ?? 0),
          managerEmail: managerEmail || null,
          policyNote: d.policy_note ?? d.policyNote ?? null,
          formalLetter: d.formal_letter ?? d.formalLetter ?? null,
        })),

    invoiceEmail: (d: any, clientEmail: string) =>
      request<{ sent: boolean; to: string }>("/api/processes/invoice/email", jsonBody(invoicePayload(d, clientEmail))),
    invoicePdf: (d: any) => downloadPdf("/api/processes/invoice/pdf", invoicePayload(d, null), `invoice-${d.client ?? "client"}.pdf`),

    createTicket: (d: any) =>
      request<{ key: string; url: string; simulated: boolean }>(
        "/api/processes/ticket/create",
        jsonBody({
          summary: d.summary,
          description: d.description ?? "",
          priority: d.priority ?? null,
          issueType: d.issue_type ?? d.issueType ?? "Task",
        })),

    provisionOnboarding: (d: any, newHireEmail?: string) =>
      request<{ tickets: string[]; simulated: boolean; emailSent: boolean }>(
        "/api/processes/onboarding/provision",
        jsonBody({
          role: d.role ?? null,
          employeeName: d.employee_name ?? d.employeeName ?? null,
          newHireEmail: newHireEmail || null,
          day_1: d.day_1 ?? [],
          week_1: d.week_1 ?? [],
          month_1: d.month_1 ?? [],
        })),

    contractAlert: (d: any) =>
      request<{ alerted: boolean; channel: string }>("/api/processes/contract/alert", jsonBody(contractPayload(d))),
    contractReport: (d: any) => downloadPdf("/api/processes/contract/report", contractPayload(d), "contract-risk-review.pdf"),
  },
};

export type ProcessStatus = { email: boolean; jira: boolean; jiraCanCreate: boolean; notifications: boolean };

function invoicePayload(d: any, clientEmail: string | null) {
  return {
    client: d.client ?? "Client",
    clientEmail,
    currency: d.currency ?? "GEL",
    notes: d.notes ?? null,
    items: (d.items ?? []).map((i: any) => ({
      description: i.description ?? "",
      quantity: Number(i.quantity ?? i.qty ?? 0),
      unitPrice: Number(i.unit_price ?? i.unitPrice ?? i.price ?? 0),
    })),
  };
}

function contractPayload(d: any) {
  return {
    title: d.title ?? null,
    overallRisk: d.overall_risk ?? d.overallRisk ?? "Unknown",
    clauses: (d.clauses ?? []).map((c: any) => ({ clause: c.clause, risk: c.risk, finding: c.finding })),
    recommendations: d.recommendations ?? [],
  };
}

// POST JSON and save the PDF response as a file download.
async function downloadPdf(path: string, body: unknown, fileName: string): Promise<void> {
  const res = await rawRequest(path, jsonBody(body));
  if (!res.ok) throw new Error(`Request failed (${res.status})`);
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

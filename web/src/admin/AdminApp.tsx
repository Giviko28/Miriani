import { useState } from "react";
import { ROLE_NAMES, useAuth } from "../auth";
import { Badge } from "../ui";
import { ChangePassword } from "../views/ChangePassword";
import { Overview } from "./pages/Overview";
import { AssistantPage } from "./pages/AssistantPage";
import { Users } from "./pages/Users";
import { Knowledge } from "./pages/Knowledge";
import { Faqs } from "./pages/Faqs";
import { Database } from "./pages/Database";
import { Audit } from "../views/Audit";

type Section = "overview" | "assistant" | "users" | "knowledge" | "faqs" | "database" | "audit" | "password";

const NAV: { key: Section; label: string }[] = [
  { key: "overview", label: "Overview" },
  { key: "assistant", label: "AI Assistant" },
  { key: "users", label: "Users" },
  { key: "knowledge", label: "Knowledge" },
  { key: "faqs", label: "FAQs" },
  { key: "database", label: "Database" },
  { key: "audit", label: "Audit" },
];

export function AdminApp() {
  const { session, logout } = useAuth();
  const [section, setSection] = useState<Section>("overview");

  return (
    <div className="flex min-h-screen bg-slate-50">
      <aside className="flex w-56 flex-col border-r border-slate-200 bg-white">
        <div className="px-5 py-4">
          <div className="font-semibold">BPA Admin</div>
          <div className="mt-0.5 text-xs text-slate-400">Console</div>
        </div>
        <nav className="flex-1 space-y-1 px-3">
          {NAV.map((n) => (
            <button
              key={n.key}
              onClick={() => setSection(n.key)}
              className={
                "block w-full rounded-md px-3 py-2 text-left text-sm font-medium " +
                (section === n.key ? "bg-slate-900 text-white" : "text-slate-600 hover:bg-slate-100")
              }
            >
              {n.label}
            </button>
          ))}
        </nav>
        <div className="border-t border-slate-200 px-5 py-4 text-sm">
          <div className="truncate font-medium text-slate-700">{session?.displayName}</div>
          <div className="mt-1"><Badge tone="blue">{ROLE_NAMES[session?.role ?? 0]}</Badge></div>
          <div className="mt-3 flex flex-col gap-1 text-xs">
            <button onClick={() => setSection("password")} className="text-left text-slate-500 hover:text-slate-800">
              Change password
            </button>
            <button onClick={() => logout()} className="text-left text-slate-500 hover:text-slate-800">
              Sign out
            </button>
          </div>
        </div>
      </aside>

      <main className="flex-1 overflow-auto">
        <div className="mx-auto max-w-4xl px-6 py-8">
          {section === "overview" && <Overview />}
          {section === "assistant" && <AssistantPage />}
          {section === "users" && <Users />}
          {section === "knowledge" && <Knowledge />}
          {section === "faqs" && <Faqs />}
          {section === "database" && <Database />}
          {section === "audit" && <Audit />}
          {section === "password" && (
            <div className="max-w-sm">
              <ChangePassword onDone={() => setSection("overview")} />
            </div>
          )}
        </div>
      </main>
    </div>
  );
}

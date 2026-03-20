import { useState } from "react";
import { AuthProvider, ROLE_NAMES, useAuth } from "./auth";
import { Badge } from "./ui";
import { Login } from "./views/Login";
import { Documents } from "./views/Documents";
import { Assistant } from "./views/Assistant";

type View = "assistant" | "documents";

function Shell() {
  const { session, logout } = useAuth();
  const [view, setView] = useState<View>("assistant");

  if (!session) return <Login />;

  const tabs: { key: View; label: string }[] = [
    { key: "assistant", label: "AI Assistant" },
    { key: "documents", label: "Documents" },
  ];

  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-4xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-6">
            <span className="font-semibold">BPA</span>
            <nav className="flex gap-1">
              {tabs.map((t) => (
                <button
                  key={t.key}
                  onClick={() => setView(t.key)}
                  className={
                    "rounded-md px-3 py-1.5 text-sm font-medium " +
                    (view === t.key ? "bg-slate-900 text-white" : "text-slate-600 hover:bg-slate-100")
                  }
                >
                  {t.label}
                </button>
              ))}
            </nav>
          </div>
          <div className="flex items-center gap-3 text-sm">
            <span className="text-slate-600">{session.displayName}</span>
            <Badge tone="blue">{ROLE_NAMES[session.role]}</Badge>
            <button onClick={logout} className="text-slate-500 hover:text-slate-800">Sign out</button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-4xl px-4 py-6">
        {view === "assistant" ? <Assistant /> : <Documents />}
      </main>
    </div>
  );
}

export default function App() {
  return (
    <AuthProvider>
      <Shell />
    </AuthProvider>
  );
}

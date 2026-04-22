import { useEffect, useState } from "react";
import { api } from "../api";
import { ROLE_NAMES, useAuth } from "../auth";
import { Badge } from "../ui";
import { Assistant } from "./Assistant";
import { ChangePassword } from "./ChangePassword";

export function Chat() {
  const { session, logout } = useAuth();
  const [faqs, setFaqs] = useState<string[]>([]);
  const [showPassword, setShowPassword] = useState(false);

  useEffect(() => {
    api.faqs.list().then((list) => setFaqs(list.map((f) => f.question))).catch(() => setFaqs([]));
  }, []);

  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-4 py-3">
          <span className="font-semibold">BPA Assistant</span>
          <div className="flex items-center gap-3 text-sm">
            <span className="text-slate-600">{session?.displayName}</span>
            <Badge tone="blue">{ROLE_NAMES[session?.role ?? 0]}</Badge>
            <button onClick={() => setShowPassword(true)} className="text-slate-500 hover:text-slate-800">
              Change password
            </button>
            <button onClick={() => logout()} className="text-slate-500 hover:text-slate-800">Sign out</button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-4 py-6">
        {showPassword ? (
          <div className="max-w-sm">
            <ChangePassword onDone={() => setShowPassword(false)} />
          </div>
        ) : (
          <Assistant suggestions={faqs.length > 0 ? faqs : undefined} sendOnClick={faqs.length > 0} />
        )}
      </main>
    </div>
  );
}

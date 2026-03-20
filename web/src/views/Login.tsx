import { useState, type FormEvent } from "react";
import { useAuth } from "../auth";
import { Button, Card, Input, Select } from "../ui";

export function Login() {
  const { login, register } = useAuth();
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [role, setRole] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      if (mode === "login") await login(email, password);
      else await register(email, password, displayName, role);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <h1 className="text-xl font-semibold">Business Process Automation</h1>
        <p className="mt-1 text-sm text-slate-500">
          {mode === "login" ? "Sign in to your workspace" : "Create an account"}
        </p>

        <form onSubmit={submit} className="mt-5 space-y-3">
          {mode === "register" && (
            <Input placeholder="Display name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
          )}
          <Input type="email" placeholder="Email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          <Input type="password" placeholder="Password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          {mode === "register" && (
            <Select value={role} onChange={(e) => setRole(Number(e.target.value))} className="w-full">
              <option value={0}>Employee</option>
              <option value={1}>Manager</option>
              <option value={2}>Admin</option>
            </Select>
          )}

          {error && <p className="text-sm text-red-600">{error}</p>}

          <Button type="submit" disabled={busy} className="w-full">
            {busy ? "Please wait…" : mode === "login" ? "Sign in" : "Register"}
          </Button>
        </form>

        <button
          className="mt-4 text-sm text-slate-500 hover:text-slate-800"
          onClick={() => { setMode(mode === "login" ? "register" : "login"); setError(null); }}
        >
          {mode === "login" ? "Need an account? Register" : "Have an account? Sign in"}
        </button>
      </Card>
    </div>
  );
}

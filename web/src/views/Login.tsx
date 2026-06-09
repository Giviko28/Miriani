import { useState, type FormEvent } from "react";
import { useAuth } from "../auth";
import { Button, Card, Input } from "../ui";

export function Login() {
  const { login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(email, password);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <div className="flex flex-col items-center text-center">
          <img src="/miriani-logo.png" alt="Miriani AI" className="h-20 w-20 object-contain" />
          <h1 className="mt-3 text-2xl font-bold tracking-tight text-slate-800">Miriani AI</h1>
          <p className="mt-1 text-sm text-slate-500">Your AI business-process assistant</p>
        </div>

        <form onSubmit={submit} className="mt-6 space-y-3">
          <Input type="email" placeholder="Email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          <Input type="password" placeholder="Password" value={password} onChange={(e) => setPassword(e.target.value)} required />

          {error && <p className="text-sm text-red-600">{error}</p>}

          <Button type="submit" disabled={busy} className="w-full">
            {busy ? "Please wait…" : "Sign in"}
          </Button>
        </form>

        <p className="mt-4 text-xs text-slate-400">
          Accounts are created by your administrator. Contact them if you need access.
        </p>
      </Card>
    </div>
  );
}

import { useState, type FormEvent } from "react";
import { useAuth } from "../auth";
import { Button, Card, Input } from "../ui";

/**
 * Password-change screen. Used both as a forced first-login step (when
 * `forced` is set) and as a voluntary change from the user/admin menu.
 */
export function ChangePassword({ forced = false, onDone }: { forced?: boolean; onDone?: () => void }) {
  const { changePassword, logout } = useAuth();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (next.length < 8) return setError("New password must be at least 8 characters.");
    if (next !== confirm) return setError("New passwords do not match.");
    setBusy(true);
    try {
      await changePassword(current, next);
      onDone?.();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not change password");
    } finally {
      setBusy(false);
    }
  }

  const body = (
    <Card className="w-full max-w-sm">
      <h1 className="text-xl font-semibold">{forced ? "Set a new password" : "Change password"}</h1>
      <p className="mt-1 text-sm text-slate-500">
        {forced
          ? "Your account uses a temporary password. Choose a new one to continue."
          : "Update your account password."}
      </p>

      <form onSubmit={submit} className="mt-5 space-y-3">
        <Input
          type="password"
          placeholder={forced ? "Temporary password" : "Current password"}
          value={current}
          onChange={(e) => setCurrent(e.target.value)}
          required
        />
        <Input type="password" placeholder="New password" value={next} onChange={(e) => setNext(e.target.value)} required />
        <Input type="password" placeholder="Confirm new password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required />

        {error && <p className="text-sm text-red-600">{error}</p>}

        <Button type="submit" disabled={busy} className="w-full">
          {busy ? "Saving…" : "Save password"}
        </Button>
      </form>

      <button
        className="mt-4 text-sm text-slate-500 hover:text-slate-800"
        onClick={() => (forced ? logout() : onDone?.())}
      >
        {forced ? "Sign out" : "Cancel"}
      </button>
    </Card>
  );

  // Forced flow is full-screen; voluntary flow renders inline where placed.
  return forced ? <div className="flex min-h-screen items-center justify-center p-4">{body}</div> : body;
}

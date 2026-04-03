import { useEffect, useState } from "react";
import { api, type CreateUserResult, type UserDto } from "../../api";
import { ROLE_NAMES } from "../../auth";
import { Badge, Button, Card, Input, Select } from "../../ui";

export function Users() {
  const [users, setUsers] = useState<UserDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [credential, setCredential] = useState<{ email: string; password: string } | null>(null);

  async function refresh() {
    try {
      setUsers(await api.users.list());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load users");
    }
  }

  useEffect(() => { refresh(); }, []);

  async function act(fn: () => Promise<unknown>) {
    setError(null);
    try {
      await fn();
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Action failed");
    }
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Users</h1>
        <Button onClick={() => setShowCreate(true)}>+ Create user</Button>
      </div>
      {error && <p className="text-sm text-red-600">{error}</p>}

      {credential && (
        <Card className="border-green-200 bg-green-50">
          <h2 className="font-semibold text-green-800">Temporary password</h2>
          <p className="mt-1 text-sm text-green-700">
            Share this with <strong>{credential.email}</strong>. It is shown only once; they must
            change it on first sign-in.
          </p>
          <div className="mt-3 flex items-center gap-3">
            <code className="rounded bg-white px-3 py-2 font-mono text-sm">{credential.password}</code>
            <button
              className="text-sm text-green-700 underline"
              onClick={() => navigator.clipboard?.writeText(credential.password)}
            >
              Copy
            </button>
            <button className="ml-auto text-sm text-slate-500" onClick={() => setCredential(null)}>
              Dismiss
            </button>
          </div>
        </Card>
      )}

      <Card>
        {users.length === 0 ? (
          <p className="text-sm text-slate-500">No users yet.</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-slate-500">
                <th className="py-2">Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Status</th>
                <th className="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id} className="border-t border-slate-100 align-middle">
                  <td className="py-2 font-medium">{u.displayName}</td>
                  <td className="text-slate-600">{u.email}</td>
                  <td>
                    <Select
                      value={u.role}
                      onChange={(e) => act(() => api.users.update(u.id, { role: Number(e.target.value) }))}
                    >
                      {ROLE_NAMES.map((r, i) => (
                        <option key={r} value={i}>{r}</option>
                      ))}
                    </Select>
                  </td>
                  <td>
                    {u.isActive ? <Badge tone="green">Active</Badge> : <Badge tone="red">Disabled</Badge>}
                    {u.mustChangePassword && <span className="ml-1"><Badge tone="amber">temp pw</Badge></span>}
                  </td>
                  <td className="space-x-2 py-2 text-right text-xs">
                    <button
                      className="text-slate-600 hover:text-slate-900"
                      onClick={() =>
                        act(async () => {
                          const r = await api.users.resetPassword(u.id);
                          setCredential({ email: u.email, password: r.tempPassword });
                        })
                      }
                    >
                      Reset pw
                    </button>
                    <button
                      className="text-slate-600 hover:text-slate-900"
                      onClick={() => act(() => api.users.update(u.id, { isActive: !u.isActive }))}
                    >
                      {u.isActive ? "Disable" : "Enable"}
                    </button>
                    <button
                      className="text-red-600 hover:text-red-800"
                      onClick={() => { if (confirm(`Delete ${u.email}?`)) act(() => api.users.remove(u.id)); }}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      {showCreate && (
        <CreateUserModal
          onClose={() => setShowCreate(false)}
          onCreated={(r) => { setCredential({ email: r.user.email, password: r.tempPassword }); setShowCreate(false); refresh(); }}
        />
      )}
    </div>
  );
}

function CreateUserModal({ onClose, onCreated }: { onClose: () => void; onCreated: (r: CreateUserResult) => void }) {
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [role, setRole] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      onCreated(await api.users.create(email.trim(), displayName.trim(), role));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not create user");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="fixed inset-0 z-10 flex items-center justify-center bg-black/30 p-4" onClick={onClose}>
      <Card className="w-full max-w-sm" >
        <div onClick={(e) => e.stopPropagation()}>
          <h2 className="text-lg font-semibold">Create user</h2>
          <p className="mt-1 text-sm text-slate-500">A temporary password is generated for them.</p>
          <div className="mt-4 space-y-3">
            <Input placeholder="Display name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
            <Input type="email" placeholder="Email" value={email} onChange={(e) => setEmail(e.target.value)} />
            <Select value={role} onChange={(e) => setRole(Number(e.target.value))} className="w-full">
              {ROLE_NAMES.map((r, i) => (<option key={r} value={i}>{r}</option>))}
            </Select>
            {error && <p className="text-sm text-red-600">{error}</p>}
            <div className="flex justify-end gap-2">
              <button className="text-sm text-slate-500" onClick={onClose}>Cancel</button>
              <Button onClick={submit} disabled={busy || !email || !displayName}>{busy ? "Creating…" : "Create"}</Button>
            </div>
          </div>
        </div>
      </Card>
    </div>
  );
}

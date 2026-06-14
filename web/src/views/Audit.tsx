import { useEffect, useState } from "react";
import { api, type AuditEntry } from "../api";
import { Badge, Card } from "../ui";

const ACTION_TONE: Record<string, "slate" | "blue" | "green" | "amber"> = {
  "user.login": "slate",
  "user.register": "blue",
  "document.upload": "amber",
  "ai.query": "green",
  "ai.agent": "green",
};

export function Audit() {
  const [entries, setEntries] = useState<AuditEntry[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.listAudit().then(setEntries).catch((e) =>
      setError(e instanceof Error ? e.message : "Failed to load audit log"),
    );
  }, []);

  return (
    <Card>
      <h2 className="text-lg font-semibold">Audit log</h2>
      <p className="mt-1 text-sm text-slate-500">
        Recent actions across your organization (Admin only).
      </p>
      {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
      {entries.length === 0 && !error ? (
        <p className="mt-3 text-sm text-slate-500">No activity yet.</p>
      ) : (
        <table className="mt-3 w-full text-sm">
          <thead>
            <tr className="text-left text-slate-500">
              <th className="py-2">Action</th>
              <th>Detail</th>
              <th>When</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((e) => (
              <tr key={e.id} className="border-t border-slate-100">
                <td className="py-2"><Badge tone={ACTION_TONE[e.action] ?? "slate"}>{e.action}</Badge></td>
                <td className="text-slate-600">{e.detail}</td>
                <td className="whitespace-nowrap text-slate-400">{new Date(e.createdAt).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Card>
  );
}

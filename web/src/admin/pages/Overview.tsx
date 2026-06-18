import { useEffect, useState } from "react";
import { api, type AuditEntry } from "../../api";
import { Card } from "../../ui";

function Stat({ label, value }: { label: string; value: number | string }) {
  return (
    <Card>
      <div className="text-3xl font-semibold text-slate-900">{value}</div>
      <div className="mt-1 text-sm text-slate-500">{label}</div>
    </Card>
  );
}

export function Overview() {
  const [counts, setCounts] = useState({ users: 0, docs: 0, faqs: 0, audit: 0 });
  const [recent, setRecent] = useState<AuditEntry[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const [users, docs, faqs, audit] = await Promise.all([
          api.users.list(),
          api.listDocuments(),
          api.faqs.list(),
          api.listAudit(),
        ]);
        setCounts({ users: users.length, docs: docs.length, faqs: faqs.length, audit: audit.length });
        setRecent(audit.slice(0, 8));
      } catch (e) {
        setError(e instanceof Error ? e.message : "Failed to load overview");
      }
    })();
  }, []);

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Overview</h1>
      {error && <p className="text-sm text-red-600">{error}</p>}

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <Stat label="Users" value={counts.users} />
        <Stat label="Documents" value={counts.docs} />
        <Stat label="FAQs" value={counts.faqs} />
        <Stat label="Audit entries" value={counts.audit} />
      </div>

      <Card>
        <h2 className="text-lg font-semibold">Recent activity</h2>
        {recent.length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">No activity yet.</p>
        ) : (
          <ul className="mt-3 space-y-2 text-sm">
            {recent.map((e) => (
              <li key={e.id} className="flex items-center justify-between border-t border-slate-100 py-2 first:border-0">
                <span className="font-medium text-slate-700">{e.action}</span>
                <span className="truncate px-3 text-slate-500">{e.detail}</span>
                <span className="whitespace-nowrap text-xs text-slate-400">{new Date(e.createdAt).toLocaleString()}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

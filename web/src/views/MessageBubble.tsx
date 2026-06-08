import { useState } from "react";
import { api, type ChatMessage } from "../api";


export const ROUTE_LABELS: Record<string, string> = {
  greeting:      "Miriani",
  policy_qa:     "Policy Q&A",
  email_draft:   "Email Draft",
  ticket_triage: "IT Helpdesk",
  ticket_advice: "Helpdesk Advisor",
  db_query:      "Incident Report",
};

type ParsedSource = { fileName: string; distance: number; text: string };

function parseSources(json: string | null): ParsedSource[] {
  if (!json) return [];
  try {
    const arr = JSON.parse(json) as Array<{ FileName?: string; fileName?: string; Distance?: number; distance?: number; Text?: string; text?: string }>;
    return arr.map((s) => ({
      fileName: s.fileName ?? s.FileName ?? "source",
      distance: s.distance ?? s.Distance ?? 0,
      text: s.text ?? s.Text ?? "",
    }));
  } catch {
    return [];
  }
}

function parseStructured(json: string | null): unknown | null {
  if (!json) return null;
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
}


function DbQueryCard({ data }: { data: any }) {
  const rows: Record<string, unknown>[] = data.rows ?? [];
  const sql: string = data.sql ?? "";
  const total: number = data.total_rows ?? rows.length;
  const headers = rows.length > 0 ? Object.keys(rows[0]) : [];
  return (
    <div className="mt-4 space-y-3">
      {sql && (
        <details className="rounded-md border border-slate-200">
          <summary className="cursor-pointer px-3 py-2 text-xs font-medium text-slate-700">SQL Query</summary>
          <pre className="whitespace-pre-wrap px-3 pb-3 pt-1 text-xs text-slate-500">{sql}</pre>
        </details>
      )}
      {rows.length > 0 ? (
        <div className="overflow-auto">
          <p className="mb-1 text-xs text-slate-400">{total} row{total !== 1 ? "s" : ""}{total > 20 ? " (showing first 20)" : ""}</p>
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-left text-slate-500">
                {headers.map(h => <th key={h} className="pb-1 pr-4 font-medium">{h}</th>)}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, i) => (
                <tr key={i} className="border-b border-slate-100">
                  {headers.map(h => (
                    <td key={h} className="py-1.5 pr-4 text-slate-700">{String(row[h] ?? "")}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="text-xs text-slate-400">No rows returned.</p>
      )}
    </div>
  );
}

const PRIORITY_ICONS: Record<string, { icon: string; color: string }> = {
  Critical: { icon: "⬆⬆", color: "text-red-600" },
  High:     { icon: "⬆",   color: "text-orange-500" },
  Medium:   { icon: "▶",   color: "text-yellow-500" },
  Low:      { icon: "⬇",   color: "text-blue-400" },
};

function TicketCard({ data }: { data: any }) {
  const priority: string = data.priority ?? "Medium";
  const p = PRIORITY_ICONS[priority] ?? { icon: "▶", color: "text-slate-400" };

  return (
    <div className="mt-4 rounded-lg border border-slate-200 overflow-hidden">
      {/* Header bar */}
      <div className="flex items-center gap-2 border-b border-slate-200 bg-slate-50 px-4 py-2.5">
        <svg viewBox="0 0 24 24" className="h-4 w-4 text-blue-600 shrink-0" fill="currentColor">
          <path d="M13 10V3L4 14h7v7l9-11h-7z"/>
        </svg>
        <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">IT Support Ticket</span>
      </div>

      <div className="px-4 py-3 space-y-3 bg-white">
        {/* Summary / title */}
        {data.summary && (
          <p className="text-sm font-semibold text-slate-900 leading-snug">{data.summary}</p>
        )}

        {/* Metadata fields */}
        <div className="grid grid-cols-2 gap-x-6 gap-y-2 text-xs">
          <div>
            <span className="block text-slate-400 mb-0.5">Priority</span>
            <span className={`font-medium ${p.color}`}>{p.icon} {priority}</span>
          </div>
          {data.issue_type && (
            <div>
              <span className="block text-slate-400 mb-0.5">Type</span>
              <span className="font-medium text-slate-700">{data.issue_type}</span>
            </div>
          )}
          {data.category && (
            <div>
              <span className="block text-slate-400 mb-0.5">Category</span>
              <span className="font-medium text-slate-700">{data.category}</span>
            </div>
          )}
        </div>

        {/* Description */}
        {data.description && (
          <div>
            <span className="block text-xs text-slate-400 mb-1">Description</span>
            <p className="text-xs text-slate-600 leading-relaxed border-l-2 border-slate-200 pl-3">{data.description}</p>
          </div>
        )}
      </div>
    </div>
  );
}

function StructuredCard({ route, data }: { route: string; data: unknown }) {
  if (route === "db_query") return <DbQueryCard data={data} />;
  if (route === "ticket_triage") return <TicketCard data={data} />;
  return (
    <pre className="mt-4 overflow-auto rounded-md bg-slate-900 p-4 text-xs text-slate-100">
      {JSON.stringify(data, null, 2)}
    </pre>
  );
}

// --- action buttons: turn a structured chat result into a real-world action ---

function ActionButton({ label, onClick, busy, tone = "blue", icon }: { label: string; onClick: () => void; busy: boolean; tone?: "blue" | "slate" | "green" | "outline"; icon?: string }) {
  const cls =
    tone === "blue"    ? "bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-700 hover:to-indigo-700 shadow-blue-500/25 text-white shadow-md hover:shadow-lg" :
    tone === "green"   ? "bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-700 hover:to-teal-700 shadow-emerald-500/25 text-white shadow-md hover:shadow-lg" :
    tone === "slate"   ? "bg-gradient-to-r from-slate-700 to-slate-800 hover:from-slate-800 hover:to-slate-900 shadow-slate-500/25 text-white shadow-md hover:shadow-lg" :
    /* outline */        "border border-slate-300 bg-white hover:bg-slate-50 text-slate-700";
  return (
    <button
      onClick={onClick}
      disabled={busy}
      className={`inline-flex items-center gap-1.5 rounded-lg px-4 py-2 text-xs font-semibold transition-all duration-150 active:scale-[0.97] disabled:opacity-50 disabled:pointer-events-none ${cls}`}
    >
      {busy ? (
        <>
          <span className="inline-block h-3.5 w-3.5 animate-spin rounded-full border-2 border-current/30 border-t-current" />
          Processing…
        </>
      ) : (
        <>{icon && <span>{icon}</span>}{label}</>
      )}
    </button>
  );
}

function ProcessActions({ route, data }: { route: string; data: any }) {
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (route !== "ticket_triage") return null;

  async function run(fn: () => Promise<string>) {
    setBusy(true); setError(null); setResult(null);
    try { setResult(await fn()); }
    catch (e) { setError(e instanceof Error ? e.message : "Action failed"); }
    finally { setBusy(false); }
  }

  return (
    <div className="mt-4 border-t border-slate-100 pt-4">
      <div className="flex flex-wrap items-center gap-3">
        <ActionButton label="Create Jira ticket" tone="outline" busy={busy}
          onClick={() => run(async () => {
            const r = await api.processes.createTicket(data);
            return `✓ Created ${r.key}${r.simulated ? " (local demo ticket)" : ""}.`;
          })} />
      </div>
      {result && (
        <div className="mt-3 flex items-center gap-2 rounded-lg bg-emerald-50 px-3 py-2 animate-fade-in">
          <span className="text-emerald-600 text-sm">✓</span>
          <p className="text-xs font-medium text-emerald-700">{result}</p>
        </div>
      )}
      {error && (
        <div className="mt-3 flex items-center gap-2 rounded-lg bg-red-50 px-3 py-2 animate-fade-in">
          <span className="text-red-500 text-sm">✕</span>
          <p className="text-xs font-medium text-red-600">{error}</p>
        </div>
      )}
    </div>
  );
}

export function MessageBubble({ message }: { message: ChatMessage }) {
  if (message.sender === "user") {
    return (
      <div className="flex justify-end animate-fade-in">
        <div className="max-w-[78%] whitespace-pre-wrap rounded-2xl rounded-tr-sm bg-blue-600 px-4 py-3 text-sm text-white">
          {message.content}
        </div>
      </div>
    );
  }

  const sources = parseSources(message.sources);
  const structured = parseStructured(message.structured);
  const routeLabel = message.route ? (ROUTE_LABELS[message.route] ?? message.route) : null;

  return (
    <div className="flex items-start gap-3 animate-fade-in sm:mr-16">
      {/* Avatar */}
      <div className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-800 text-xs font-semibold text-white select-none">
        M
      </div>

      {/* Bubble */}
      <div className="flex-1 rounded-2xl rounded-tl-sm border border-slate-200 bg-white px-4 py-3">
        {/* sender + route */}
        <div className="mb-2 flex items-center gap-2">
          <span className="text-xs font-semibold text-slate-800">Miriani</span>
          {routeLabel && (
            <span className="text-xs text-slate-400">· {routeLabel}</span>
          )}
        </div>

        <p className="whitespace-pre-wrap text-sm leading-relaxed text-slate-800">{message.content}</p>

        {structured !== null && (
          <>
            <StructuredCard route={message.route ?? ""} data={structured} />
            <ProcessActions route={message.route ?? ""} data={structured} />
          </>
        )}

        {sources.length > 0 && (
          <details className="mt-4 group">
            <summary className="flex cursor-pointer items-center gap-1.5 text-xs text-slate-400 hover:text-slate-600 transition-colors select-none w-fit">
              <svg className="h-3 w-3 transition-transform group-open:rotate-90" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M7.21 14.77a.75.75 0 01.02-1.06L11.168 10 7.23 6.29a.75.75 0 111.04-1.08l4.5 4.25a.75.75 0 010 1.08l-4.5 4.25a.75.75 0 01-1.06-.02z" clipRule="evenodd" />
              </svg>
              {sources.length} source{sources.length !== 1 ? "s" : ""}
            </summary>
            <ul className="mt-2 space-y-1.5">
              {sources.map((s, i) => (
                <li key={i} className="rounded-md border border-slate-100 bg-slate-50 p-3 text-xs">
                  <div className="font-medium text-slate-600">{s.fileName}</div>
                  <p className="mt-0.5 line-clamp-2 text-slate-400">{s.text}</p>
                </li>
              ))}
            </ul>
          </details>
        )}
      </div>
    </div>
  );
}

import { useState } from "react";
import { api, type AgentResult } from "../api";
import { Badge, Button, Card } from "../ui";

const ROUTE_LABELS: Record<string, string> = {
  policy_qa: "Policy Q&A",
  doc_summary: "Summarizer",
  email_draft: "Email Drafter",
  report_draft: "Report Drafter",
  invoice_gen: "Invoice Generator",
};

const EXAMPLES = [
  "How many days can I work remotely?",
  "Summarize the remote work policy",
  "Write an email announcing the new policy to the team",
  "Create an invoice for ACME: 10 hours consulting at 150 GEL",
];

/**
 * The AI assistant. By default it shows static example chips that fill the input.
 * Pass `suggestions` (e.g. admin-curated FAQs) with `sendOnClick` to make the chips
 * submit immediately.
 */
export function Assistant({ suggestions, sendOnClick = false }: { suggestions?: string[]; sendOnClick?: boolean }) {
  const [query, setQuery] = useState("");
  const [result, setResult] = useState<AgentResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const chips = suggestions ?? EXAMPLES;

  async function run(q: string) {
    if (!q.trim()) return;
    setBusy(true);
    setError(null);
    setResult(null);
    try {
      setResult(await api.runAgent(q));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Request failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-5">
      <Card>
        <h2 className="text-lg font-semibold">AI Assistant</h2>
        <p className="mt-1 text-sm text-slate-500">
          Ask a question or request a task. The router picks the right agent, and answers are
          grounded in documents your role can access.
        </p>
        <textarea
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          rows={3}
          placeholder="e.g. Summarize the remote work policy"
          className="mt-4 w-full rounded-md border border-slate-300 p-3 text-sm outline-none focus:border-slate-500"
        />
        {chips.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-2">
            {chips.map((ex) => (
              <button
                key={ex}
                onClick={() => (sendOnClick ? (setQuery(ex), run(ex)) : setQuery(ex))}
                disabled={busy}
                className="rounded-full border border-slate-200 px-3 py-1 text-xs text-slate-600 hover:bg-slate-100 disabled:opacity-50"
              >
                {ex}
              </button>
            ))}
          </div>
        )}
        <div className="mt-4">
          <Button onClick={() => run(query)} disabled={busy}>{busy ? "Thinking…" : "Send"}</Button>
        </div>
        {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
      </Card>

      {result && (
        <Card>
          <div className="flex items-center gap-2">
            <Badge tone="blue">{ROUTE_LABELS[result.route] ?? result.route}</Badge>
            {result.usedContext && <Badge tone="green">grounded</Badge>}
          </div>

          <p className="mt-4 whitespace-pre-wrap text-sm leading-relaxed">{result.answer}</p>

          {result.structured && (
            <pre className="mt-4 overflow-auto rounded-md bg-slate-900 p-4 text-xs text-slate-100">
              {JSON.stringify(result.structured, null, 2)}
            </pre>
          )}

          {result.sources.length > 0 && (
            <div className="mt-5">
              <h3 className="text-sm font-semibold text-slate-700">Sources</h3>
              <ul className="mt-2 space-y-2">
                {result.sources.map((s, i) => (
                  <li key={i} className="rounded-md border border-slate-100 bg-slate-50 p-3 text-xs">
                    <div className="font-medium text-slate-700">
                      {s.fileName} <span className="text-slate-400">· distance {s.distance.toFixed(3)}</span>
                    </div>
                    <p className="mt-1 line-clamp-2 text-slate-500">{s.text}</p>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </Card>
      )}
    </div>
  );
}

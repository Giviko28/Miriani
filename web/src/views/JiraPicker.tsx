import { useEffect, useState } from "react";
import { api, type JiraIssueSummary } from "../api";
import { Badge, Button, Input } from "../ui";

/**
 * Modal that lists/searches Jira tickets and hands the chosen one back to the chat. Selecting a
 * ticket fetches its description and the parent formats it into the message box, so the user can
 * tweak the question before sending it to the assistant.
 */
export function JiraPicker({
  onPick,
  onClose,
}: {
  onPick: (issue: { key: string; summary: string; description: string }) => void;
  onClose: () => void;
}) {
  const [search, setSearch] = useState("");
  const [issues, setIssues] = useState<JiraIssueSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [picking, setPicking] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Debounced search: reload the list ~300ms after the user stops typing.
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    const t = setTimeout(async () => {
      try {
        const list = await api.jira.list(search.trim() || undefined);
        if (!cancelled) setIssues(list);
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : "Failed to load tickets");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }, 300);
    return () => {
      cancelled = true;
      clearTimeout(t);
    };
  }, [search]);

  async function choose(key: string, summary: string) {
    setPicking(key);
    setError(null);
    try {
      const detail = await api.jira.get(key);
      onPick({ key: detail.key, summary: detail.summary || summary, description: detail.description });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load ticket");
      setPicking(null);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4"
      onClick={onClose}
    >
      <div
        className="flex max-h-[80vh] w-full max-w-lg flex-col overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3">
          <h3 className="text-sm font-semibold text-slate-800">Add a Jira ticket</h3>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-700" title="Close">
            ×
          </button>
        </div>

        <div className="p-4">
          <Input
            autoFocus
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by key or summary…"
          />
        </div>

        {error && <p className="px-4 pb-2 text-sm text-red-600">{error}</p>}

        <div className="flex-1 overflow-auto px-2 pb-3">
          {loading ? (
            <p className="px-2 py-2 text-sm text-slate-400">Loading tickets…</p>
          ) : issues.length === 0 ? (
            <p className="px-2 py-2 text-sm text-slate-400">No matching tickets.</p>
          ) : (
            issues.map((i) => (
              <button
                key={i.key}
                onClick={() => choose(i.key, i.summary)}
                disabled={picking !== null}
                className="flex w-full items-start gap-2 rounded-md px-2 py-2 text-left hover:bg-slate-50 disabled:opacity-50"
              >
                <span className="mt-0.5 shrink-0 font-mono text-xs text-slate-500">{i.key}</span>
                <span className="flex-1 text-sm text-slate-700">
                  {i.summary}
                  <span className="ml-2 inline-flex gap-1 align-middle">
                    {i.issueType && <Badge tone="blue">{i.issueType}</Badge>}
                    {i.status && <Badge tone="slate">{i.status}</Badge>}
                  </span>
                </span>
                {picking === i.key && <span className="text-xs text-slate-400">…</span>}
              </button>
            ))
          )}
        </div>

        <div className="flex justify-end border-t border-slate-200 px-4 py-3">
          <Button onClick={onClose} className="bg-slate-200 text-slate-700 hover:bg-slate-300">
            Cancel
          </Button>
        </div>
      </div>
    </div>
  );
}

/** Format a chosen ticket into a chat prompt the assistant can act on. */
export function formatTicketPrompt(issue: { key: string; summary: string; description: string }) {
  const lines = [
    `Help me with Jira ticket ${issue.key}: ${issue.summary}`,
  ];
  if (issue.description) {
    lines.push("", "Ticket details:", issue.description);
  }
  lines.push("", "Based on our company knowledge, how should I approach this?");
  return lines.join("\n");
}

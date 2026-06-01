import { useEffect, useState } from "react";
import { api, type JiraIssueDetail, type JiraIssueSummary } from "../api";
import { Badge, Input } from "../ui";

/**
 * Read-only Jira ticket browser for employees: a searchable list on the left, the selected
 * ticket's details on the right. Viewing only — to act on a ticket, the AI Assistant tab has
 * the "+ Jira" button that pulls it into chat.
 */
export function JiraTickets() {
  const [configured, setConfigured] = useState<boolean | null>(null);
  const [site, setSite] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [issues, setIssues] = useState<JiraIssueSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<JiraIssueDetail | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.jira
      .status()
      .then((s) => {
        setConfigured(s.configured);
        setSite(s.site);
      })
      .catch(() => setConfigured(false));
  }, []);

  // Debounced search once we know Jira is on.
  useEffect(() => {
    if (!configured) return;
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
  }, [search, configured]);

  async function open(key: string) {
    setLoadingDetail(true);
    setError(null);
    try {
      setSelected(await api.jira.get(key));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load ticket");
    } finally {
      setLoadingDetail(false);
    }
  }

  if (configured === false) {
    return (
      <div className="rounded-xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">
        Jira isn't connected yet. Ask an administrator to configure it.
      </div>
    );
  }

  return (
    <div className="flex h-full overflow-hidden bg-white">
      {/* Ticket list */}
      <div className="flex w-72 flex-col border-r border-slate-200">
        <div className="border-b border-slate-200 p-3">
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by key or summary…"
          />
        </div>
        <div className="flex-1 overflow-auto p-2">
          {loading ? (
            <p className="px-2 py-2 text-sm text-slate-400">Loading tickets…</p>
          ) : issues.length === 0 ? (
            <p className="px-2 py-2 text-sm text-slate-400">No matching tickets.</p>
          ) : (
            issues.map((i) => (
              <button
                key={i.key}
                onClick={() => open(i.key)}
                className={
                  "mb-1 flex w-full flex-col gap-1 rounded-md px-2 py-2 text-left " +
                  (selected?.key === i.key ? "bg-slate-100" : "hover:bg-slate-50")
                }
              >
                <span className="flex items-center gap-2">
                  <span className="font-mono text-xs text-slate-500">{i.key}</span>
                  {i.status && <Badge tone="slate">{i.status}</Badge>}
                </span>
                <span className="text-sm text-slate-700">{i.summary}</span>
              </button>
            ))
          )}
        </div>
      </div>

      {/* Detail pane */}
      <div className="flex-1 overflow-auto p-6">
        {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
        {loadingDetail ? (
          <p className="text-sm text-slate-400">Loading ticket…</p>
        ) : !selected ? (
          <p className="text-sm text-slate-400">Select a ticket to view its details.</p>
        ) : (
          <article className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-mono text-sm text-slate-500">{selected.key}</span>
              {selected.issueType && <Badge tone="blue">{selected.issueType}</Badge>}
              {selected.status && <Badge tone="slate">{selected.status}</Badge>}
              {site && (
                <a
                  href={`${site.replace(/\/$/, "")}/browse/${selected.key}`}
                  target="_blank"
                  rel="noreferrer"
                  className="ml-auto text-xs text-blue-600 hover:underline"
                >
                  Open in Jira ↗
                </a>
              )}
            </div>
            <h2 className="text-lg font-semibold text-slate-800">{selected.summary}</h2>
            {selected.description ? (
              <pre className="whitespace-pre-wrap break-words font-sans text-sm leading-relaxed text-slate-700">
                {selected.description}
              </pre>
            ) : (
              <p className="text-sm text-slate-400">No description.</p>
            )}
          </article>
        )}
      </div>
    </div>
  );
}

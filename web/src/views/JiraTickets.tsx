import { useEffect, useState } from "react";
import {
  api,
  type AgentResult,
  type JiraIssueDetail,
  type JiraIssueSummary,
  type JiraTransition,
  type JiraUser,
  type ManagerDto,
  type ProcessStatus,
} from "../api";
import { Badge, Button, Input, Select } from "../ui";

/**
 * Jira ticket workspace: a searchable list on the left and, for the selected ticket, a two-column
 * cockpit — the ticket itself (detail, assignee, status, comments, all writable to live Jira) and
 * Miriani's panel (ask questions about the ticket + draft-and-send a Slack alert, a manager email,
 * or a PDF report). Every AI move is drafted first, reviewed/edited, then sent.
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
  const [procStatus, setProcStatus] = useState<ProcessStatus | null>(null);

  useEffect(() => {
    api.jira
      .status()
      .then((s) => {
        setConfigured(s.configured);
        setSite(s.site);
      })
      .catch(() => setConfigured(false));
    api.processes.status().then(setProcStatus).catch(() => setProcStatus(null));
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

  // Re-fetch the open ticket after a write so the UI reflects live Jira state.
  async function refreshSelected() {
    if (selected) {
      try {
        setSelected(await api.jira.get(selected.key));
      } catch {
        /* keep the stale view rather than blanking it */
      }
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
      <div className="flex w-72 shrink-0 flex-col border-r border-slate-200">
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

      {/* Workspace */}
      <div className="flex-1 overflow-auto">
        {error && <p className="px-6 pt-4 text-sm text-red-600">{error}</p>}
        {loadingDetail ? (
          <p className="p-6 text-sm text-slate-400">Loading ticket…</p>
        ) : !selected ? (
          <p className="p-6 text-sm text-slate-400">Select a ticket to open it.</p>
        ) : (
          <div className="grid gap-6 p-6 lg:grid-cols-[minmax(0,1fr)_24rem]">
            <TicketDetail
              key={selected.key}
              ticket={selected}
              site={site}
              onChanged={refreshSelected}
            />
            <TicketAssistant key={`ai-${selected.key}`} ticket={selected} proc={procStatus} />
          </div>
        )}
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Ticket detail: assignee, status, description, comments — all writable.
// ---------------------------------------------------------------------------

function TicketDetail({
  ticket,
  site,
  onChanged,
}: {
  ticket: JiraIssueDetail;
  site: string | null;
  onChanged: () => void;
}) {
  const [assignable, setAssignable] = useState<JiraUser[] | null>(null);
  const [assigning, setAssigning] = useState(false);
  const [transitions, setTransitions] = useState<JiraTransition[] | null>(null);
  const [transitioning, setTransitioning] = useState(false);
  const [commentText, setCommentText] = useState("");
  const [commenting, setCommenting] = useState(false);
  const [note, setNote] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);

  function flash(msg: string) {
    setNote(msg);
    setErr(null);
    setTimeout(() => setNote(null), 3000);
  }

  async function loadAssignable() {
    if (assignable) return;
    try {
      setAssignable(await api.jira.assignable(ticket.key));
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Couldn't load users");
    }
  }

  async function loadTransitions() {
    if (transitions) return;
    try {
      setTransitions(await api.jira.transitions(ticket.key));
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Couldn't load statuses");
    }
  }

  async function assign(accountId: string) {
    if (!accountId) return;
    const user = assignable?.find((u) => u.accountId === accountId);
    setAssigning(true);
    setErr(null);
    try {
      await api.jira.assign(ticket.key, accountId, user?.displayName ?? "");
      flash(`Assigned to ${user?.displayName ?? "user"}.`);
      onChanged();
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Assign failed");
    } finally {
      setAssigning(false);
    }
  }

  async function transition(id: string) {
    if (!id) return;
    const t = transitions?.find((x) => x.id === id);
    setTransitioning(true);
    setErr(null);
    try {
      await api.jira.transition(ticket.key, id, t?.name ?? "");
      flash(`Moved to ${t?.toStatus || t?.name || "new status"}.`);
      setTransitions(null); // available transitions change after a move
      onChanged();
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Transition failed");
    } finally {
      setTransitioning(false);
    }
  }

  async function addComment() {
    if (!commentText.trim()) return;
    setCommenting(true);
    setErr(null);
    try {
      await api.jira.comment(ticket.key, commentText.trim());
      setCommentText("");
      flash("Comment added.");
      onChanged();
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Comment failed");
    } finally {
      setCommenting(false);
    }
  }

  return (
    <article className="space-y-5">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-mono text-sm text-slate-500">{ticket.key}</span>
        {ticket.issueType && <Badge tone="blue">{ticket.issueType}</Badge>}
        {ticket.status && <Badge tone="slate">{ticket.status}</Badge>}
        {ticket.priority && <Badge tone="amber">{ticket.priority}</Badge>}
        {site && (
          <a
            href={`${site.replace(/\/$/, "")}/browse/${ticket.key}`}
            target="_blank"
            rel="noreferrer"
            className="ml-auto text-xs text-blue-600 hover:underline"
          >
            Open in Jira ↗
          </a>
        )}
      </div>

      <h2 className="text-lg font-semibold text-slate-800">{ticket.summary}</h2>

      {(note || err) && (
        <p className={`text-xs font-medium ${err ? "text-red-600" : "text-emerald-700"}`}>{err || note}</p>
      )}

      {/* People + workflow controls */}
      <div className="grid gap-4 rounded-lg border border-slate-200 bg-slate-50/60 p-4 sm:grid-cols-2">
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-400">Assignee</label>
          <Select
            className="w-full"
            disabled={assigning}
            value={ticket.assignee?.accountId ?? ""}
            onFocus={loadAssignable}
            onChange={(e) => assign(e.target.value)}
          >
            <option value="">{ticket.assignee?.displayName ?? "Unassigned"}</option>
            {(assignable ?? []).map((u) => (
              <option key={u.accountId} value={u.accountId}>
                {u.displayName}
              </option>
            ))}
          </Select>
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-400">Status / transition</label>
          <Select
            className="w-full"
            disabled={transitioning}
            value=""
            onFocus={loadTransitions}
            onChange={(e) => transition(e.target.value)}
          >
            <option value="">{ticket.status || "Move to…"}</option>
            {(transitions ?? []).map((t) => (
              <option key={t.id} value={t.id}>
                → {t.name}
              </option>
            ))}
          </Select>
        </div>
        {ticket.reporter && (
          <div className="text-xs text-slate-500">
            <span className="text-slate-400">Reporter: </span>
            {ticket.reporter.displayName}
          </div>
        )}
        {ticket.updated && (
          <div className="text-xs text-slate-500">
            <span className="text-slate-400">Updated: </span>
            {new Date(ticket.updated).toLocaleString()}
          </div>
        )}
      </div>

      {/* Description */}
      <div>
        <h3 className="mb-1 text-xs font-medium uppercase tracking-wide text-slate-400">Description</h3>
        {ticket.description ? (
          <pre className="whitespace-pre-wrap break-words font-sans text-sm leading-relaxed text-slate-700">
            {ticket.description}
          </pre>
        ) : (
          <p className="text-sm text-slate-400">No description.</p>
        )}
      </div>

      {/* Comments */}
      <div>
        <h3 className="mb-2 text-xs font-medium uppercase tracking-wide text-slate-400">
          Comments {ticket.comments?.length ? `(${ticket.comments.length})` : ""}
        </h3>
        <div className="space-y-3">
          {(ticket.comments ?? []).length === 0 ? (
            <p className="text-sm text-slate-400">No comments yet.</p>
          ) : (
            ticket.comments!.map((c, i) => (
              <div key={i} className="rounded-lg border border-slate-100 bg-white p-3">
                <div className="mb-1 flex items-center gap-2 text-xs">
                  <span className="font-semibold text-slate-700">{c.author}</span>
                  {c.created && (
                    <span className="text-slate-400">{new Date(c.created).toLocaleString()}</span>
                  )}
                </div>
                <p className="whitespace-pre-wrap text-sm text-slate-600">{c.body}</p>
              </div>
            ))
          )}
        </div>
        <div className="mt-3 space-y-2">
          <textarea
            value={commentText}
            onChange={(e) => setCommentText(e.target.value)}
            placeholder="Add a comment to this ticket…"
            rows={2}
            className="w-full rounded-lg border border-slate-200 bg-white/60 px-3 py-2 text-sm outline-none transition-all focus:border-slate-400 focus:bg-white focus:ring-4 focus:ring-slate-100"
          />
          <div className="flex justify-end">
            <Button onClick={addComment} disabled={commenting || !commentText.trim()}>
              {commenting ? "Posting…" : "Add comment"}
            </Button>
          </div>
        </div>
      </div>
    </article>
  );
}

// ---------------------------------------------------------------------------
// Miriani panel: ask about the ticket + draft-and-send the three actions.
// ---------------------------------------------------------------------------

type ActionKind = "alert" | "email" | "report";

function TicketAssistant({ ticket, proc }: { ticket: JiraIssueDetail; proc: ProcessStatus | null }) {
  // Q&A
  const [question, setQuestion] = useState("");
  const [asking, setAsking] = useState(false);
  const [answer, setAnswer] = useState<AgentResult | null>(null);
  const [qaError, setQaError] = useState<string | null>(null);

  // active action draft
  const [kind, setKind] = useState<ActionKind | null>(null);
  const [draft, setDraft] = useState<any>(null);
  const [drafting, setDrafting] = useState<ActionKind | null>(null);
  const [sending, setSending] = useState(false);
  const [actionMsg, setActionMsg] = useState<string | null>(null);
  const [actionErr, setActionErr] = useState<string | null>(null);

  // manager picker (for email)
  const [managers, setManagers] = useState<ManagerDto[]>([]);
  const [managerEmail, setManagerEmail] = useState("");

  function ticketContext() {
    const parts = [`Jira ticket ${ticket.key}: ${ticket.summary}`];
    if (ticket.status) parts.push(`Status: ${ticket.status}`);
    if (ticket.priority) parts.push(`Priority: ${ticket.priority}`);
    if (ticket.description) parts.push(`\nDescription:\n${ticket.description}`);
    const comments = ticket.comments ?? [];
    if (comments.length) {
      parts.push("\nComments:");
      comments.forEach((c) => parts.push(`- ${c.author}: ${c.body}`));
    }
    return parts.join("\n");
  }

  async function ask() {
    if (!question.trim()) return;
    setAsking(true);
    setQaError(null);
    setAnswer(null);
    try {
      const prompt = `${ticketContext()}\n\nQuestion about this ticket: ${question.trim()}`;
      setAnswer(await api.runAgent(prompt));
    } catch (e) {
      setQaError(e instanceof Error ? e.message : "Couldn't get an answer");
    } finally {
      setAsking(false);
    }
  }

  async function startDraft(k: ActionKind) {
    setKind(k);
    setDraft(null);
    setActionMsg(null);
    setActionErr(null);
    setDrafting(k);
    try {
      if (k === "email" && managers.length === 0) {
        const list = await api.processes.listManagers();
        setManagers(list);
        if (list[0]) setManagerEmail(list[0].email);
      }
      const managerName = managers.find((m) => m.email === managerEmail)?.displayName;
      const res = await api.processes.jiraDraft(k, ticket, managerName);
      setDraft(res.structured ?? {});
    } catch (e) {
      setActionErr(e instanceof Error ? e.message : "Draft failed");
      setKind(null);
    } finally {
      setDrafting(null);
    }
  }

  async function send() {
    if (!kind || !draft) return;
    setSending(true);
    setActionMsg(null);
    setActionErr(null);
    try {
      if (kind === "alert") {
        const r = await api.processes.jiraAlert(ticket.key, {
          title: draft.title,
          message: draft.message,
          severity: draft.severity,
        });
        setActionMsg(r.alerted ? "Alert sent to the channel." : "No webhook configured — nothing sent.");
      } else if (kind === "email") {
        if (!managerEmail) {
          setActionErr("Pick a manager first.");
          return;
        }
        const r = await api.processes.jiraEmail(ticket.key, managerEmail, {
          subject: draft.subject,
          body: draft.body,
        });
        setActionMsg(`Email sent to ${r.to}.`);
      } else if (kind === "report") {
        await api.processes.jiraReport(ticket.key, draft);
        setActionMsg("Report downloaded.");
      }
    } catch (e) {
      setActionErr(e instanceof Error ? e.message : "Action failed");
    } finally {
      setSending(false);
    }
  }

  const emailOn = proc?.email ?? false;
  const slackOn = proc?.notifications ?? false;

  return (
    <aside className="space-y-4 lg:sticky lg:top-6 lg:self-start">
      {/* Ask Miriani */}
      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <div className="mb-2 flex items-center gap-2">
          <div className="flex h-6 w-6 items-center justify-center rounded-full bg-slate-800 text-[10px] font-semibold text-white">
            M
          </div>
          <span className="text-sm font-semibold text-slate-800">Ask Miriani about this ticket</span>
        </div>
        <textarea
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="e.g. What does our SLA say for this? How should I resolve it?"
          rows={2}
          className="w-full rounded-lg border border-slate-200 bg-white/60 px-3 py-2 text-sm outline-none transition-all focus:border-slate-400 focus:bg-white focus:ring-4 focus:ring-slate-100"
        />
        <div className="mt-2 flex justify-end">
          <Button onClick={ask} disabled={asking || !question.trim()}>
            {asking ? "Thinking…" : "Ask"}
          </Button>
        </div>
        {qaError && <p className="mt-2 text-xs text-red-600">{qaError}</p>}
        {answer && (
          <div className="mt-3 rounded-lg bg-slate-50 p-3">
            <p className="whitespace-pre-wrap text-sm leading-relaxed text-slate-700">{answer.answer}</p>
            {answer.sources.length > 0 && (
              <p className="mt-2 text-xs text-slate-400">
                {answer.sources.length} source{answer.sources.length !== 1 ? "s" : ""} ·{" "}
                {answer.sources.map((s) => s.fileName).filter((v, i, a) => a.indexOf(v) === i).join(", ")}
              </p>
            )}
          </div>
        )}
      </div>

      {/* Actions */}
      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">Actions</span>
        <div className="mt-3 flex flex-wrap gap-2">
          <ActBtn label="Draft Slack alert" busy={drafting === "alert"} disabled={!slackOn}
            title={slackOn ? "" : "No webhook configured"} onClick={() => startDraft("alert")} />
          <ActBtn label="Email a manager" busy={drafting === "email"} disabled={!emailOn}
            title={emailOn ? "" : "Email not configured"} onClick={() => startDraft("email")} />
          <ActBtn label="Generate report" busy={drafting === "report"} onClick={() => startDraft("report")} />
        </div>

        {/* Draft review panel */}
        {kind && draft && (
          <div className="mt-4 space-y-3 border-t border-slate-100 pt-4">
            {kind === "alert" && (
              <>
                <Field label="Title">
                  <Input value={draft.title ?? ""} onChange={(e) => setDraft({ ...draft, title: e.target.value })} />
                </Field>
                <Field label={`Message${draft.severity ? ` · ${draft.severity}` : ""}`}>
                  <Area value={draft.message ?? ""} onChange={(v) => setDraft({ ...draft, message: v })} rows={4} />
                </Field>
              </>
            )}

            {kind === "email" && (
              <>
                <Field label="To (manager)">
                  <Select className="w-full" value={managerEmail} onChange={(e) => setManagerEmail(e.target.value)}>
                    {managers.length === 0 && <option value="">No managers found</option>}
                    {managers.map((m) => (
                      <option key={m.id} value={m.email}>
                        {m.displayName} ({m.email})
                      </option>
                    ))}
                  </Select>
                </Field>
                <Field label="Subject">
                  <Input value={draft.subject ?? ""} onChange={(e) => setDraft({ ...draft, subject: e.target.value })} />
                </Field>
                <Field label="Body">
                  <Area value={draft.body ?? ""} onChange={(v) => setDraft({ ...draft, body: v })} rows={7} />
                </Field>
              </>
            )}

            {kind === "report" && (
              <div className="space-y-2 rounded-lg bg-slate-50 p-3 text-sm text-slate-700">
                {draft.title && <p className="font-semibold">{draft.title}</p>}
                {draft.severity && <p className="text-xs text-slate-500">Severity: {draft.severity}</p>}
                {draft.summary && <p>{draft.summary}</p>}
                {draft.impact && <p><span className="text-slate-400">Impact: </span>{draft.impact}</p>}
                {draft.root_cause_hypothesis && (
                  <p><span className="text-slate-400">Likely cause: </span>{draft.root_cause_hypothesis}</p>
                )}
                {Array.isArray(draft.recommended_actions) && draft.recommended_actions.length > 0 && (
                  <ul className="list-disc pl-5">
                    {draft.recommended_actions.map((a: string, i: number) => <li key={i}>{a}</li>)}
                  </ul>
                )}
              </div>
            )}

            <div className="flex items-center justify-end gap-2">
              <button
                onClick={() => { setKind(null); setDraft(null); }}
                className="rounded-lg px-3 py-2 text-xs font-medium text-slate-500 hover:bg-slate-100"
              >
                Discard
              </button>
              <Button onClick={send} disabled={sending}>
                {sending ? "Sending…" : kind === "report" ? "Download PDF" : kind === "email" ? "Send email" : "Send to Slack"}
              </Button>
            </div>
          </div>
        )}

        {actionMsg && <p className="mt-3 text-xs font-medium text-emerald-700">✓ {actionMsg}</p>}
        {actionErr && <p className="mt-3 text-xs font-medium text-red-600">✕ {actionErr}</p>}
      </div>
    </aside>
  );
}

function ActBtn({ label, onClick, busy, disabled, title }: {
  label: string; onClick: () => void; busy?: boolean; disabled?: boolean; title?: string;
}) {
  return (
    <button
      onClick={onClick}
      disabled={busy || disabled}
      title={title}
      className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 transition-all hover:bg-slate-50 active:scale-[0.97] disabled:opacity-40 disabled:pointer-events-none"
    >
      {busy ? "Drafting…" : label}
    </button>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="mb-1 block text-xs font-medium text-slate-400">{label}</label>
      {children}
    </div>
  );
}

function Area({ value, onChange, rows }: { value: string; onChange: (v: string) => void; rows: number }) {
  return (
    <textarea
      value={value}
      onChange={(e) => onChange(e.target.value)}
      rows={rows}
      className="w-full rounded-lg border border-slate-200 bg-white/60 px-3 py-2 text-sm outline-none transition-all focus:border-slate-400 focus:bg-white focus:ring-4 focus:ring-slate-100"
    />
  );
}

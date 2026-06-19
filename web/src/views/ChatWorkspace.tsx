import { useEffect, useRef, useState } from "react";
import { api, type ChatAttachment, type ChatMessage, type ChatSessionSummary } from "../api";
import { ROLE_NAMES, useAuth } from "../auth";
import { BrandHeader } from "../branding";
import { Badge, Button } from "../ui";
import { ChangePassword } from "./ChangePassword";
import { JiraPicker, formatTicketPrompt } from "./JiraPicker";
import { JiraTickets } from "./JiraTickets";
import { MessageBubble } from "./MessageBubble";

let tempId = 0;
const nextTempId = () => `tmp-${++tempId}`;

export function ChatWorkspace() {
  const { session, logout } = useAuth();
  const [sessions, setSessions] = useState<ChatSessionSummary[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [faqs, setFaqs] = useState<string[]>([]);
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [attachment, setAttachment] = useState<ChatAttachment | null>(null);
  const [attaching, setAttaching] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [view, setView] = useState<"chat" | "tickets">("chat");
  const [jiraOn, setJiraOn] = useState(false);
  const [showJira, setShowJira] = useState(false);
  const threadRef = useRef<HTMLDivElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  async function onPickFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = ""; // allow re-selecting the same file
    if (!file) return;
    setAttaching(true);
    setError(null);
    try {
      setAttachment(await api.chat.extractAttachment(file));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Couldn't read that file");
    } finally {
      setAttaching(false);
    }
  }

  async function refreshSessions() {
    try {
      setSessions(await api.chat.listSessions());
    } catch {
      /* non-fatal */
    }
  }

  useEffect(() => {
    refreshSessions();
    api.faqs.list().then((l) => setFaqs(l.map((f) => f.question))).catch(() => setFaqs([]));
    api.jira.status().then((s) => setJiraOn(s.configured)).catch(() => setJiraOn(false));
  }, []);

  // A Jira ticket chosen from the picker is dropped into the composer as an editable prompt.
  function onPickTicket(issue: { key: string; summary: string; description: string }) {
    setShowJira(false);
    setView("chat");
    setInput(formatTicketPrompt(issue));
  }

  useEffect(() => {
    threadRef.current?.scrollTo({ top: threadRef.current.scrollHeight });
  }, [messages]);

  async function openSession(id: string) {
    setError(null);
    try {
      const thread = await api.chat.getSession(id);
      setActiveId(thread.id);
      setMessages(thread.messages);
      setView("chat");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to open chat");
    }
  }

  function newChat() {
    setActiveId(null);
    setMessages([]);
    setError(null);
  }

  async function send(text: string) {
    const query = text.trim();
    if ((!query && !attachment) || busy) return;
    const sentAttachment = attachment;
    setBusy(true);
    setError(null);
    setInput("");
    setAttachment(null);

    const optimistic: ChatMessage = {
      id: nextTempId(),
      sender: "user",
      content: sentAttachment ? `${query}\n\n📎 ${sentAttachment.fileName}` : query,
      route: null, usedContext: false, sources: null, structured: null,
      createdAt: new Date().toISOString(),
    };
    setMessages((m) => [...m, optimistic]);

    try {
      const r = await api.chat.sendMessage(activeId, query || `(see attached ${sentAttachment?.fileName})`, sentAttachment);
      const reply: ChatMessage = {
        id: nextTempId(), sender: "assistant", content: r.answer,
        route: r.route, usedContext: r.usedContext, sources: r.sources, structured: r.structured,
        createdAt: new Date().toISOString(),
      };
      setMessages((m) => [...m, reply]);
      setActiveId(r.sessionId);
      refreshSessions();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Message failed");
      setMessages((m) => m.filter((x) => x.id !== optimistic.id)); // roll back the optimistic message
    } finally {
      setBusy(false);
    }
  }

  async function remove(id: string) {
    try {
      await api.chat.deleteSession(id);
      if (id === activeId) newChat();
      refreshSessions();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to delete chat");
    }
  }

  const initials = (session?.displayName ?? "?")
    .split(" ")
    .map((p) => p[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

  return (
    <div className="flex h-screen overflow-hidden bg-transparent text-slate-900">
      {/* History sidebar */}
      <aside className="flex w-72 shrink-0 flex-col border-r border-slate-200/60 bg-slate-50/80 backdrop-blur-md">
        <div className="px-4 py-4">
          <BrandHeader />
        </div>
        <div className="px-3">
          <Button onClick={() => { newChat(); setView("chat"); }} className="w-full justify-start gap-2">
            <span className="text-base leading-none">＋</span> New chat
          </Button>
        </div>

        {jiraOn && (
          <nav className="mt-3 flex gap-1 px-3">
            <button
              onClick={() => setView("chat")}
              className={
                "flex-1 rounded-lg px-3 py-1.5 text-sm font-medium transition " +
                (view === "chat" ? "bg-slate-200 text-slate-900" : "text-slate-500 hover:bg-slate-200/50")
              }
            >
              Chat
            </button>
            <button
              onClick={() => setView("tickets")}
              className={
                "flex-1 rounded-lg px-3 py-1.5 text-sm font-medium transition " +
                (view === "tickets" ? "bg-slate-200 text-slate-900" : "text-slate-500 hover:bg-slate-200/50")
              }
            >
              Tickets
            </button>
          </nav>
        )}

        <div className="mt-4 flex-1 overflow-auto px-2 pb-2">
          <p className="px-2 pb-1 text-xs font-medium uppercase tracking-wide text-slate-400">History</p>
          {sessions.length === 0 ? (
            <p className="px-2 py-2 text-xs text-slate-400">No conversations yet.</p>
          ) : (
            sessions.map((s) => (
              <div
                key={s.id}
                className={
                  "group flex items-center gap-1 rounded-lg px-2 py-2 text-sm " +
                  (s.id === activeId ? "bg-slate-200/70" : "hover:bg-slate-200/50")
                }
              >
                <button onClick={() => openSession(s.id)} className="flex-1 truncate text-left text-slate-700">
                  {s.title}
                </button>
                <button
                  onClick={() => remove(s.id)}
                  className="opacity-0 transition group-hover:opacity-100 text-slate-400 hover:text-red-600"
                  title="Delete chat"
                >
                  ×
                </button>
              </div>
            ))
          )}
        </div>

        {/* Account footer */}
        <div className="border-t border-slate-200 p-3">
          <div className="flex items-center gap-3">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-slate-900 text-xs font-semibold text-white">
              {initials}
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-slate-800">{session?.displayName}</p>
              <Badge tone="blue">{ROLE_NAMES[session?.role ?? 0]}</Badge>
            </div>
          </div>
          <div className="mt-2 flex gap-3 px-1 text-xs">
            <button onClick={() => setShowPassword(true)} className="text-slate-500 hover:text-slate-800">
              Change password
            </button>
            <button onClick={() => logout()} className="text-slate-500 hover:text-slate-800">
              Sign out
            </button>
          </div>
        </div>
      </aside>

      {/* Right column: ticket browser or chat */}
      <main className="flex flex-1 flex-col">
        {view === "tickets" ? (
          <JiraTickets />
        ) : (
        <>
        <div ref={threadRef} className="flex-1 overflow-auto">
          <div className="mx-auto w-full max-w-3xl px-4 py-6">
            {messages.length === 0 ? (
              <div className="flex h-full flex-col items-center justify-center gap-5 pt-24 text-center animate-slide-up">
                <img src="/miriani-logo.png" alt="Miriani" className="mb-2 h-20 w-20 object-contain" />
                <h1 className="text-3xl font-bold tracking-tight text-slate-800">Hi, I'm Miriani.</h1>
                <p className="max-w-md text-sm leading-relaxed text-slate-500">
                  I'm here to help you get things done. Answers are grounded in your organization's documents.
                </p>
                {faqs.length > 0 && (
                  <div className="mt-2 flex flex-wrap justify-center gap-2">
                    {faqs.map((q) => (
                      <button
                        key={q}
                        onClick={() => send(q)}
                        disabled={busy}
                        className="rounded-full border border-slate-200 px-3 py-1 text-xs text-slate-600 hover:bg-slate-100 disabled:opacity-50"
                      >
                        {q}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ) : (
              <div className="space-y-4">
                {messages.map((m) => <MessageBubble key={m.id} message={m} />)}
                {busy && (
                  <div className="flex items-center gap-2.5 px-1 py-2 animate-fade-in">
                    <div className="flex gap-1">
                      <span className="h-2 w-2 rounded-full bg-blue-400 animate-bounce" style={{ animationDelay: '0ms' }} />
                      <span className="h-2 w-2 rounded-full bg-indigo-400 animate-bounce" style={{ animationDelay: '150ms' }} />
                      <span className="h-2 w-2 rounded-full bg-violet-400 animate-bounce" style={{ animationDelay: '300ms' }} />
                    </div>
                    <span className="text-sm text-slate-400 italic">Miriani is thinking…</span>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Composer */}
        <div className="bg-transparent pb-6 pt-2">
          <div className="mx-auto w-full max-w-3xl px-4">
            {error && <p className="mb-2 text-sm text-red-600">{error}</p>}
            {(attachment || attaching) && (
              <div className="mb-2 flex items-center gap-2 animate-fade-in">
                <span className="inline-flex items-center gap-2 rounded-full bg-blue-50/80 ring-1 ring-blue-200 px-3 py-1 text-xs text-blue-700 shadow-sm backdrop-blur-sm">
                  📎 {attaching ? "Reading file…" : attachment!.fileName}
                  {attachment?.truncated && <span className="text-blue-400">(truncated)</span>}
                  {attachment && (
                    <button onClick={() => setAttachment(null)} className="text-blue-400 hover:text-red-600 transition-colors" title="Remove attachment">×</button>
                  )}
                </span>
                <span className="text-xs text-slate-400">temporary — used for this message only, not saved</span>
              </div>
            )}
            <div className="flex items-end gap-2 rounded-3xl border border-slate-200/80 bg-white/80 backdrop-blur-xl px-4 py-3 shadow-lg shadow-slate-200/40 ring-1 ring-black/5 focus-within:border-slate-400 focus-within:ring-4 focus-within:ring-slate-100 transition-all duration-300">
              <input ref={fileRef} type="file" className="hidden"
                accept=".pdf,.docx,.xlsx,.txt,.md,.csv" onChange={onPickFile} />
              <button
                onClick={() => fileRef.current?.click()}
                disabled={busy || attaching}
                title="Attach a file (temporary)"
                className="rounded-md px-1 pb-1 text-lg text-slate-400 hover:text-slate-700 disabled:opacity-50"
              >
                📎
              </button>
              {jiraOn && (
                <button
                  onClick={() => setShowJira(true)}
                  disabled={busy}
                  title="Add a Jira ticket to your message"
                  className="rounded-md px-2 pb-1 text-xs font-medium text-slate-500 hover:text-slate-700 disabled:opacity-50"
                >
                  + Jira
                </button>
              )}
              <textarea
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(input); }
                }}
                rows={1}
                placeholder="Message Miriani…  (Enter to send, Shift+Enter for newline)"
                className="max-h-40 flex-1 resize-none bg-transparent py-1.5 text-sm outline-none"
              />
              <Button 
                onClick={() => send(input)} 
                disabled={busy || (!input.trim() && !attachment)} 
                className="rounded-full bg-blue-600 hover:bg-blue-700 shadow-blue-500/30 px-5"
              >
                Send
              </Button>
            </div>
            <p className="mt-1.5 text-center text-xs text-slate-400">
              Miriani can make mistakes. Answers are grounded in your organization's documents.
            </p>
          </div>
        </div>
        </>
        )}
      </main>

      {/* Jira ticket picker */}
      {showJira && <JiraPicker onPick={onPickTicket} onClose={() => setShowJira(false)} />}

      {/* Change-password overlay */}
      {showPassword && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4" onClick={() => setShowPassword(false)}>
          <div onClick={(e) => e.stopPropagation()}>
            <ChangePassword onDone={() => setShowPassword(false)} />
          </div>
        </div>
      )}
    </div>
  );
}

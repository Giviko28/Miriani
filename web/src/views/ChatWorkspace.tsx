import { useEffect, useRef, useState } from "react";
import { api, type ChatAttachment, type ChatMessage, type ChatSessionSummary } from "../api";
import { Button } from "../ui";
import { MessageBubble } from "./MessageBubble";

let tempId = 0;
const nextTempId = () => `tmp-${++tempId}`;

export function ChatWorkspace() {
  const [sessions, setSessions] = useState<ChatSessionSummary[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [faqs, setFaqs] = useState<string[]>([]);
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [attachment, setAttachment] = useState<ChatAttachment | null>(null);
  const [attaching, setAttaching] = useState(false);
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
  }, []);

  useEffect(() => {
    threadRef.current?.scrollTo({ top: threadRef.current.scrollHeight });
  }, [messages]);

  async function openSession(id: string) {
    setError(null);
    try {
      const thread = await api.chat.getSession(id);
      setActiveId(thread.id);
      setMessages(thread.messages);
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

  return (
    <div className="flex h-[70vh] overflow-hidden rounded-xl border border-slate-200 bg-white">
      {/* Sessions sidebar */}
      <div className="flex w-56 flex-col border-r border-slate-200">
        <div className="p-3">
          <Button onClick={newChat} className="w-full">+ New chat</Button>
        </div>
        <div className="flex-1 overflow-auto px-2 pb-2">
          {sessions.length === 0 ? (
            <p className="px-2 py-2 text-xs text-slate-400">No conversations yet.</p>
          ) : (
            sessions.map((s) => (
              <div
                key={s.id}
                className={
                  "group flex items-center gap-1 rounded-md px-2 py-2 text-sm " +
                  (s.id === activeId ? "bg-slate-100" : "hover:bg-slate-50")
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
      </div>

      {/* Thread + input */}
      <div className="flex flex-1 flex-col">
        <div ref={threadRef} className="flex-1 space-y-4 overflow-auto p-4">
          {messages.length === 0 ? (
            <div className="space-y-3">
              <p className="text-sm text-slate-500">
                Ask a question or request a task. Answers are grounded in documents your role can
                access, and this conversation is saved so you can come back to it.
              </p>
              {faqs.length > 0 && (
                <div className="flex flex-wrap gap-2">
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
            messages.map((m) => <MessageBubble key={m.id} message={m} />)
          )}
          {busy && <p className="text-sm text-slate-400">Thinking…</p>}
        </div>

        {error && <p className="px-4 text-sm text-red-600">{error}</p>}

        <div className="border-t border-slate-200 p-3">
          {(attachment || attaching) && (
            <div className="mb-2 flex items-center gap-2">
              <span className="inline-flex items-center gap-2 rounded-full bg-blue-50 px-3 py-1 text-xs text-blue-700">
                📎 {attaching ? "Reading file…" : attachment!.fileName}
                {attachment?.truncated && <span className="text-blue-400">(truncated)</span>}
                {attachment && (
                  <button onClick={() => setAttachment(null)} className="text-blue-400 hover:text-red-600" title="Remove attachment">×</button>
                )}
              </span>
              <span className="text-xs text-slate-400">temporary — used for this message only, not saved</span>
            </div>
          )}
          <div className="flex gap-2">
            <input ref={fileRef} type="file" className="hidden"
              accept=".pdf,.docx,.xlsx,.txt,.md,.csv" onChange={onPickFile} />
            <button
              onClick={() => fileRef.current?.click()}
              disabled={busy || attaching}
              title="Attach a file (temporary)"
              className="rounded-md border border-slate-300 px-3 text-slate-500 hover:bg-slate-50 disabled:opacity-50"
            >
              📎
            </button>
            <textarea
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(input); }
              }}
              rows={1}
              placeholder="Message the assistant…  (Enter to send, Shift+Enter for newline)"
              className="flex-1 resize-none rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-500"
            />
            <Button onClick={() => send(input)} disabled={busy || (!input.trim() && !attachment)}>Send</Button>
          </div>
        </div>
      </div>
    </div>
  );
}

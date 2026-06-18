import { useEffect, useState } from "react";
import { api, type FaqDto } from "../../api";
import { Button, Card, Input } from "../../ui";

export function Faqs() {
  const [faqs, setFaqs] = useState<FaqDto[]>([]);
  const [newQuestion, setNewQuestion] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    try {
      setFaqs(await api.faqs.list());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load FAQs");
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

  async function add() {
    const q = newQuestion.trim();
    if (!q) return;
    const nextOrder = (faqs.at(-1)?.sortOrder ?? 0) + 1;
    await act(() => api.faqs.create(q, nextOrder));
    setNewQuestion("");
  }

  return (
    <div className="space-y-5">
      <h1 className="text-2xl font-semibold">FAQs</h1>
      <p className="text-sm text-slate-500">
        These appear as clickable suggestions on the user chat screen. Clicking one runs it
        through the AI assistant.
      </p>
      {error && <p className="text-sm text-red-600">{error}</p>}

      <Card>
        <div className="flex gap-2">
          <Input
            placeholder="Add a suggested question…"
            value={newQuestion}
            onChange={(e) => setNewQuestion(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter") add(); }}
          />
          <Button onClick={add} disabled={!newQuestion.trim()}>Add</Button>
        </div>
      </Card>

      <Card>
        {faqs.length === 0 ? (
          <p className="text-sm text-slate-500">No FAQs yet.</p>
        ) : (
          <ul className="space-y-2">
            {faqs.map((f) => (
              <FaqRow key={f.id} faq={f} onSave={(q) => act(() => api.faqs.update(f.id, q, f.sortOrder))} onDelete={() => act(() => api.faqs.remove(f.id))} />
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

function FaqRow({ faq, onSave, onDelete }: { faq: FaqDto; onSave: (q: string) => void; onDelete: () => void }) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(faq.question);

  return (
    <li className="flex items-center gap-2 border-t border-slate-100 py-2 first:border-0">
      {editing ? (
        <>
          <Input value={value} onChange={(e) => setValue(e.target.value)} />
          <Button onClick={() => { onSave(value.trim()); setEditing(false); }} disabled={!value.trim()}>Save</Button>
          <button className="text-sm text-slate-500" onClick={() => { setValue(faq.question); setEditing(false); }}>Cancel</button>
        </>
      ) : (
        <>
          <span className="flex-1 text-sm text-slate-700">{faq.question}</span>
          <button className="text-xs text-slate-600 hover:text-slate-900" onClick={() => setEditing(true)}>Edit</button>
          <button className="text-xs text-red-600 hover:text-red-800" onClick={onDelete}>Delete</button>
        </>
      )}
    </li>
  );
}

import type { ChatMessage } from "../api";
import { Badge, Card } from "../ui";

export const ROUTE_LABELS: Record<string, string> = {
  policy_qa: "Policy Q&A",
  doc_summary: "Summarizer",
  email_draft: "Email Drafter",
  report_draft: "Report Drafter",
  invoice_gen: "Invoice Generator",
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

export function MessageBubble({ message }: { message: ChatMessage }) {
  if (message.sender === "user") {
    return (
      <div className="flex justify-end">
        <div className="max-w-[80%] whitespace-pre-wrap rounded-2xl bg-slate-900 px-4 py-2 text-sm text-white">
          {message.content}
        </div>
      </div>
    );
  }

  const sources = parseSources(message.sources);
  const structured = parseStructured(message.structured);

  return (
    <Card>
      <div className="flex items-center gap-2">
        {message.route && <Badge tone="blue">{ROUTE_LABELS[message.route] ?? message.route}</Badge>}
        {message.usedContext && <Badge tone="green">grounded</Badge>}
      </div>

      <p className="mt-3 whitespace-pre-wrap text-sm leading-relaxed">{message.content}</p>

      {structured !== null && (
        <pre className="mt-4 overflow-auto rounded-md bg-slate-900 p-4 text-xs text-slate-100">
          {JSON.stringify(structured, null, 2)}
        </pre>
      )}

      {sources.length > 0 && (
        <div className="mt-5">
          <h3 className="text-sm font-semibold text-slate-700">Sources</h3>
          <ul className="mt-2 space-y-2">
            {sources.map((s, i) => (
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
  );
}

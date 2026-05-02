import type { ChatMessage } from "../api";
import { Badge, Card } from "../ui";

const RISK_COLORS: Record<string, string> = {
  High: "bg-red-100 text-red-700",
  Medium: "bg-yellow-100 text-yellow-700",
  Low: "bg-green-100 text-green-700",
};

const STATUS_COLORS: Record<string, string> = {
  approved: "bg-green-100 text-green-700",
  pending: "bg-yellow-100 text-yellow-700",
  flagged: "bg-red-100 text-red-700",
};

export const ROUTE_LABELS: Record<string, string> = {
  policy_qa: "Policy Q&A",
  doc_summary: "Summarizer",
  email_draft: "Email Drafter",
  report_draft: "Report Drafter",
  invoice_gen: "Invoice Generator",
  leave_request: "Leave Request",
  onboarding_gen: "Onboarding Kit",
  contract_scan: "Contract Scanner",
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

function LeaveCard({ data }: { data: any }) {
  const status: string = data.status ?? "pending";
  return (
    <div className="mt-4 space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <span className={`rounded-full px-3 py-1 text-xs font-semibold capitalize ${STATUS_COLORS[status] ?? "bg-slate-100 text-slate-600"}`}>
          {status}
        </span>
        {data.days_requested != null && (
          <span className="text-sm text-slate-600">{data.days_requested} day(s)</span>
        )}
        {data.start_date && data.end_date && (
          <span className="text-sm text-slate-500">{data.start_date} – {data.end_date}</span>
        )}
      </div>
      {data.policy_note && (
        <p className="rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-600">{data.policy_note}</p>
      )}
      {data.formal_letter && (
        <details className="rounded-md border border-slate-200">
          <summary className="cursor-pointer px-3 py-2 text-xs font-medium text-slate-700">Formal Letter</summary>
          <pre className="whitespace-pre-wrap px-3 pb-3 pt-1 text-xs text-slate-600">{data.formal_letter}</pre>
        </details>
      )}
    </div>
  );
}

function OnboardingCard({ data }: { data: any }) {
  const phases = [
    { label: "Day 1", key: "day_1" },
    { label: "Week 1", key: "week_1" },
    { label: "Month 1", key: "month_1" },
  ];
  return (
    <div className="mt-4 space-y-3">
      {data.role && <p className="text-xs font-semibold text-slate-700">Role: {data.role}{data.employee_name ? ` · ${data.employee_name}` : ""}{data.start_date ? ` · Starting ${data.start_date}` : ""}</p>}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        {phases.map(({ label, key }) => {
          const items: string[] = data[key] ?? [];
          return (
            <div key={key} className="rounded-md border border-slate-200 bg-slate-50 p-3">
              <p className="mb-2 text-xs font-semibold text-slate-700">{label}</p>
              <ul className="space-y-1">
                {items.map((item, i) => (
                  <li key={i} className="flex items-start gap-1.5 text-xs text-slate-600">
                    <span className="mt-0.5 text-slate-400">☐</span>{item}
                  </li>
                ))}
              </ul>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function ContractCard({ data }: { data: any }) {
  const overall: string = data.overall_risk ?? "Unknown";
  const clauses: any[] = data.clauses ?? [];
  const recommendations: string[] = data.recommendations ?? [];
  return (
    <div className="mt-4 space-y-3">
      <div className="flex items-center gap-2">
        <span className="text-xs font-semibold text-slate-600">Overall Risk:</span>
        <span className={`rounded-full px-3 py-0.5 text-xs font-semibold ${RISK_COLORS[overall] ?? "bg-slate-100 text-slate-600"}`}>{overall}</span>
      </div>
      {clauses.length > 0 && (
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-slate-200 text-left text-slate-500">
              <th className="pb-1 pr-3 font-medium">Clause</th>
              <th className="pb-1 pr-3 font-medium">Risk</th>
              <th className="pb-1 font-medium">Finding</th>
            </tr>
          </thead>
          <tbody>
            {clauses.map((c, i) => (
              <tr key={i} className="border-b border-slate-100">
                <td className="py-1.5 pr-3 text-slate-700">{c.clause}</td>
                <td className="py-1.5 pr-3">
                  <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${RISK_COLORS[c.risk] ?? "bg-slate-100 text-slate-600"}`}>{c.risk}</span>
                </td>
                <td className="py-1.5 text-slate-500">{c.finding}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {recommendations.length > 0 && (
        <div className="rounded-md bg-blue-50 p-3">
          <p className="mb-1 text-xs font-semibold text-blue-700">Recommendations</p>
          <ul className="list-inside list-disc space-y-0.5 text-xs text-blue-600">
            {recommendations.map((r, i) => <li key={i}>{r}</li>)}
          </ul>
        </div>
      )}
    </div>
  );
}

function InvoiceCard({ data }: { data: any }) {
  const items: any[] = data.items ?? [];
  return (
    <div className="mt-4 space-y-3">
      {data.client && <p className="text-xs font-semibold text-slate-700">Client: {data.client}</p>}
      {items.length > 0 && (
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-slate-200 text-left text-slate-500">
              <th className="pb-1 pr-3 font-medium">Description</th>
              <th className="pb-1 pr-3 font-medium">Qty</th>
              <th className="pb-1 font-medium">Unit Price</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item, i) => (
              <tr key={i} className="border-b border-slate-100">
                <td className="py-1.5 pr-3 text-slate-700">{item.description}</td>
                <td className="py-1.5 pr-3 text-slate-500">{item.quantity ?? item.qty}</td>
                <td className="py-1.5 text-slate-500">{item.unit_price ?? item.price}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {data.total != null && (
        <p className="text-right text-sm font-semibold text-slate-800">Total: {data.total} {data.currency ?? ""}</p>
      )}
      {data.notes && <p className="text-xs text-slate-500">{data.notes}</p>}
    </div>
  );
}

function StructuredCard({ route, data }: { route: string; data: unknown }) {
  if (route === "leave_request") return <LeaveCard data={data} />;
  if (route === "onboarding_gen") return <OnboardingCard data={data} />;
  if (route === "contract_scan") return <ContractCard data={data} />;
  if (route === "invoice_gen") return <InvoiceCard data={data} />;
  return (
    <pre className="mt-4 overflow-auto rounded-md bg-slate-900 p-4 text-xs text-slate-100">
      {JSON.stringify(data, null, 2)}
    </pre>
  );
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
        <StructuredCard route={message.route ?? ""} data={structured} />
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

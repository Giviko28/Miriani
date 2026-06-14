import { useState } from "react";

// Smoke-test page. Proves React -> .NET API -> Python AI service -> Ollama.
// The real UI (Tailwind + shadcn) is built in Milestone 6.

const API_BASE = "http://localhost:5080";

type PingResult = {
  model: string;
  reply: string;
  elapsed_Seconds: number;
};

export default function App() {
  const [prompt, setPrompt] = useState("Say hello in one short sentence.");
  const [result, setResult] = useState<PingResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function send() {
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const res = await fetch(`${API_BASE}/api/smoke/ping-llm`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ prompt }),
      });
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      setResult(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }

  return (
    <main style={{ maxWidth: 640, margin: "3rem auto", fontFamily: "system-ui", padding: "0 1rem" }}>
      <h1>BPA — Integration Smoke Test</h1>
      <p style={{ color: "#666" }}>React → .NET API → Python AI service → Ollama (local LLM)</p>

      <textarea
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
        rows={3}
        style={{ width: "100%", padding: 8, fontSize: 14 }}
      />
      <button onClick={send} disabled={loading} style={{ marginTop: 8, padding: "8px 16px" }}>
        {loading ? "Asking the LLM…" : "Send"}
      </button>

      {error && <p style={{ color: "crimson" }}>Error: {error}</p>}

      {result && (
        <div style={{ marginTop: 16, padding: 16, border: "1px solid #ddd", borderRadius: 8 }}>
          <p><strong>Reply:</strong> {result.reply}</p>
          <p style={{ color: "#666", fontSize: 13 }}>
            model: {result.model} · {result.elapsed_Seconds}s
          </p>
        </div>
      )}
    </main>
  );
}

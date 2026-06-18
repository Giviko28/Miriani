import { useEffect, useRef, useState } from "react";
import { api, type DocumentDto } from "../../api";
import { ROLE_NAMES } from "../../auth";
import { Badge, Button, Card, Select } from "../../ui";

const STATUS: Record<number, { label: string; tone: "slate" | "amber" | "green" | "red" }> = {
  0: { label: "Uploaded", tone: "slate" },
  1: { label: "Processing", tone: "amber" },
  2: { label: "Indexed", tone: "green" },
  3: { label: "Failed", tone: "red" },
};

export function Knowledge() {
  const [docs, setDocs] = useState<DocumentDto[]>([]);
  const [accessRole, setAccessRole] = useState(0);
  const [fileName, setFileName] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  async function refresh() {
    try {
      setDocs(await api.listDocuments());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load documents");
    }
  }

  useEffect(() => { refresh(); }, []);

  async function onUpload() {
    const file = fileRef.current?.files?.[0];
    if (!file) {
      setError("Choose a file first.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await api.uploadDocument(file, accessRole);
      if (fileRef.current) fileRef.current.value = "";
      setFileName(null);
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Upload failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-5">
      <h1 className="text-2xl font-semibold">Knowledge</h1>

      <Card>
        <h2 className="text-lg font-semibold">Upload a document</h2>
        <p className="mt-1 text-sm text-slate-500">
          PDF, Word, Excel, or text. It is indexed into the knowledge base and visible to the
          selected role and above.
        </p>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <input
            ref={fileRef}
            type="file"
            className="text-sm"
            accept=".pdf,.docx,.xlsx,.txt,.md,.csv"
            onChange={(e) => setFileName(e.target.files?.[0]?.name ?? null)}
          />
          <Select value={accessRole} onChange={(e) => setAccessRole(Number(e.target.value))}>
            <option value={0}>Visible to: Employee+</option>
            <option value={1}>Visible to: Manager+</option>
            <option value={2}>Visible to: Admin only</option>
          </Select>
          <Button onClick={onUpload} disabled={busy || !fileName}>{busy ? "Uploading…" : "Upload"}</Button>
        </div>
        {fileName && <p className="mt-2 text-xs text-slate-500">Selected: {fileName}</p>}
        {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
      </Card>

      <Card>
        <h2 className="text-lg font-semibold">Documents</h2>
        {docs.length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">No documents yet.</p>
        ) : (
          <table className="mt-3 w-full text-sm">
            <thead>
              <tr className="text-left text-slate-500">
                <th className="py-2">Name</th>
                <th>Access</th>
                <th>Status</th>
                <th>Size</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {docs.map((d) => (
                <tr key={d.id} className="border-t border-slate-100">
                  <td className="py-2 font-medium">{d.fileName}</td>
                  <td>{ROLE_NAMES[d.accessRole]}+</td>
                  <td><Badge tone={STATUS[d.status].tone}>{STATUS[d.status].label}</Badge></td>
                  <td className="text-slate-500">{(d.sizeBytes / 1024).toFixed(1)} KB</td>
                  <td className="text-right">
                    <button
                      onClick={async () => {
                        if (!confirm(`Delete "${d.fileName}"?`)) return;
                        try {
                          await api.deleteDocument(d.id);
                          await refresh();
                        } catch (err) {
                          setError(err instanceof Error ? err.message : "Delete failed");
                        }
                      }}
                      className="text-xs text-red-500 hover:text-red-700"
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}

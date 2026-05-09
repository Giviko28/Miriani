import { useEffect, useState } from "react";
import { api } from "../../api";

type DbStatus = {
  connected: boolean;
  dbType: string | null;
  schemaJson: string | null;
  updatedAt: string | null;
};

type SchemaTable = { name: string; columns: { name: string; type: string }[] };

const DB_TYPES = [
  { value: "sqlite", label: "SQLite", placeholder: "sqlite:///C:/path/to/database.db" },
  { value: "postgresql", label: "PostgreSQL", placeholder: "postgresql://user:password@localhost:5432/dbname" },
  { value: "mysql", label: "MySQL", placeholder: "mysql+pymysql://user:password@localhost:3306/dbname" },
  { value: "mssql", label: "SQL Server", placeholder: "mssql+pyodbc://user:password@localhost/dbname?driver=ODBC+Driver+17+for+SQL+Server" },
];

export function Database() {
  const [status, setStatus] = useState<DbStatus | null>(null);
  const [dbType, setDbType] = useState("sqlite");
  const [connStr, setConnStr] = useState("");
  const [saving, setSaving] = useState(false);
  const [exploring, setExploring] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    api.orgDb.get().then(setStatus).catch(() => setStatus({ connected: false, dbType: null, schemaJson: null, updatedAt: null }));
  }, []);

  const placeholder = DB_TYPES.find(t => t.value === dbType)?.placeholder ?? "";

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    if (!connStr.trim()) return;
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const result = await api.orgDb.save(dbType, connStr.trim());
      setStatus(result);
      setConnStr("");
      setSuccess("Database connected and schema indexed successfully.");
    } catch (err: any) {
      setError(err.message ?? "Failed to connect.");
    } finally {
      setSaving(false);
    }
  }

  async function handleExplore() {
    setExploring(true);
    setError(null);
    setSuccess(null);
    try {
      const result = await api.orgDb.explore();
      setStatus(s => s ? { ...s, schemaJson: result.summary } : s);
      setSuccess("Database explored and memorized. Mirian will use this context in every query.");
    } catch (err: any) {
      setError(err.message ?? "Exploration failed.");
    } finally {
      setExploring(false);
    }
  }

  async function handleDisconnect() {
    if (!confirm("Disconnect the database? Mirian will no longer be able to query it.")) return;
    try {
      await api.orgDb.disconnect();
      setStatus({ connected: false, dbType: null, schemaJson: null, updatedAt: null });
      setSuccess("Database disconnected.");
    } catch (err: any) {
      setError(err.message ?? "Failed to disconnect.");
    }
  }

  const tables: SchemaTable[] = (() => {
    if (!status?.schemaJson) return [];
    try {
      const parsed = JSON.parse(status.schemaJson);
      return parsed?.tables ?? parsed ?? [];
    } catch { return []; }
  })();

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">Database Connector</h1>
        <p className="mt-1 text-sm text-slate-500">
          Connect an external database so Mirian can query live data (e.g. vacation schedules, employee records).
          Only SELECT queries are ever executed — the AI cannot modify data.
        </p>
      </div>

      {/* Status banner */}
      {status?.connected && (
        <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-3 space-y-2">
          <div className="flex items-center justify-between">
            <div className="text-sm">
              <span className="font-medium text-green-800">Connected</span>
              <span className="ml-2 text-green-600">{status.dbType}</span>
              {status.updatedAt && (
                <span className="ml-2 text-green-500">· last updated {new Date(status.updatedAt).toLocaleString()}</span>
              )}
            </div>
            <button onClick={handleDisconnect} className="text-xs font-medium text-red-600 hover:text-red-800">
              Disconnect
            </button>
          </div>
          <button
            onClick={handleExplore}
            disabled={exploring}
            className="rounded-md bg-green-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-800 disabled:opacity-50"
          >
            {exploring ? "Exploring…" : "Explore & Memorize"}
          </button>
          {status.schemaJson && status.schemaJson.length > 50 && (
            <details className="rounded-md border border-green-200 bg-white">
              <summary className="cursor-pointer px-3 py-1.5 text-xs font-medium text-green-700">Memorized Description</summary>
              <p className="whitespace-pre-wrap px-3 pb-3 pt-1 text-xs text-slate-600">{status.schemaJson}</p>
            </details>
          )}
        </div>
      )}

      {error && <p className="rounded-md bg-red-50 px-4 py-2 text-sm text-red-700">{error}</p>}
      {success && <p className="rounded-md bg-green-50 px-4 py-2 text-sm text-green-700">{success}</p>}

      {/* Connection form */}
      <form onSubmit={handleSave} className="space-y-4 rounded-lg border border-slate-200 bg-white p-5">
        <h2 className="text-sm font-semibold text-slate-800">
          {status?.connected ? "Update Connection" : "Connect a Database"}
        </h2>

        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">Database Type</label>
          <select
            value={dbType}
            onChange={e => setDbType(e.target.value)}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-400"
          >
            {DB_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>

        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">Connection String</label>
          <input
            type="text"
            value={connStr}
            onChange={e => setConnStr(e.target.value)}
            placeholder={placeholder}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-slate-400"
          />
          <p className="mt-1 text-xs text-slate-400">Uses SQLAlchemy URL format. The connection string is stored securely per organization.</p>
        </div>

        <button
          type="submit"
          disabled={saving || !connStr.trim()}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {saving ? "Connecting…" : "Save & Connect"}
        </button>
      </form>

      {/* Schema preview */}
      {tables.length > 0 && (
        <div className="space-y-3">
          <h2 className="text-sm font-semibold text-slate-800">Indexed Schema ({tables.length} table{tables.length !== 1 ? "s" : ""})</h2>
          <div className="space-y-2">
            {tables.map((table) => (
              <details key={table.name} className="rounded-md border border-slate-200 bg-white">
                <summary className="cursor-pointer px-4 py-2 text-sm font-medium text-slate-700">
                  {table.name}
                  <span className="ml-2 text-xs text-slate-400">{table.columns.length} column{table.columns.length !== 1 ? "s" : ""}</span>
                </summary>
                <div className="border-t border-slate-100 px-4 py-2">
                  <table className="w-full text-xs">
                    <thead>
                      <tr className="text-left text-slate-400">
                        <th className="pb-1 pr-4 font-medium">Column</th>
                        <th className="pb-1 font-medium">Type</th>
                      </tr>
                    </thead>
                    <tbody>
                      {table.columns.map((col) => (
                        <tr key={col.name} className="border-t border-slate-50">
                          <td className="py-1 pr-4 font-mono text-slate-700">{col.name}</td>
                          <td className="py-1 text-slate-400">{col.type}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </details>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

import { useEffect, useRef, useState } from "react";
import { api } from "../../api";
import { useBranding, BrandHeader } from "../../branding";
import { Button, Card, Input } from "../../ui";

const DEFAULT_ACCENT = "#4f46e5";

/**
 * Company profile / branding settings. Admins set the display name, tagline, accent color, and
 * logo that brand the in-app header for everyone in their organization.
 */
export function Company() {
  const { branding, logoSrc, refresh } = useBranding();
  const [displayName, setDisplayName] = useState("");
  const [tagline, setTagline] = useState("");
  const [accentColor, setAccentColor] = useState(DEFAULT_ACCENT);
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  // Seed the form from the loaded branding (once it arrives / changes).
  useEffect(() => {
    if (!branding) return;
    setDisplayName(branding.displayName ?? "");
    setTagline(branding.tagline ?? "");
    setAccentColor(branding.accentColor ?? DEFAULT_ACCENT);
  }, [branding]);

  function flash(msg: string) {
    setNote(msg);
    setError(null);
    setTimeout(() => setNote(null), 3000);
  }

  async function act(fn: () => Promise<unknown>, ok: string) {
    setBusy(true);
    setError(null);
    setNote(null);
    try {
      await fn();
      await refresh();
      flash(ok);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Action failed");
    } finally {
      setBusy(false);
    }
  }

  const save = () =>
    act(() => api.org.updateBranding({ displayName, tagline, accentColor }), "Branding saved.");

  function onPickLogo(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    act(() => api.org.uploadLogo(file), "Logo uploaded.");
    if (fileRef.current) fileRef.current.value = "";
  }

  return (
    <div className="space-y-5">
      <h1 className="text-2xl font-semibold">Company</h1>
      <p className="text-sm text-slate-500">
        Brand the workspace for your organization. The logo, name, and accent color appear in the
        app header for everyone signed in. Miriani remains the assistant in chat.
      </p>
      {note && <p className="text-sm font-medium text-emerald-700">✓ {note}</p>}
      {error && <p className="text-sm text-red-600">{error}</p>}

      {/* Live preview of the header lockup */}
      <Card>
        <div className="mb-3 text-xs font-medium uppercase tracking-wide text-slate-400">Header preview</div>
        <div className="rounded-xl border border-slate-200 bg-slate-50/60 p-4">
          <BrandHeader subtitle={tagline || undefined} />
        </div>
      </Card>

      {/* Logo */}
      <Card>
        <div className="flex items-center gap-4">
          {logoSrc ? (
            <img src={logoSrc} alt="Logo" className="h-16 w-16 rounded-lg border border-slate-200 object-contain" />
          ) : (
            <div className="flex h-16 w-16 items-center justify-center rounded-lg border border-dashed border-slate-300 text-xs text-slate-400">
              No logo
            </div>
          )}
          <div className="flex-1">
            <div className="font-medium text-slate-800">Company logo</div>
            <p className="text-xs text-slate-500">PNG, JPG, or SVG. Max 1&nbsp;MB.</p>
            <div className="mt-2 flex gap-2">
              <input ref={fileRef} type="file" accept="image/*" className="hidden" onChange={onPickLogo} />
              <Button onClick={() => fileRef.current?.click()} disabled={busy}>
                {branding?.hasLogo ? "Replace logo" : "Upload logo"}
              </Button>
              {branding?.hasLogo && (
                <button
                  onClick={() => act(() => api.org.removeLogo(), "Logo removed.")}
                  disabled={busy}
                  className="rounded-lg px-3 py-2 text-sm font-medium text-slate-500 hover:bg-slate-100 disabled:opacity-50"
                >
                  Remove
                </button>
              )}
            </div>
          </div>
        </div>
      </Card>

      {/* Text + color fields */}
      <Card>
        <div className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Display name</label>
            <Input
              placeholder={branding?.companyName ?? "Your company name"}
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Tagline</label>
            <Input
              placeholder="e.g. Customer & IT support"
              value={tagline}
              onChange={(e) => setTagline(e.target.value)}
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Accent color</label>
            <div className="flex items-center gap-3">
              <input
                type="color"
                value={accentColor}
                onChange={(e) => setAccentColor(e.target.value)}
                className="h-10 w-14 cursor-pointer rounded-lg border border-slate-200 bg-white p-1"
              />
              <Input
                className="w-32"
                value={accentColor}
                onChange={(e) => setAccentColor(e.target.value)}
              />
            </div>
          </div>
          <div className="flex justify-end">
            <Button onClick={save} disabled={busy}>{busy ? "Saving…" : "Save changes"}</Button>
          </div>
        </div>
      </Card>
    </div>
  );
}

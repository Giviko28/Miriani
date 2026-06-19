import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import { api, type OrgBranding } from "./api";
import { useAuth } from "./auth";

const DEFAULT_ACCENT = "#4f46e5"; // indigo-600, the app's resting accent

type BrandingContextValue = {
  branding: OrgBranding | null;
  logoSrc: string | null;
  refresh: () => Promise<void>;
};

const BrandingContext = createContext<BrandingContextValue | null>(null);

/**
 * Loads the tenant's company branding once a session exists and exposes it app-wide. Also pushes
 * the accent color into a CSS variable (`--brand-accent`) so brand elements can tint themselves,
 * and resolves the logo to an object URL (the image request needs the bearer token, which an
 * <img src> can't send).
 */
export function BrandingProvider({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  const [branding, setBranding] = useState<OrgBranding | null>(null);
  const [logoSrc, setLogoSrc] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (!session) {
      setBranding(null);
      return;
    }
    try {
      const b = await api.org.branding();
      setBranding(b);
      document.documentElement.style.setProperty("--brand-accent", b.accentColor || DEFAULT_ACCENT);
      if (b.hasLogo) {
        const url = await api.org.logoObjectUrl();
        setLogoSrc((prev) => {
          if (prev) URL.revokeObjectURL(prev);
          return url;
        });
      } else {
        setLogoSrc((prev) => {
          if (prev) URL.revokeObjectURL(prev);
          return null;
        });
      }
    } catch {
      /* branding is best-effort; the app still works without it */
    }
  }, [session]);

  useEffect(() => {
    if (session) {
      refresh();
    } else {
      setBranding(null);
      setLogoSrc((prev) => {
        if (prev) URL.revokeObjectURL(prev);
        return null;
      });
      document.documentElement.style.setProperty("--brand-accent", DEFAULT_ACCENT);
    }
  }, [session, refresh]);

  return (
    <BrandingContext.Provider value={{ branding, logoSrc, refresh }}>
      {children}
    </BrandingContext.Provider>
  );
}

export function useBranding() {
  const ctx = useContext(BrandingContext);
  if (!ctx) throw new Error("useBranding must be used within BrandingProvider");
  return ctx;
}

/** First letters of the company name, for the logo-less monogram fallback. */
function monogram(name: string): string {
  return name
    .split(/\s+/)
    .map((w) => w[0])
    .filter(Boolean)
    .slice(0, 2)
    .join("")
    .toUpperCase();
}

/**
 * The tenant's brand lockup (logo or monogram + company name + optional tagline). Shown in the app
 * header/sidebars — this is the customer's company brand; the assistant stays "Miriani" in chat.
 */
export function BrandHeader({ subtitle, compact = false }: { subtitle?: string; compact?: boolean }) {
  const { branding, logoSrc } = useBranding();
  const name = branding?.displayName || branding?.companyName || "Workspace";
  const tagline = subtitle ?? branding?.tagline ?? null;

  return (
    <div className="flex items-center gap-2.5">
      {logoSrc ? (
        <img
          src={logoSrc}
          alt={name}
          className={(compact ? "h-7 w-7" : "h-9 w-9") + " shrink-0 rounded-lg object-contain"}
        />
      ) : (
        <div
          className={
            (compact ? "h-7 w-7 text-xs" : "h-9 w-9 text-sm") +
            " flex shrink-0 items-center justify-center rounded-lg font-bold text-white"
          }
          style={{ backgroundColor: "var(--brand-accent)" }}
        >
          {monogram(name)}
        </div>
      )}
      <div className="min-w-0">
        <div className="truncate font-semibold tracking-tight text-slate-800">{name}</div>
        {tagline && <div className="truncate text-xs text-slate-400">{tagline}</div>}
      </div>
    </div>
  );
}

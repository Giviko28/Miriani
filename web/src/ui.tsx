// Lightweight shadcn-style primitives (Tailwind). Kept minimal and local so the UI has a
// consistent look without pulling the full shadcn toolchain.
import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from "react";

export function cn(...parts: (string | false | undefined)[]) {
  return parts.filter(Boolean).join(" ");
}

export function Button({ className, ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      className={cn(
        "inline-flex items-center justify-center rounded-lg bg-slate-900 px-4 py-2.5 text-sm font-medium text-white shadow-sm hover:shadow-md",
        "transition-all duration-200 hover:bg-slate-800 active:scale-[0.98] disabled:pointer-events-none disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}

export function Input({ className, ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={cn(
        "w-full rounded-lg border border-slate-200 bg-white/60 px-3 py-2 text-sm outline-none transition-all duration-200",
        "focus:border-slate-400 focus:bg-white focus:ring-4 focus:ring-slate-100",
        className,
      )}
      {...props}
    />
  );
}

export function Select({ className, children, ...props }: SelectHTMLAttributes<HTMLSelectElement> & { children: ReactNode }) {
  return (
    <select
      className={cn(
        "rounded-lg border border-slate-200 bg-white/60 px-3 py-2 text-sm outline-none transition-all duration-200 focus:border-slate-400 focus:bg-white focus:ring-4 focus:ring-slate-100",
        className,
      )}
      {...props}
    >
      {children}
    </select>
  );
}

export function Card({ className, children }: { className?: string; children: ReactNode }) {
  return <div className={cn("rounded-2xl border border-slate-200/60 bg-white p-5 shadow-sm transition-all duration-300 hover:shadow-md", className)}>{children}</div>;
}

const BADGE_TONES: Record<string, string> = {
  slate: "bg-slate-100/80 text-slate-700 ring-1 ring-inset ring-slate-200",
  green: "bg-emerald-50 text-emerald-700 ring-1 ring-inset ring-emerald-200/60",
  amber: "bg-amber-50 text-amber-800 ring-1 ring-inset ring-amber-200/60",
  red: "bg-rose-50 text-rose-700 ring-1 ring-inset ring-rose-200/60",
  blue: "bg-indigo-50 text-indigo-700 ring-1 ring-inset ring-indigo-200/60",
};

export function Badge({ tone = "slate", children }: { tone?: keyof typeof BADGE_TONES; children: ReactNode }) {
  return (
    <span className={cn("inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold tracking-wide", BADGE_TONES[tone])}>
      {children}
    </span>
  );
}

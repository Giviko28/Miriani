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
        "inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white",
        "transition hover:bg-slate-700 disabled:cursor-not-allowed disabled:opacity-50",
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
        "w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none",
        "focus:border-slate-500 focus:ring-2 focus:ring-slate-200",
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
        "rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-slate-500",
        className,
      )}
      {...props}
    >
      {children}
    </select>
  );
}

export function Card({ className, children }: { className?: string; children: ReactNode }) {
  return <div className={cn("rounded-xl border border-slate-200 bg-white p-5 shadow-sm", className)}>{children}</div>;
}

const BADGE_TONES: Record<string, string> = {
  slate: "bg-slate-100 text-slate-700",
  green: "bg-green-100 text-green-700",
  amber: "bg-amber-100 text-amber-800",
  red: "bg-red-100 text-red-700",
  blue: "bg-blue-100 text-blue-700",
};

export function Badge({ tone = "slate", children }: { tone?: keyof typeof BADGE_TONES; children: ReactNode }) {
  return (
    <span className={cn("inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium", BADGE_TONES[tone])}>
      {children}
    </span>
  );
}

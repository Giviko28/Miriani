import { useEffect, useRef, useState, type ReactNode } from "react";

/**
 * Public landing page. Plain and restrained: white background, ink text, a single clean sans
 * typeface, with light scroll-reveal. The hero shows a looping chat demo of someone typing a
 * question and Miriani answering. Header is just the logo and a login action.
 */

const AGENTS = [
  { n: "01", title: "Communication", desc: "Drafts emails, replies, and announcements in your tone, ready for you to send." },
  { n: "02", title: "Support & tickets", desc: "Turns any reported issue into a structured ticket and files it with the right team." },
  { n: "03", title: "Documents", desc: "Reads, summarizes, and answers questions about the files you hand it." },
  { n: "04", title: "Data & reporting", desc: "Pulls precise answers and assembles reports from your connected systems." },
  { n: "05", title: "Approvals & workflows", desc: "Routes requests for sign-off and follows multi-step processes through to the end." },
];

const STEPS = [
  { k: "Ask", d: "Describe what you need in plain language, and attach a document if it helps." },
  { k: "Review", d: "An agent drafts the result and lays it out for you to check." },
  { k: "Act", d: "One click performs the real action, whether sending, filing, or generating it, and records it." },
];

const CAPABILITIES = [
  { term: "Knowledge base", desc: "Upload your policies, runbooks, and manuals. Answers stay grounded in what your own documents actually say." },
  { term: "Attach a file mid-chat", desc: "Drop a document into a single message for one-off analysis. It's read on the spot, never stored." },
  { term: "Connected databases", desc: "Ask plain questions about your operational data and get an exact, current answer back." },
  { term: "Role-based access", desc: "Employees, managers, and admins each see only what they should, scoped per organization." },
  { term: "Your branding", desc: "Each company sets its own name, logo, and accent color, applied across the workspace." },
  { term: "Audit trail", desc: "Every action an agent takes on your behalf is logged, so nothing happens unseen." },
];

const SCRIPT = [
  {
    q: "Draft a leave request for next Monday and Tuesday.",
    a: "Done. I've drafted your leave for Monday and Tuesday and prepared the approval email to your manager. Want me to send it?",
  },
  {
    q: "Summarize the contract I just uploaded.",
    a: "It's a 12-month service agreement. Two things to watch: it auto-renews 60 days out, and liability is capped at ₾50,000.",
  },
];

function useReveal() {
  useEffect(() => {
    const els = Array.from(document.querySelectorAll<HTMLElement>(".landing-root [data-reveal]"));
    if (!("IntersectionObserver" in window)) {
      els.forEach((el) => el.classList.add("is-visible"));
      return;
    }
    const io = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            entry.target.classList.add("is-visible");
            io.unobserve(entry.target);
          }
        }
      },
      { threshold: 0.12, rootMargin: "0px 0px -8% 0px" },
    );
    els.forEach((el) => io.observe(el));
    return () => io.disconnect();
  }, []);
}

function useScrolled(threshold = 8) {
  const [scrolled, setScrolled] = useState(false);
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > threshold);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, [threshold]);
  return scrolled;
}

function Reveal({ children, delay = 0, className }: { children: ReactNode; delay?: number; className?: string }) {
  return (
    <div data-reveal style={{ animationDelay: `${delay}ms` }} className={className}>
      {children}
    </div>
  );
}

function Arrow({ className = "" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <path d="M5 12h14M13 6l6 6-6 6" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

type Msg = { role: "user" | "bot"; text: string };

/** Looping hero chat demo: a question is typed, sent, Miriani "thinks", then answers. */
function ChatDemo() {
  const [messages, setMessages] = useState<Msg[]>([]);
  const [input, setInput] = useState("");
  const [botTyping, setBotTyping] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, botTyping]);

  useEffect(() => {
    const reduce = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
    if (reduce) {
      setMessages([{ role: "user", text: SCRIPT[0].q }, { role: "bot", text: SCRIPT[0].a }]);
      return;
    }

    let cancelled = false;
    const sleep = (ms: number) => new Promise<void>((r) => setTimeout(r, ms));
    const type = async (text: string) => {
      for (let i = 1; i <= text.length; i++) {
        if (cancelled) return;
        setInput(text.slice(0, i));
        await sleep(26);
      }
    };

    async function run() {
      while (!cancelled) {
        for (const turn of SCRIPT) {
          if (cancelled) return;
          setMessages([]);
          setInput("");
          setBotTyping(false);
          await sleep(400);
          await type(turn.q);
          await sleep(450);
          if (cancelled) return;
          setMessages([{ role: "user", text: turn.q }]);
          setInput("");
          await sleep(550);
          setBotTyping(true);
          await sleep(1400);
          if (cancelled) return;
          setBotTyping(false);
          setMessages((m) => [...m, { role: "bot", text: turn.a }]);
          await sleep(3600);
        }
      }
    }
    run();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="flex h-[420px] select-none flex-col overflow-hidden rounded-lg border border-[#E4E4E7] bg-white">
      {/* header */}
      <div className="flex items-center gap-2.5 border-b border-[#EFEFF1] px-4 py-3">
        <img src="/miriani-logo.png" alt="" className="h-6 w-6 object-contain" />
        <span className="font-display text-sm font-semibold">Miriani</span>
      </div>

      {/* messages */}
      <div ref={scrollRef} className="flex-1 space-y-3 overflow-hidden px-4 py-4">
        {messages.map((m, i) =>
          m.role === "user" ? (
            <div key={i} className="chat-msg flex justify-end">
              <div className="max-w-[82%] rounded-2xl rounded-br-sm bg-[#22425F] px-4 py-2.5 text-sm leading-relaxed text-white">
                {m.text}
              </div>
            </div>
          ) : (
            <div key={i} className="chat-msg flex justify-start">
              <div className="max-w-[88%] rounded-2xl rounded-bl-sm bg-[#F4F4F5] px-4 py-2.5 text-sm leading-relaxed text-[#27272A]">
                {m.text}
              </div>
            </div>
          ),
        )}
        {botTyping && (
          <div className="chat-msg flex justify-start">
            <div className="flex items-center gap-1 rounded-2xl rounded-bl-sm bg-[#F4F4F5] px-4 py-3">
              {[0, 1, 2].map((d) => (
                <span key={d} className="chat-dot h-1.5 w-1.5 rounded-full bg-[#A1A1AA]" style={{ animationDelay: `${d * 0.18}s` }} />
              ))}
            </div>
          </div>
        )}
      </div>

      {/* input */}
      <div className="flex items-center gap-2 border-t border-[#EFEFF1] px-3 py-3">
        <div className="flex h-10 flex-1 items-center rounded-lg border border-[#E4E4E7] bg-[#FAFAFA] px-3 text-sm text-[#27272A]">
          {input ? (
            <span>
              {input}
              <span className="chat-caret ml-0.5 inline-block w-px self-stretch border-l border-[#27272A]" />
            </span>
          ) : (
            <span className="text-[#A1A1AA]">Message Miriani…</span>
          )}
        </div>
        <span className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[#22425F] text-white">
          <Arrow className="h-4 w-4" />
        </span>
      </div>
    </div>
  );
}

export function Landing({ onLogin }: { onLogin: () => void }) {
  useReveal();
  const scrolled = useScrolled();

  const goto = (id: string) => () => document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "start" });

  return (
    <div className="landing-root min-h-screen bg-white text-[#18181B] antialiased">
      {/* ---------- Header ---------- */}
      <header
        className={
          "sticky top-0 z-50 transition-all duration-300 " +
          (scrolled ? "border-b border-[#E4E4E7] bg-white/85 backdrop-blur-md" : "border-b border-transparent")
        }
      >
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-6">
          <button onClick={goto("top")} className="flex items-center gap-2.5" aria-label="Miriani home">
            <img src="/miriani-logo.png" alt="" className="h-8 w-8 object-contain" />
            <span className="font-display text-xl font-semibold tracking-tight">Miriani</span>
          </button>

          <button
            onClick={onLogin}
            className="group inline-flex items-center gap-2 rounded-md bg-[#22425F] px-4 py-2 text-sm font-medium text-white transition-all duration-200 hover:bg-[#1A3349] active:scale-[0.98]"
          >
            Log in
            <Arrow className="h-4 w-4 transition-transform duration-200 group-hover:translate-x-0.5" />
          </button>
        </div>
      </header>

      <main id="top">
        {/* ---------- Hero ---------- */}
        <section className="mx-auto max-w-6xl px-6 pt-16 pb-24 sm:pt-20 sm:pb-28">
          <div className="grid items-center gap-12 lg:grid-cols-2 lg:gap-16">
            <div>
              <Reveal delay={0}>
                <h1 className="font-display text-[2.5rem] font-semibold leading-[1.08] tracking-tight sm:text-[3.4rem]">
                  Your company's processes, handled in a single conversation.
                </h1>
              </Reveal>

              <Reveal delay={100}>
                <p className="mt-7 max-w-xl text-lg leading-relaxed text-[#52525B]">
                  Miriani drafts the email, the ticket, the invoice, or the report, grounded in your own
                  documents, then performs the real action the moment you approve it.
                </p>
              </Reveal>

              <Reveal delay={190}>
                <div className="mt-10 flex flex-wrap items-center gap-x-6 gap-y-3">
                  <button
                    onClick={onLogin}
                    className="group inline-flex items-center gap-2 rounded-md bg-[#22425F] px-6 py-3 text-sm font-medium text-white transition-all duration-200 hover:bg-[#1A3349] active:scale-[0.98]"
                  >
                    Log in to your workspace
                    <Arrow className="h-4 w-4 transition-transform duration-200 group-hover:translate-x-1" />
                  </button>
                  <button
                    onClick={goto("agents")}
                    className="group inline-flex items-center gap-1.5 text-sm font-medium text-[#22425F]"
                  >
                    See how it works
                    <span className="transition-transform duration-200 group-hover:translate-y-0.5">↓</span>
                  </button>
                </div>
              </Reveal>
            </div>

            <Reveal delay={160}>
              <ChatDemo />
            </Reveal>
          </div>
        </section>

        {/* ---------- Agents (generic) ---------- */}
        <section id="agents" className="mx-auto max-w-6xl px-6 pb-24">
          <Reveal>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
              <h2 className="font-display max-w-xl text-3xl font-semibold tracking-tight sm:text-4xl">
                A specialist agent for whatever you need.
              </h2>
              <p className="max-w-sm text-sm leading-relaxed text-[#71717A]">
                Every request is routed to the agent built for that kind of work, and tuned to how your
                company runs.
              </p>
            </div>
          </Reveal>

          <div className="mt-12 border-t border-[#E4E4E7]">
            {AGENTS.map((a, i) => (
              <Reveal key={a.n} delay={i * 55}>
                <div className="grid grid-cols-1 gap-2 border-b border-[#E4E4E7] py-7 transition-colors duration-200 hover:bg-[#FAFAFA] sm:grid-cols-12 sm:items-baseline sm:gap-6 sm:px-3">
                  <div className="text-sm text-[#A1A1AA] sm:col-span-1">{a.n}</div>
                  <h3 className="font-display text-xl font-semibold tracking-tight sm:col-span-3">{a.title}</h3>
                  <p className="text-[15px] leading-relaxed text-[#52525B] sm:col-span-8">{a.desc}</p>
                </div>
              </Reveal>
            ))}
          </div>
        </section>

        {/* ---------- How it works ---------- */}
        <section className="border-t border-[#E4E4E7]">
          <div className="mx-auto max-w-6xl px-6 py-20 sm:py-24">
            <Reveal>
              <h2 className="font-display text-3xl font-semibold tracking-tight sm:text-4xl">How it works</h2>
            </Reveal>
            <div className="mt-12 grid gap-px overflow-hidden rounded-xl border border-[#E4E4E7] bg-[#E4E4E7] sm:grid-cols-3">
              {STEPS.map((s, i) => (
                <Reveal key={s.k} delay={i * 90} className="h-full">
                  <div className="flex h-full flex-col bg-white p-7">
                    <span className="text-sm font-medium text-[#A1A1AA]">{`0${i + 1}`}</span>
                    <h3 className="font-display mt-4 text-2xl font-semibold tracking-tight">{s.k}</h3>
                    <p className="mt-3 text-[15px] leading-relaxed text-[#52525B]">{s.d}</p>
                  </div>
                </Reveal>
              ))}
            </div>
          </div>
        </section>

        {/* ---------- Capabilities ---------- */}
        <section className="border-t border-[#E4E4E7]">
          <div className="mx-auto max-w-6xl px-6 py-20 sm:py-24">
            <Reveal>
              <h2 className="font-display max-w-2xl text-3xl font-semibold tracking-tight sm:text-4xl">
                Everything runs on your organization's own knowledge.
              </h2>
            </Reveal>

            <div className="mt-12 grid gap-x-12 gap-y-10 sm:grid-cols-2 lg:grid-cols-3">
              {CAPABILITIES.map((c, i) => (
                <Reveal key={c.term} delay={(i % 3) * 70}>
                  <div>
                    <div className="h-px w-8 bg-[#22425F]" />
                    <h3 className="font-display mt-4 text-lg font-semibold tracking-tight">{c.term}</h3>
                    <p className="mt-2 text-[15px] leading-relaxed text-[#71717A]">{c.desc}</p>
                  </div>
                </Reveal>
              ))}
            </div>
          </div>
        </section>

        {/* ---------- Closing CTA ---------- */}
        <section className="border-t border-[#E4E4E7] bg-[#22425F] text-white">
          <div className="mx-auto max-w-6xl px-6 py-20 text-center sm:py-24">
            <Reveal>
              <h2 className="font-display mx-auto max-w-2xl text-3xl font-semibold leading-tight tracking-tight sm:text-4xl">
                Sign in and let the routine work take care of itself.
              </h2>
            </Reveal>
            <Reveal delay={120}>
              <div className="mt-9 flex flex-col items-center gap-3">
                <button
                  onClick={onLogin}
                  className="group inline-flex items-center gap-2 rounded-md bg-white px-6 py-3 text-sm font-medium text-[#22425F] transition-all duration-200 hover:bg-[#EEF1F4] active:scale-[0.98]"
                >
                  Log in
                  <Arrow className="h-4 w-4 transition-transform duration-200 group-hover:translate-x-1" />
                </button>
                <p className="text-sm text-white/60">Accounts are created by your administrator.</p>
              </div>
            </Reveal>
          </div>
        </section>
      </main>

      {/* ---------- Footer ---------- */}
      <footer className="border-t border-[#E4E4E7] bg-white">
        <div className="mx-auto flex max-w-6xl flex-col gap-8 px-6 py-12 sm:flex-row sm:items-end sm:justify-between">
          <div className="flex flex-col gap-4">
            <div className="flex items-center gap-2.5">
              <img src="/miriani-logo.png" alt="" className="h-7 w-7 object-contain" />
              <span className="font-display text-lg font-semibold tracking-tight">Miriani</span>
            </div>
            <img src="/caucasus-logo.png" alt="Caucasus University" className="h-12 w-auto object-contain" />
          </div>
          <div className="text-sm text-[#71717A] sm:text-right">
            <p>Caucasus University, School of Technology · 2026</p>
            <p className="mt-1 text-[#A1A1AA]">Givi Chelidze · Supervised by Prof. Maksim Iavich</p>
          </div>
        </div>
      </footer>
    </div>
  );
}

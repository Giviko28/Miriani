# 15-Minute Presentation & Demo Outline

A suggested structure for the defense presentation and live demo.

## Slides (~9 min)

1. **Title** (15s) — project, author, supervisor.
2. **The problem** (1.5 min) — rule-based RPA breaks when requirements change; many
   knowledge tasks (policy Q&A, drafting, summarizing, routing) still done by hand because
   they need *language understanding*. Cite the McKinsey 2024 figure ($2.6–4.4T potential).
3. **The idea** (1.5 min) — a local LLM + RAG + agents that adapt to natural language,
   grounded in the company's own documents, with role-based access and no paid API.
4. **Architecture** (2 min) — the 3-service diagram (React → .NET gateway → Python AI →
   Ollama; MS SQL + ChromaDB). Stress the security boundary and "AI brain is stateless."
5. **How RAG works here** (1.5 min) — ingest → chunk → embed → store → role-filtered
   retrieve → grounded answer. Explain hallucination mitigation and role filtering.
6. **The agents** (1 min) — router + 5 specialists; invoice totals computed server-side.
7. **Results** (1 min) — accuracy 100% on the eval set (target ≥80%), p95 latency ~2 s
   (target <3 s), 6 processes automated, runs on a modest GPU.

## Live demo (~5 min)

> Have all four services running beforehand and do one warm-up query so the model is loaded.

1. **Sign in** as a **Manager**. Show the role badge.
2. **Upload** a policy document (Employee-visible) and a confidential one (Manager-only).
   Show status flip to *Indexed*.
3. **Policy Q&A** — ask a question answered from the policy doc; point out the **sources**.
4. **Summarize** the same document — show the router chose the Summarizer.
5. **Invoice** — "Create an invoice for ACME: 10 hours consulting at 150 GEL"; show the
   structured JSON and the **server-computed total**.
6. **The RBAC moment** (the key slide-stealer): sign out, sign in as an **Employee**, ask
   about the confidential document → the system has no information, because that document
   is invisible to the Employee role. Same question, different role, different access.

## Closing (~1 min)

- Recap: a working prototype that automates 6 knowledge processes, locally and securely.
- Limitations: MVP scope, single-org, depends on local hardware.
- Future work: multi-tenancy, more agents, production hardening, open-sourcing.

## Backup / Q&A prep

- *Why local LLM?* Cost + data privacy; trade-off is hardware-bound latency.
- *Why not call the AI service from React directly?* Single security boundary; role must
  come from a verified JWT, not the client.
- *How do you prevent hallucination?* Strict "use only retrieved context" prompt; decline
  when no context; server-side arithmetic for invoices; source citations for auditability.
- *Accuracy caveat:* measured on a curated eval set; real-world accuracy depends on document
  quality and question complexity.

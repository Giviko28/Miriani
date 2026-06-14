# Architecture

Technical documentation for the Business Process Automation system — an ERP-like web
application that automates knowledge-based business processes with a **local** LLM,
grounded by Retrieval-Augmented Generation (RAG) and orchestrated by an agentic system.

## 1. Overview

The system lets a company upload its documents (policies, handbooks, spreadsheets) into a
knowledge base, then ask questions or request tasks in natural language. A router agent
decides which specialized agent should handle each request; answers are grounded in the
documents the **caller's role** is allowed to see, which keeps confidential information
from leaking and reduces hallucination.

Everything runs locally — no paid LLM API — which is a core constraint of the project.

## 2. Components

```
┌──────────────┐   JWT / HTTPS   ┌──────────────────┐   SQL    ┌──────────────┐
│  React SPA   │ ───────────────▶│  .NET 10 Web API │ ───────▶ │  MS SQL      │
│ (web/, Vite) │                 │  (api/, gateway) │          │  Server      │
└──────────────┘                 └────────┬─────────┘          └──────────────┘
                                          │ internal REST
                                          ▼
                                 ┌──────────────────┐   embed/search  ┌──────────┐
                                 │ Python FastAPI    │ ──────────────▶ │ ChromaDB │
                                 │ (ai-service/)     │                 │ (vectors)│
                                 │ RAG + LangGraph   │                 └──────────┘
                                 └────────┬─────────┘
                                          │ generate / embed
                                          ▼
                                    ┌──────────┐
                                    │  Ollama  │  qwen2.5:3b (chat)
                                    │ (local)  │  nomic-embed-text (embeddings)
                                    └──────────┘
```

| Component | Tech | Responsibility |
|-----------|------|----------------|
| `web/` | React 18, TypeScript, Vite, Tailwind | UI: auth, document management, AI assistant |
| `api/` | .NET 10, ASP.NET Core, EF Core | Gateway: identity, RBAC, business data, file intake, AI relay |
| `ai-service/` | Python 3.12, FastAPI, LangChain, LangGraph | RAG pipeline and agent orchestration |
| MS SQL Server | Docker | Users, roles, orgs, document metadata, audit |
| ChromaDB | embedded, persistent | Vector store (embeddings + chunks) |
| Ollama | host process | Local chat + embedding models |

**Security boundary.** The React app talks *only* to the .NET API. The Python service is
a stateless "AI brain" that never decides access on its own — the .NET gateway always
supplies the caller's `org_id` and role, both read from the verified JWT (never from a
request body the client could forge).

## 3. Data model (MS SQL)

- **Organization** — a tenant company. Every org-owned row carries `OrgId`, so the
  single-org MVP can become multi-tenant without a rewrite.
- **User** — `Email`, `PasswordHash` (BCrypt), `Role` (Employee < Manager < Admin).
- **Document** — file metadata: `FileName`, `StoragePath`, `Status`
  (Uploaded→Processing→Indexed/Failed), and `AccessRole` (minimum role to retrieve it).
- **BusinessProcess** — an automatable process with an `AgentKey` linking it to an agent.
- **AuditLog** — append-only record of notable actions.

File bytes live on disk (`data/uploads/<orgId>/`); only metadata is in SQL. Vectors live
in ChromaDB.

## 4. Authentication & RBAC

1. `POST /api/auth/register` / `login` issue a JWT (HMAC-SHA256) carrying `sub` (user id),
   `org`, and `role` claims.
2. Protected endpoints require the bearer token; `[Authorize(Roles = "Admin")]` enforces
   role gates at the API.
3. For AI calls, the gateway extracts `org_id` + role from the JWT and passes them to the
   AI service. Retrieval is filtered to documents where
   `org_id == caller.org AND access_role <= caller.roleLevel`. A user cannot widen their
   own access because the role never comes from the request.

## 5. RAG pipeline (ai-service)

1. **Ingest** (`/ingest`): extract text (pypdf / python-docx / openpyxl / plain text) →
   chunk (LangChain `RecursiveCharacterTextSplitter`, 800 chars / 120 overlap) →
   embed each chunk via Ollama `nomic-embed-text` (768-dim) → store in ChromaDB with
   metadata `{org_id, doc_id, file_name, access_role, chunk_index}`.
2. **Retrieve** (`/rag/retrieve`): embed the query, search the collection with a
   `where` filter on `org_id` and `access_role`, return the top-k chunks (cosine).
3. **Answer** (`/rag/query`): build a context block from retrieved chunks and prompt the
   LLM with a strict "use only this context" system prompt. If nothing is retrieved, the
   service returns a "no information" answer instead of inventing one (anti-hallucination).

Embeddings use Ollama rather than sentence-transformers to avoid a multi-GB PyTorch
install and keep the stack fully local.

## 6. Agentic system (LangGraph)

`/agent/run` executes a small graph:

```
        ┌─────────┐  route == "policy_qa"   ┌────────────┐
        │ router  │ ───────────────────────▶│ policy_qa  │
        │ (agent) │  route == "invoice_gen" ├────────────┤
        └─────────┘ ───────────────────────▶│ invoice_gen│ ─▶ END
                     ... one edge per agent  └────────────┘
```

- **Router** — a keyword heuristic with an LLM fallback classifies the request into one
  agent key.
- **Specialists** — `policy_qa` (RAG), `doc_summary`, `email_draft`, `report_draft` (all
  role-scoped via RAG), and `invoice_gen`.
- **Invoice generation** produces structured JSON; **totals are computed server-side** (the
  LLM only extracts line items), so arithmetic is never trusted to the model — important
  because the proposal flags financial operations as the highest-risk area for
  hallucination.

## 7. Request flow (AI assistant)

```
User types a request in React
  → POST /api/ai/agent  (JWT attached)
    → .NET validates JWT, reads org_id + role
      → POST /agent/run on the AI service {org_id, role_level, query}
        → router picks an agent
          → agent retrieves role-scoped context (ChromaDB) and/or calls Ollama
        ← {route, answer, used_context, sources, structured}
      ← relayed unchanged
    ← rendered: agent badge, grounded answer, sources, structured output
```

## 8. Evaluation

`ai-service/eval/run_eval.py` ingests a labelled corpus and measures answer accuracy and
latency against the project's success criteria.

Latest run (local, qwen2.5:3b + nomic-embed-text on a modest GPU):

| Metric | Target | Result |
|--------|--------|--------|
| Answer accuracy | ≥ 80% | **100%** (10/10) |
| Latency (p95) | < 3 s | **~2.0 s** (avg ~1.75 s) |

Unit tests live in `ai-service/tests/` (`pytest`): document extraction, chunking, router
heuristics, invoice parsing/totals.

## 9. Key technical decisions

- **Local LLM via Ollama** — no API cost, data stays on-premise.
- **Ollama embeddings** instead of sentence-transformers — avoids a heavy PyTorch
  dependency on modest hardware.
- **chromadb 1.x** (prebuilt Rust wheels) — avoids the C++ build toolchain that the 0.5.x
  `chroma-hnswlib` extension needs on Windows.
- **Embedded ChromaDB** — no client/server version coupling for the MVP.
- **.NET as the only client-facing service** — single security boundary; the AI service is
  never exposed to the browser.
- **Single-org MVP, multi-tenant-ready schema** — realistic for one semester, no rewrite to
  generalize later.

## 10. Out of scope (per proposal)

Production infrastructure (Kubernetes, distributed scaling), training an LLM from scratch,
external system integrations (banks, tax authorities), a mobile app, and 100% answer
accuracy. The system is an MVP/prototype.

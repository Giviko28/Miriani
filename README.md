# Miriani — Business Process Automation Platform

Miriani is a multi-tenant, ERP-style web platform that automates knowledge-based
business processes for an organization. A conversational assistant grounds its
answers in the company's own documents using **Retrieval-Augmented Generation
(RAG)**, and an **agent-based orchestration layer** (LangGraph) routes each request
to the specialist that can resolve it — drafting emails, filing support tickets,
generating reports, or querying live operational data.

> Caucasus University · School of Technology (CST) · 2026
> Author: **Givi Chelidze** · Supervisor: **Prof. Maksim Iavich**

---

## What it does

Miriani is built around a single pattern: the assistant **drafts** a structured
result in chat, the user **reviews** it, and a single click triggers the **real
side-effect** through an integration — every action audit-logged. The platform
ships five end-to-end automations:

| # | Process            | What happens                                                            | Integration            |
|---|--------------------|------------------------------------------------------------------------|------------------------|
| 1 | Leave approval     | Drafts a leave request, emails the manager, and holds a calendar slot  | SMTP + iCal `.ics`     |
| 2 | Invoice delivery   | Generates a branded invoice PDF and emails it to the recipient         | QuestPDF + SMTP        |
| 3 | IT helpdesk        | Triages a problem into a structured ticket and files it                | Jira REST API          |
| 4 | Employee onboarding| Provisions onboarding tasks/phases and sends a welcome email           | Jira + SMTP            |
| 5 | Contract review    | Scans a contract, flags risks, and posts an alert with a PDF report    | Slack/Teams webhook    |

Each integration degrades gracefully when it isn't configured, so the platform is
always demoable.

### Other capabilities

- **Knowledge base (RAG):** admins upload PDF / DOCX / XLSX / TXT / MD / CSV files;
  the system extracts, chunks, embeds, and stores them for semantic, role-scoped
  retrieval. Answers are grounded strictly in the retrieved passages.
- **Chat attachments:** a file can be attached to a single message for one-shot
  analysis — extracted on the fly, reasoned over, and never persisted or embedded.
- **Connected databases:** the assistant can answer questions over an
  organization's own SQL database by generating and running read-only queries
  against a cached schema.
- **Company branding:** each tenant customizes its display name, tagline, logo, and
  accent color, which theme the entire application.
- **Role-based access:** Employee < Manager < Admin, enforced end to end; documents
  and data are scoped to the caller's organization and role.

---

## Architecture

The browser only ever talks to the .NET gateway. The gateway authenticates the
caller, attaches their organization and role, and forwards scoped requests to the
Python service; the browser never reaches the language model or vector store
directly.

```
 React SPA  ──JWT──▶  .NET 10 Gateway  ──────▶  PostgreSQL
 (Vite)                 │  internal REST          (users, orgs, chat, branding)
                        ▼
              Python FastAPI service  ──────▶  ChromaDB (vector store / embeddings)
              (RAG + LangGraph agents)
                        │
                        ▼
              Ollama (local)  /  Groq API (cloud)   ← language-model inference
```

- **Same codebase, two environments.** Only environment variables differ. Locally:
  PostgreSQL in Docker + Ollama on the host for inference and embeddings. In the
  cloud: managed Postgres + Groq for inference + in-process CPU embeddings.
- **Request flow:** a message hits `POST /api/chat/message`; the gateway forwards it
  to the Python service, where a router classifies the intent and a LangGraph
  `StateGraph` dispatches to the matching specialist node, which returns an answer
  plus any structured result the UI can act on.

---

## Repository layout

| Path          | What it is                                                          |
|---------------|--------------------------------------------------------------------|
| `web/`        | React + Vite + TypeScript + Tailwind frontend (talks only to the gateway) |
| `api/`        | .NET 10 ASP.NET Core gateway — auth, RBAC, orchestration, integrations (Clean Architecture) |
| `ai-service/` | Python FastAPI service — RAG (LangChain) + agent orchestration (LangGraph) |
| `docs/`       | Technical documentation and reports                                |
| `data/`       | Sample company documents used to seed the demo                     |

---

## Tech stack

- **Frontend:** React, Vite, TypeScript, Tailwind CSS
- **Gateway:** .NET 10, ASP.NET Core, Entity Framework Core, JWT auth, bcrypt
- **Service:** Python 3.10+, FastAPI, LangChain, LangGraph
- **Data:** PostgreSQL (relational), ChromaDB (vector store)
- **Inference:** Ollama (local) / Groq (cloud)
- **Integrations:** Jira REST, SMTP (MailKit), QuestPDF, iCal, Slack/Teams webhooks

---

## Getting started (local development)

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js 18+](https://nodejs.org/) and npm
- [Python 3.10+](https://www.python.org/)
- [Docker](https://www.docker.com/) (for PostgreSQL and Mailpit)
- [Ollama](https://ollama.com) with a model pulled locally:
  ```bash
  ollama pull qwen2.5:3b
  ollama pull nomic-embed-text
  ```

### 1. Start infrastructure (database + mail sink)

```bash
docker compose up -d postgres mailpit
```

### 2. Start the Python service

```bash
cd ai-service
python -m venv .venv
.venv\Scripts\activate          # Windows  (use: source .venv/bin/activate on macOS/Linux)
pip install -r requirements.txt
uvicorn app.main:app --port 8001
```

### 3. Start the .NET gateway

```bash
cd api/Api
dotnet run
```

The gateway applies database migrations and seeds a demo organization, a root
admin, and sample FAQs on first boot.

### 4. Start the frontend

```bash
cd web
npm install
npm run dev
```

The app is now available at <http://localhost:5173>.

| Service            | URL / Port             |
|--------------------|------------------------|
| Frontend (Vite)    | http://localhost:5173  |
| Gateway (.NET)     | http://localhost:5080  |
| Python service     | http://localhost:8001  |
| Mail inbox (Mailpit)| http://localhost:8025 |

**Default local admin:** `admin@bpa.local` / `ChangeMe!123` (you're prompted to
change it on first login).

---

## Configuration

Non-secret defaults live in `api/Api/appsettings.json` (connection string, JWT,
SMTP, integration project keys). Live integration secrets (Jira API token,
notification webhook URL) are kept out of source control — locally via .NET
user-secrets, in the cloud via dashboard environment variables. The Python
service reads its provider switches (`LLM_PROVIDER`, `EMBEDDING_PROVIDER`) from the
environment, defaulting to Ollama for local development.

---

## API overview

All client requests go through the gateway's REST API. Key endpoints:

| Method & path                   | Description                                                        |
|---------------------------------|-------------------------------------------------------------------|
| `POST /api/auth/login`          | Authenticate; returns an access `token` and a refresh token        |
| `POST /api/chat/message`        | Send a chat message; routed to the matching specialist            |
| `POST /api/chat/extract`        | Attach a temporary file to a message (analyzed, never stored)     |
| `POST /api/documents/ingest`    | Upload a document into the organization's knowledge base          |
| `POST /api/processes/ticket/create` | File a structured support ticket in Jira                      |
| `GET  /api/org/branding`        | Return the tenant's branding (name, logo, accent color)           |

Interactive API documentation (Swagger) is served by the gateway in development.

---

## Deployment

The platform is deployable to the cloud as infrastructure-as-code (a Render
blueprint, `render.yaml`): a static frontend, two Docker web services (gateway and
Python service), managed Postgres, and Groq for inference with in-process CPU
embeddings — no local GPU required. Pushing the connected branch triggers a
redeploy. See [`docs/DEPLOY.md`](./docs/DEPLOY.md) for the full guide.

---

## Project status

Feature-complete. The build follows the 14-week milestone plan from the original
project proposal: all five business-process automations, the knowledge base,
connected-database querying, the support-ticket workspace, per-tenant branding,
and cloud deployment are implemented.

---

## License

Developed as a bachelor's thesis project at Caucasus University. All rights
reserved by the author unless stated otherwise.

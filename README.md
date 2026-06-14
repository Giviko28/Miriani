# Business Process Automation with AI (Bachelor's Project)

ERP-like web application that automates knowledge-based business processes using a
**local LLM** grounded by **Retrieval-Augmented Generation (RAG)** and orchestrated by an
**agentic system** (LangGraph).

> Caucasus University · School of Technology (CST) · 2026
> Author: Givi Chelidze · Supervisor: Maksim Iavich

See [`CLAUDE.md`](./CLAUDE.md) for the goal, decisions, and architecture, and
[`Bachelors.docx`](./Bachelors.docx) for the full proposal.

## Architecture

```
React SPA ──JWT──▶ .NET 10 API ──▶ MS SQL Server
                       │ internal REST
                       ▼
                  Python FastAPI AI service ──▶ ChromaDB
                       │
                       ▼
                  Ollama (local Qwen2.5)
```

## Repository layout

| Path          | What it is                                                        |
|---------------|-------------------------------------------------------------------|
| `web/`        | React + Vite + TypeScript + Tailwind + shadcn frontend            |
| `api/`        | .NET 10 ASP.NET Core Web API (gateway: auth, roles, business data)|
| `ai-service/` | Python FastAPI service — RAG (LangChain) + agents (LangGraph)     |
| `docs/`       | Architecture and design documentation                             |
| `data/`       | Sample company documents used to seed the demo                    |

## Prerequisites

- .NET 10 SDK
- Node.js 20+ / npm
- Python 3.12 (use `py -3.12`)
- Docker Desktop
- [Ollama](https://ollama.com) with a model: `ollama pull qwen2.5:3b`

## Status

Early scaffolding (Milestone 1 — architecture). Build sequence follows the proposal's
14-week milestone plan; see `CLAUDE.md`.

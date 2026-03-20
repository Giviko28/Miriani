# Setup & Usage Guide

How to run the Business Process Automation system locally.

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 10.x | backend |
| Node.js | 20+ | frontend |
| Python | 3.12 | use `py -3.12` (the bare `python` may be the Windows Store stub) |
| Docker Desktop | latest | runs MS SQL |
| Ollama | latest | local LLM, runs on the host (for GPU) |

Pull the models once:

```bash
ollama pull qwen2.5:3b
ollama pull nomic-embed-text
```

## First-time setup

```bash
# 1. Database (MS SQL in Docker)
docker compose up -d mssql

# 2. AI service (Python)
cd ai-service
py -3.12 -m venv .venv
.venv\Scripts\python.exe -m pip install -r requirements.txt

# 3. Frontend
cd ../web
npm install
```

The .NET API restores and migrates automatically on first run.

## Running (4 processes)

Open four terminals (and make sure Ollama is running — `ollama serve` or the desktop app):

```bash
# 1. Database
docker compose up -d mssql

# 2. AI service  -> http://localhost:8001
cd ai-service && .venv\Scripts\python.exe -m uvicorn app.main:app --port 8001

# 3. API (auto-migrates + seeds)  -> http://localhost:5080  (Swagger at /swagger)
cd api && dotnet run --project Api

# 4. Frontend  -> http://localhost:5173
cd web && npm run dev
```

Open http://localhost:5173, register a user (pick a role), and you're in.

## Ports

| Service | URL |
|---------|-----|
| Frontend | http://localhost:5173 |
| API (gateway) | http://localhost:5080 (`/swagger`) |
| AI service | http://localhost:8001 (`/docs`) |
| MS SQL | localhost:1433 |
| Ollama | http://localhost:11434 |

## Using the app

1. **Register / sign in.** Choose a role (Employee / Manager / Admin). Role drives what
   documents you can see.
2. **Documents tab.** Upload PDF/Word/Excel/text. Set the minimum role that may see it.
   Status moves Uploaded → Processing → **Indexed** once embedded.
3. **AI Assistant tab.** Ask a question or request a task. The router picks an agent:
   - *"How many remote days are allowed?"* → Policy Q&A (grounded, with sources)
   - *"Summarize the remote work policy"* → Summarizer
   - *"Write an email announcing…"* → Email Drafter
   - *"Create an invoice for ACME: 10 hours at 150 GEL"* → Invoice Generator (structured)

   Answers only use documents your role can access — a Manager-only document is invisible
   to an Employee.

## Tests & evaluation

```bash
cd ai-service
.venv\Scripts\python.exe -m pytest tests -q          # unit tests
.venv\Scripts\python.exe -m eval.run_eval            # RAG accuracy + latency (needs Ollama)
```

## Troubleshooting

- **AI answers are slow on the first call.** The model loads into VRAM on first use
  (~30 s cold); subsequent calls are ~1–2 s.
- **`python` prints nothing.** Use `py` / `py -3.12` — the bare command is the Store stub.
- **API can't reach the database.** Ensure `docker compose up -d mssql` is healthy and the
  connection string in `api/Api/appsettings.json` matches the container password.
- **Document stuck on "Failed".** The AI service wasn't running during upload, or the file
  type is unsupported. Re-upload with the AI service up.

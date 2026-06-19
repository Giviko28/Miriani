# Deploying to the cloud (free tier)

The whole stack deploys to **Render** from `render.yaml` (Blueprint), with **Neon** for
PostgreSQL and **Groq** for the chat LLM. Embeddings run on a local CPU model inside the AI
service container. Everything is on free tiers.

> Local AI is unchanged. The code defaults to Ollama for both chat and embeddings; the cloud
> swaps are env-var only (`LLM_PROVIDER=groq`, `EMBEDDING_PROVIDER=local`). The database moves
> to PostgreSQL on this branch — point your local dev at any Postgres (or Neon) via
> `ConnectionStrings__Default`.

## Architecture in the cloud

```
Browser ── https ──> miriani-web (static, Vite build)
                         │  VITE_API_BASE
                         ▼
                     miriani-api (.NET gateway, Docker)
                     │            │
            Neon Postgres     miriani-ai (FastAPI, Docker)
                                  │
                              Groq API (chat) + in-process ONNX embeddings
```

## One-time accounts (all free, no card)

1. **Neon** — https://neon.tech → create a project → copy the connection string
   (`postgresql://user:pass@host/db?sslmode=require`). The gateway accepts this URL as-is.
2. **Groq** — https://console.groq.com → API Keys → create a key (`gsk_...`).
3. **Render** — https://dashboard.render.com → sign in with GitHub.

## Deploy

1. Render dashboard → **New → Blueprint** → pick this repo and the deploy branch.
   Render reads `render.yaml` and proposes three services: `miriani-ai`, `miriani-api`,
   `miriani-web`.
2. Fill the prompted secrets:
   - `miriani-ai` → `GROQ_API_KEY` = your Groq key.
   - `miriani-api` → `ConnectionStrings__Default` = your Neon connection string;
     `Seed__RootAdminPassword` = the first admin password you want.
   - (`Jwt__Secret` is auto-generated.)
3. **Apply**. Render builds and deploys all three. First build is slow (Docker + model
   download); subsequent pushes auto-deploy (this is the CD pipeline).

### If a service name was taken
Render appends a suffix to the public URL. If so, update these three env values to the real
URLs (Settings → Environment), then redeploy:
- `miriani-api` → `Cors__AllowedOrigins__0` = the real `miriani-web` URL
- `miriani-api` → `AiService__BaseUrl` = the real `miriani-ai` URL
- `miriani-web` → `VITE_API_BASE` = the real `miriani-api` URL (static rebuild needed)

## Log in

Open the `miriani-web` URL. Sign in with the root admin: `admin@<seeded>` / the
`Seed__RootAdminPassword` you set. (See `DbInitializer` for the seeded admin email.)

## Free-tier caveats

- **Cold starts:** free web services sleep after ~15 min idle; the first request wakes them
  (10–30 s). The gateway wakes the AI service on its first call.
- **Ephemeral storage:** free services have no persistent disk, so the ChromaDB vector store
  and uploaded files are wiped on restart/redeploy. Users, auth, chat history, and branding
  live in Neon and **do** persist. Re-ingest knowledge-base docs after a restart, or attach a
  paid disk / use Chroma Cloud for durable vectors.
- **Integrations** (Jira, SMTP, Slack/Teams) stay unconfigured by default and degrade
  gracefully; set their env vars on `miriani-api` to enable them.

## CI

`.github/workflows/ci.yml` builds + tests the gateway, builds the frontend, and builds the AI
image on every push/PR.

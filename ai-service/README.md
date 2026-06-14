# ai-service — AI brain (Python FastAPI)

Stateless service that does RAG (LangChain + ChromaDB) and agent orchestration
(LangGraph) against a local LLM (Ollama). Takes a question + role/org filters, returns a
**source-grounded** answer. Called only by the .NET API.

Built out across **Milestones 3–5 (weeks 5–10)**. A stub endpoint exists earlier for the
integration smoke test.

Planned layout:

```
ai-service/
├─ app/
│  ├─ main.py        # FastAPI app
│  ├─ ingestion/     # PDF/Word/Excel -> chunk -> embed -> ChromaDB
│  ├─ rag/           # retrieval (role/org filtered) + prompt building
│  ├─ agents/        # LangGraph router + specialized agents
│  └─ llm/           # Ollama client, caching
└─ tests/
```

Setup (use Python 3.12):

```bash
py -3.12 -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload
```

Environment: `OLLAMA_BASE_URL` (default `http://localhost:11434`), `CHROMA_URL`.

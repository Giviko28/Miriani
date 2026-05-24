"""AI service.

Endpoints:
  GET  /health        liveness
  POST /ping-llm      smoke test: prompt -> local LLM
  POST /ingest        store a document in the vector store (org + role scoped)
  POST /rag/retrieve  role-scoped retrieval only (no generation)
  POST /rag/query     role-scoped retrieval + grounded LLM answer
  POST /agent/run     route the request to a specialized agent (LangGraph)
"""

import time

import httpx
from fastapi import FastAPI, Form, HTTPException, UploadFile
from pydantic import BaseModel

from app.agents.graph import run_agents
from app.config import settings
from app.db import connector as db_connector
from app.db import schema_cache
from app.ingestion.extract import UnsupportedFileType
from app.rag import service as rag
from app.rag.store import vector_store

app = FastAPI(title="BPA AI Service", version="0.1.0")


# ---------- health + smoke ----------

class PingRequest(BaseModel):
    prompt: str = "Say hello in one short sentence."


class PingResponse(BaseModel):
    model: str
    reply: str
    elapsed_seconds: float


@app.get("/health")
def health() -> dict[str, object]:
    return {"status": "ok", "chunks": vector_store.count()}


@app.post("/ping-llm", response_model=PingResponse)
async def ping_llm(req: PingRequest) -> PingResponse:
    started = time.perf_counter()
    payload = {"model": settings.ollama_model, "prompt": req.prompt, "stream": False}
    try:
        async with httpx.AsyncClient(timeout=settings.request_timeout_seconds) as client:
            resp = await client.post(f"{settings.ollama_base_url}/api/generate", json=payload)
            resp.raise_for_status()
            data = resp.json()
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"Ollama request failed: {exc}") from exc

    return PingResponse(
        model=settings.ollama_model,
        reply=data.get("response", "").strip(),
        elapsed_seconds=round(time.perf_counter() - started, 2),
    )


# ---------- RAG ----------

class IngestResponse(BaseModel):
    doc_id: str
    chunks: int


@app.post("/ingest", response_model=IngestResponse)
async def ingest(
    file: UploadFile,
    org_id: str = Form(...),
    doc_id: str = Form(...),
    access_role: int = Form(0),
) -> IngestResponse:
    data = await file.read()
    try:
        result = rag.ingest_document(
            org_id=org_id,
            doc_id=doc_id,
            file_name=file.filename or "upload",
            access_role=access_role,
            data=data,
        )
    except UnsupportedFileType as exc:
        raise HTTPException(status_code=415, detail=str(exc)) from exc

    return IngestResponse(doc_id=result.doc_id, chunks=result.chunks)


@app.delete("/ingest/{doc_id}", status_code=204)
async def delete_document(doc_id: str) -> None:
    from app.rag.store import vector_store
    vector_store.delete_document(doc_id)


class ReconcileRequest(BaseModel):
    org_id: str
    valid_doc_ids: list[str]


class ReconcileResponse(BaseModel):
    removed: list[str]


@app.get("/ingest/docs/{org_id}")
async def list_org_docs(org_id: str) -> dict:
    """List the distinct doc_ids currently held in the vector store for an org."""
    return {"org_id": org_id, "doc_ids": sorted(vector_store.list_doc_ids(org_id))}


@app.post("/ingest/reconcile")
async def reconcile_org_docs(req: ReconcileRequest) -> ReconcileResponse:
    """Purge orphaned vectors: delete any of the org's chunks whose doc_id is not in
    the authoritative list supplied by the system of record (the .NET Documents table)."""
    removed = vector_store.reconcile(req.org_id, set(req.valid_doc_ids))
    return ReconcileResponse(removed=removed)


class QueryRequest(BaseModel):
    org_id: str
    role_level: int = 0
    query: str
    top_k: int | None = None


class SourceDto(BaseModel):
    doc_id: str
    file_name: str
    chunk_index: int
    distance: float
    text: str


class RetrieveResponse(BaseModel):
    sources: list[SourceDto]


class QueryResponse(BaseModel):
    answer: str
    used_context: bool
    sources: list[SourceDto]


def _to_dto(chunks) -> list[SourceDto]:
    return [
        SourceDto(
            doc_id=c.doc_id,
            file_name=c.file_name,
            chunk_index=c.chunk_index,
            distance=round(c.distance, 4),
            text=c.text,
        )
        for c in chunks
    ]


@app.post("/rag/retrieve", response_model=RetrieveResponse)
def rag_retrieve(req: QueryRequest) -> RetrieveResponse:
    chunks = rag.retrieve(
        org_id=req.org_id, role_level=req.role_level, query=req.query, top_k=req.top_k
    )
    return RetrieveResponse(sources=_to_dto(chunks))


@app.post("/rag/query", response_model=QueryResponse)
async def rag_query(req: QueryRequest) -> QueryResponse:
    result = await rag.answer(
        org_id=req.org_id, role_level=req.role_level, query=req.query, top_k=req.top_k
    )
    return QueryResponse(
        answer=result.answer,
        used_context=result.used_context,
        sources=_to_dto(result.sources),
    )


# ---------- External DB ----------

class DbConnectRequest(BaseModel):
    org_id: str
    connection_string: str


class DbSchemaResponse(BaseModel):
    org_id: str
    tables: list[dict]


@app.post("/db/connect", response_model=DbSchemaResponse)
def db_connect(req: DbConnectRequest) -> DbSchemaResponse:
    """Introspect the target DB schema and cache it for the org."""
    try:
        schema = db_connector.introspect(req.connection_string)
    except Exception as exc:
        raise HTTPException(status_code=400, detail=f"Connection failed: {exc}") from exc
    schema_cache.save(req.org_id, req.connection_string, schema)
    return DbSchemaResponse(org_id=req.org_id, tables=schema["tables"])


@app.get("/db/schema/{org_id}", response_model=DbSchemaResponse)
def db_get_schema(org_id: str) -> DbSchemaResponse:
    """Return the cached schema for an org, or 404 if not connected."""
    schema = schema_cache.get_schema(org_id)
    if schema is None:
        raise HTTPException(status_code=404, detail="No database connected for this org.")
    return DbSchemaResponse(org_id=org_id, tables=schema["tables"])


@app.delete("/db/disconnect/{org_id}", status_code=204)
def db_disconnect(org_id: str) -> None:
    """Remove the cached DB config for an org."""
    schema_cache.delete(org_id)


class DbExploreResponse(BaseModel):
    org_id: str
    summary: str
    tables_explored: int


@app.post("/db/explore/{org_id}", response_model=DbExploreResponse)
async def db_explore(org_id: str) -> DbExploreResponse:
    """Sample every table, generate a natural-language description, and cache it."""
    import json as _json
    from app.llm.client import generate

    cached = schema_cache.load(org_id)
    if cached is None:
        raise HTTPException(status_code=404, detail="No database connected for this org.")

    conn_str = cached["connection_string"]
    tables = cached["schema"].get("tables", [])

    samples: list[str] = []
    for table in tables:
        try:
            rows = db_connector.execute_select(conn_str, f"SELECT * FROM \"{table['name']}\" LIMIT 5")
            rows_text = _json.dumps(rows, default=str)
        except Exception:
            rows_text = "(could not read)"
        col_list = ", ".join(f"{c['name']} ({c['type']})" for c in table["columns"])
        samples.append(f"Table: {table['name']}\nColumns: {col_list}\nSample rows: {rows_text}")

    prompt = (
        "You have been given a database with the following tables and sample data:\n\n"
        + "\n\n".join(samples)
        + "\n\nWrite a clear, concise description of this database — what it stores, what each table "
        "represents, the key columns, and any notable data patterns visible in the samples. "
        "This description will be used as permanent context for an AI assistant. "
        "Write one cohesive paragraph per table."
    )
    summary = await generate(prompt, system="You write precise, factual database descriptions.")
    schema_cache.save_summary(org_id, summary)

    return DbExploreResponse(org_id=org_id, summary=summary, tables_explored=len(tables))


# ---------- Agents ----------

class AgentTurn(BaseModel):
    sender: str
    content: str


class AgentRequest(BaseModel):
    org_id: str
    role_level: int = 0
    query: str
    history: list[AgentTurn] | None = None


class AgentResponse(BaseModel):
    route: str
    answer: str
    used_context: bool
    sources: list[SourceDto]
    structured: dict | None = None


@app.post("/agent/run", response_model=AgentResponse)
async def agent_run(req: AgentRequest) -> AgentResponse:
    """Route the request to the right specialized agent and return its result."""
    history = [t.model_dump() for t in req.history] if req.history else None
    state = await run_agents(
        org_id=req.org_id, role_level=req.role_level, query=req.query, history=history
    )
    sources = [SourceDto(**s) for s in state.get("sources", [])]
    return AgentResponse(
        route=state.get("route", ""),
        answer=state.get("answer", ""),
        used_context=state.get("used_context", False),
        sources=sources,
        structured=state.get("structured"),
    )

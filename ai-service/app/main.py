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


# ---------- Agents ----------

class AgentRequest(BaseModel):
    org_id: str
    role_level: int = 0
    query: str


class AgentResponse(BaseModel):
    route: str
    answer: str
    used_context: bool
    sources: list[SourceDto]
    structured: dict | None = None


@app.post("/agent/run", response_model=AgentResponse)
async def agent_run(req: AgentRequest) -> AgentResponse:
    """Route the request to the right specialized agent and return its result."""
    state = await run_agents(
        org_id=req.org_id, role_level=req.role_level, query=req.query
    )
    sources = [SourceDto(**s) for s in state.get("sources", [])]
    return AgentResponse(
        route=state.get("route", ""),
        answer=state.get("answer", ""),
        used_context=state.get("used_context", False),
        sources=sources,
        structured=state.get("structured"),
    )

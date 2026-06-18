"""RAG orchestration: ingest documents into the vector store, and answer questions
grounded in role-scoped retrieved context."""

from dataclasses import dataclass

from app.ingestion.chunker import chunk_text
from app.ingestion.extract import extract_text
from app.llm.client import generate
from app.rag.store import RetrievedChunk, vector_store

_SYSTEM_PROMPT = (
    "You are a business assistant. Answer ONLY using the provided context. "
    "If the context does not contain the answer, say you don't have that information. "
    "Be concise and cite facts from the context. Do not invent details."
)


@dataclass
class IngestResult:
    doc_id: str
    chunks: int


@dataclass
class RagAnswer:
    answer: str
    sources: list[RetrievedChunk]
    used_context: bool


def ingest_document(*, org_id: str, doc_id: str, file_name: str, access_role: int, data: bytes) -> IngestResult:
    """Extract text from a document, chunk it, and store it in the vector store."""
    text = extract_text(file_name, data)
    chunks = chunk_text(text)
    stored = vector_store.add_chunks(
        org_id=org_id,
        doc_id=doc_id,
        file_name=file_name,
        access_role=access_role,
        chunks=chunks,
    )
    return IngestResult(doc_id=doc_id, chunks=stored)


def retrieve(*, org_id: str, role_level: int, query: str, top_k: int | None = None) -> list[RetrievedChunk]:
    """Return the role-scoped chunks most relevant to the query (no generation)."""
    return vector_store.query(org_id=org_id, role_level=role_level, text=query, top_k=top_k)


def _history_block(history: list[dict] | None) -> str:
    if not history:
        return ""
    lines = [f"{m['sender'].capitalize()}: {m['content']}" for m in history]
    return "Conversation so far:\n" + "\n".join(lines) + "\n\n"


async def answer(
    *, org_id: str, role_level: int, query: str, top_k: int | None = None,
    history: list[dict] | None = None,
) -> RagAnswer:
    """Retrieve role-scoped context and generate a grounded answer.

    Retrieval grounds on the current query only (so citations stay accurate); any prior
    conversation turns are included in the prompt so follow-ups resolve in context.
    """
    sources = retrieve(org_id=org_id, role_level=role_level, query=query, top_k=top_k)

    if not sources:
        return RagAnswer(
            answer="I don't have any information on that in the available documents.",
            sources=[],
            used_context=False,
        )

    context = "\n\n".join(
        f"[Source {i + 1}: {c.file_name}]\n{c.text}" for i, c in enumerate(sources)
    )
    prompt = f"{_history_block(history)}Context:\n{context}\n\nQuestion: {query}\n\nAnswer:"
    reply = await generate(prompt, system=_SYSTEM_PROMPT, temperature=0.2)
    return RagAnswer(answer=reply, sources=sources, used_context=True)

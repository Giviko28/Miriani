"""Specialized agents — one per automated business process.

Each is a LangGraph node: it reads the request (and the caller's role-scoped context
where useful) and produces an answer. Generative agents (email/report) draft text;
invoice_gen produces structured JSON.
"""

import json

from app.agents.state import AgentState
from app.llm.client import generate
from app.rag import service as rag


def _sources_to_dicts(sources) -> list[dict]:
    return [
        {
            "doc_id": s.doc_id,
            "file_name": s.file_name,
            "chunk_index": s.chunk_index,
            "distance": round(s.distance, 4),
            "text": s.text,
        }
        for s in sources
    ]


def _retrieve(state: AgentState):
    return rag.retrieve(
        org_id=state["org_id"], role_level=state.get("role_level", 0), query=state["query"]
    )


def _history_block(state: AgentState) -> str:
    """Render recent conversation turns so the model can interpret follow-ups."""
    history = state.get("history") or []
    if not history:
        return ""
    lines = [f"{m['sender'].capitalize()}: {m['content']}" for m in history]
    return "Conversation so far:\n" + "\n".join(lines) + "\n\n"


async def policy_qa(state: AgentState) -> AgentState:
    """Answer a question grounded in role-scoped company documents."""
    result = await rag.answer(
        org_id=state["org_id"], role_level=state.get("role_level", 0), query=state["query"],
        history=state.get("history"),
    )
    return {
        "answer": result.answer,
        "used_context": result.used_context,
        "sources": _sources_to_dicts(result.sources),
        "structured": None,
    }


async def doc_summary(state: AgentState) -> AgentState:
    """Summarize the most relevant company content for the request."""
    sources = _retrieve(state)
    if not sources:
        return {"answer": "I couldn't find any matching documents to summarize.",
                "used_context": False, "sources": [], "structured": None}

    context = "\n\n".join(s.text for s in sources)
    prompt = (
        f"{_history_block(state)}"
        f"Summarize the following content into 3-5 concise bullet points.\n\n{context}"
    )
    reply = await generate(prompt, system="You are a precise business summarizer. Use only the given content.")
    return {"answer": reply, "used_context": True, "sources": _sources_to_dicts(sources), "structured": None}


async def email_draft(state: AgentState) -> AgentState:
    """Draft a business email for the request, grounded in any relevant context."""
    sources = _retrieve(state)
    context = "\n\n".join(s.text for s in sources)
    context_block = f"Relevant company context:\n{context}\n\n" if context else ""
    prompt = (
        f"{_history_block(state)}{context_block}"
        f"Write a professional business email for this request:\n{state['query']}\n\n"
        "Include a subject line. Keep it concise and courteous."
    )
    reply = await generate(prompt, system="You draft clear, professional business emails.")
    return {"answer": reply, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": None}


async def report_draft(state: AgentState) -> AgentState:
    """Draft a short business report, grounded in relevant company context."""
    sources = _retrieve(state)
    context = "\n\n".join(s.text for s in sources)
    context_block = f"Use this company context where relevant:\n{context}\n\n" if context else ""
    prompt = (
        f"{_history_block(state)}{context_block}"
        f"Write a structured business report for this request:\n{state['query']}\n\n"
        "Use clear headings (Summary, Details, Recommendations)."
    )
    reply = await generate(prompt, system="You write structured, factual business reports.")
    return {"answer": reply, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": None}


async def invoice_gen(state: AgentState) -> AgentState:
    """Produce a structured invoice (JSON) from the details in the request."""
    prompt = (
        "Extract invoice details from the request and return ONLY a JSON object with keys: "
        "client (string), items (array of {description, quantity, unit_price}), "
        "currency (string), notes (string). Compute nothing; just extract. "
        f"If a field is missing, use null or an empty array.\n\nRequest:\n{state['query']}"
    )
    raw = await generate(prompt, system="You output only valid JSON, no prose, no code fences.")

    structured = _parse_json(raw)
    if structured is None:
        return {"answer": raw, "used_context": False, "sources": [], "structured": None}

    # Compute totals server-side (don't trust the LLM with arithmetic). Be tolerant of
    # the LLM's key naming (quantity/qty/_quantity, unit_price/price/rate/_unit_price).
    total = 0.0
    for item in structured.get("items") or []:
        if not isinstance(item, dict):
            continue
        qty = _find_number(item, ("quantity", "qty", "units", "hours", "days"))
        price = _find_number(item, ("unit_price", "price", "rate", "amount", "cost"))
        total += qty * price
    structured["total"] = round(total, 2)

    summary = f"Invoice for {structured.get('client') or 'client'} — total {structured['total']} {structured.get('currency') or ''}".strip()
    return {"answer": summary, "used_context": False, "sources": [], "structured": structured}


def _find_number(item: dict, names: tuple[str, ...]) -> float:
    """Find a numeric value in an item dict by trying several key names (and a
    case-insensitive, underscore-stripped match) so totals survive LLM key drift."""
    for key, value in item.items():
        normalized = key.lower().strip("_")
        if normalized in names:
            try:
                return float(value)
            except (TypeError, ValueError):
                return 0.0
    return 0.0


def _parse_json(raw: str) -> dict | None:
    raw = raw.strip()
    if raw.startswith("```"):
        raw = raw.strip("`")
        raw = raw[raw.find("{"):]
    start, end = raw.find("{"), raw.rfind("}")
    if start == -1 or end == -1:
        return None
    try:
        return json.loads(raw[start : end + 1])
    except json.JSONDecodeError:
        return None


# Map agent keys to their node functions (consumed by the graph builder).
SPECIALISTS = {
    "policy_qa": policy_qa,
    "doc_summary": doc_summary,
    "email_draft": email_draft,
    "report_draft": report_draft,
    "invoice_gen": invoice_gen,
}

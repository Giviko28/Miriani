"""Specialized agents — one per automated business process.

Each is a LangGraph node: it reads the request (and the caller's role-scoped context
where useful) and produces an answer. Generative agents (email/report) draft text;
invoice_gen produces structured JSON.
"""

import json

from app.agents.state import AgentState
from app.llm.client import generate
from app.rag import service as rag

# Miriani's character — injected into every specialist system prompt.
_PERSONA = (
    "You are Miriani, a warm and professional AI business assistant. "
    "You go by Mirian for short. "
    "You are knowledgeable, reliable, and genuinely enjoy helping people. "
    "You keep a friendly but professional tone at all times. "
    "If someone asks who you are, introduce yourself as Miriani. "
)


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


async def greeting(state: AgentState) -> AgentState:
    """Respond to greetings, introductions, and casual conversation as Miriani."""
    prompt = (
        f"{_history_block(state)}"
        f"The user said: {state['query']}\n\n"
        "Always start by introducing yourself as Miriani (or Mirian for short). "
        "Greet them back warmly, acknowledge their name if they gave one, and briefly mention "
        "what you can help with (company policies, document summaries, emails, reports, invoices). "
        "Keep it short, friendly, and natural — 2 to 4 sentences, no bullet lists."
    )
    reply = await generate(
        prompt,
        system=(
            _PERSONA +
            "You are having a casual, friendly conversation. "
            "Always introduce yourself by name (Miriani) early in your reply."
        ),
    )
    return {"answer": reply, "used_context": False, "sources": [], "structured": None}


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
    reply = await generate(prompt, system=_PERSONA + "You are a precise business summarizer. Use only the given content.")
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
    reply = await generate(prompt, system=_PERSONA + "You draft clear, professional business emails.")
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
    reply = await generate(prompt, system=_PERSONA + "You write structured, factual business reports.")
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


async def leave_request(state: AgentState) -> AgentState:
    """Process a leave request: check policy, assess eligibility, generate formal letter."""
    sources = _retrieve(state)
    context = "\n\n".join(s.text for s in sources)
    context_block = f"Company leave policy:\n{context}\n\n" if context else ""
    prompt = (
        f"{_history_block(state)}{context_block}"
        f"Employee leave request: {state['query']}\n\n"
        "Extract the request details and assess eligibility against any policy provided. "
        "Return ONLY a JSON object with keys: "
        "employee_name (string or null), start_date (string), end_date (string), "
        "days_requested (number), status (one of: approved, pending, flagged), "
        "policy_note (string — one sentence explaining the policy check result), "
        "formal_letter (string — a short formal leave request letter the employee can send). "
        "If no policy is available, set status to pending and note that HR review is needed."
    )
    raw = await generate(prompt, system="You output only valid JSON, no prose, no code fences.")
    structured = _parse_json(raw)
    if structured is None:
        return {"answer": raw, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": None}
    summary = (
        f"Leave request for {structured.get('days_requested', '?')} day(s) "
        f"({structured.get('start_date', '')} – {structured.get('end_date', '')}): "
        f"{structured.get('status', 'pending').upper()}. {structured.get('policy_note', '')}"
    )
    return {"answer": summary, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": structured}


async def onboarding_gen(state: AgentState) -> AgentState:
    """Generate a role-specific onboarding checklist grounded in company policy docs."""
    sources = _retrieve(state)
    context = "\n\n".join(s.text for s in sources)
    context_block = f"Company policies and procedures:\n{context}\n\n" if context else ""
    prompt = (
        f"{_history_block(state)}{context_block}"
        f"Onboarding request: {state['query']}\n\n"
        "Generate a structured onboarding plan. "
        "Return ONLY a JSON object with keys: "
        "role (string), employee_name (string or null), start_date (string or null), "
        "day_1 (array of strings — tasks for the first day), "
        "week_1 (array of strings — tasks for the first week), "
        "month_1 (array of strings — tasks for the first month). "
        "Base tasks on the company policies where available. Aim for 4-6 items per phase."
    )
    raw = await generate(prompt, system="You output only valid JSON, no prose, no code fences.")
    structured = _parse_json(raw)
    if structured is None:
        return {"answer": raw, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": None}
    role = structured.get("role") or "new employee"
    total = (
        len(structured.get("day_1") or []) +
        len(structured.get("week_1") or []) +
        len(structured.get("month_1") or [])
    )
    summary = f"Onboarding plan for {role} — {total} tasks across Day 1, Week 1, and Month 1."
    return {"answer": summary, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": structured}


async def contract_scan(state: AgentState) -> AgentState:
    """Scan a vendor contract or agreement for risks relative to company policy."""
    sources = _retrieve(state)
    context = "\n\n".join(s.text for s in sources)
    context_block = f"Available company documents (policies and/or contract content):\n{context}\n\n" if context else ""
    prompt = (
        f"{_history_block(state)}{context_block}"
        f"Contract scan request: {state['query']}\n\n"
        "Analyze the documents for contractual risks relative to company policy. "
        "Return ONLY a JSON object with keys: "
        "overall_risk (one of: Low, Medium, High), "
        "clauses (array of objects, each with: clause (string), risk (Low/Medium/High), finding (string)), "
        "recommendations (array of strings — concrete next steps). "
        "If no contract content is found, set overall_risk to Medium and note that a document should be uploaded."
    )
    raw = await generate(prompt, system="You output only valid JSON, no prose, no code fences.")
    structured = _parse_json(raw)
    if structured is None:
        return {"answer": raw, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": None}
    overall = structured.get("overall_risk", "Unknown")
    n_clauses = len(structured.get("clauses") or [])
    high = sum(1 for c in (structured.get("clauses") or []) if c.get("risk") == "High")
    summary = f"Contract scan complete — overall risk: {overall}. {n_clauses} clause(s) reviewed, {high} high-risk finding(s)."
    return {"answer": summary, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": structured}


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
    "greeting": greeting,
    "policy_qa": policy_qa,
    "doc_summary": doc_summary,
    "email_draft": email_draft,
    "report_draft": report_draft,
    "invoice_gen": invoice_gen,
    "leave_request": leave_request,
    "onboarding_gen": onboarding_gen,
    "contract_scan": contract_scan,
}

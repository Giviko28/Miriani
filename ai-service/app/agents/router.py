"""Router agent: classify an incoming request into one specialized agent.

This is the "general agent" from the proposal — it reads the user's request and decides
which specialized agent should handle it (LangGraph conditional routing).
"""

from app.agents.state import AGENT_KEYS, AgentState
from app.llm.client import generate

_ROUTER_SYSTEM = (
    "You are a request router for a business automation system. "
    "Read the user's request and choose the single best handler. "
    "Reply with ONLY one of these exact keys and nothing else:\n"
    "- policy_qa: questions answered from company documents/policies\n"
    "- doc_summary: requests to summarize a document or content\n"
    "- email_draft: requests to write/draft an email\n"
    "- report_draft: requests to write/draft a report\n"
    "- invoice_gen: requests to create/generate an invoice"
)

_KEYWORD_HINTS = {
    "invoice_gen": ("invoice", "bill", "receipt"),
    "email_draft": ("email", "e-mail", "write to", "reply to"),
    "report_draft": ("report",),
    "doc_summary": ("summar", "tl;dr", "tldr", "key points"),
}


def _heuristic(query: str) -> str | None:
    q = query.lower()
    for key, words in _KEYWORD_HINTS.items():
        if any(w in q for w in words):
            return key
    return None


async def route(state: AgentState) -> AgentState:
    """Decide which specialized agent handles the request."""
    query = state["query"]

    # Cheap keyword pass first; fall back to the LLM for anything ambiguous.
    choice = _heuristic(query)
    if choice is None:
        raw = (await generate(query, system=_ROUTER_SYSTEM)).strip().lower()
        choice = next((k for k in AGENT_KEYS if k in raw), "policy_qa")

    return {"route": choice}

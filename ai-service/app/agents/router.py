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
    "- greeting: greetings, introductions, small talk, asking who you are, or casual conversation\n"
    "- policy_qa: questions answered from company documents/policies\n"
    "- doc_summary: requests to summarize a document or content\n"
    "- email_draft: requests to write/draft an email\n"
    "- report_draft: requests to write/draft a report\n"
    "- invoice_gen: requests to create/generate an invoice"
)

_KEYWORD_HINTS = {
    "greeting": ("hi", "hello", "hey", "who are you", "what are you", "introduce", "your name",
                 "good morning", "good afternoon", "good evening", "howdy", "sup", "greetings"),
    "invoice_gen": ("invoice", "bill", "receipt"),
    "email_draft": ("email", "e-mail", "write to", "reply to"),
    "report_draft": ("report",),
    "doc_summary": ("summar", "tl;dr", "tldr", "key points"),
    "leave_request": ("leave", "time off", "day off", "days off", "vacation", "annual leave",
                      "pto", "holiday request", "request leave", "take leave"),
    "onboarding_gen": ("onboard", "onboarding", "new hire", "new employee", "new staff",
                       "joining", "first day", "checklist for"),
    "contract_scan": ("contract", "vendor", "agreement", "risk", "scan contract",
                      "review contract", "check contract", "risky clause"),
    "db_query": ("who is on leave", "who is off", "check availability", "is available",
                 "query the database", "from the database", "from the system",
                 "show me scheduled", "scheduled leave", "schedule conflict"),
    "db_explore": ("explore db", "explore the db", "explore database", "explore the database",
                   "describe the db", "describe the database", "describe db",
                   "learn the database", "scan the database", "what tables", "what's in the db",
                   "what is in the db", "index the db", "memorize the db", "remember the db"),
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

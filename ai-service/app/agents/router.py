"""Router agent: classify an incoming request into one specialized agent.

This is the "general agent" from the proposal — it reads the user's request and decides
which specialized agent should handle it (LangGraph conditional routing).
"""

from app.agents.state import AGENT_KEYS, AgentState
from app.llm.client import generate

_ROUTER_SYSTEM = (
    "You are a request router for a business automation system at Silknet, a telecom operator. "
    "Read the user's request and choose the single best handler. "
    "Decide by the ACTION the user wants, not by surface keywords.\n\n"
    "CRITICAL DISTINCTION — policy_qa vs ticket_triage:\n"
    "  policy_qa: the user is ASKING A QUESTION about how something works, what the process is, "
    "or what the SLA/policy says. They want information, not an action taken.\n"
    "  ticket_triage: the user is REPORTING AN ACTIVE PROBLEM or EXPLICITLY asking to file/create "
    "a ticket right now. Something is broken or not working.\n\n"
    "Reply with ONLY one of these exact keys and nothing else:\n"
    "- greeting: greetings, introductions, small talk, or asking who you are\n"
    "- policy_qa: questions about company policies, SLA terms, runbooks, or procedures\n"
    "- email_draft: requests to write/draft/compose an email or message to someone\n"
    "- ticket_triage: reporting an active IT/network problem RIGHT NOW, or explicitly asking "
    "to create/file/raise a support ticket\n"
    "- ticket_advice: asking HOW to approach or resolve an existing ticket or support issue\n"
    "- db_query: requests for live incident data — open incidents, recent P1s, resolved this week, "
    "incident counts, anything that requires querying the incidents database\n\n"
    "Examples:\n"
    "Request: show me all open P1 incidents -> db_query\n"
    "Request: what incidents were resolved last week? -> db_query\n"
    "Request: how many P2s do we have right now? -> db_query\n"
    "Request: give me the incident report for today -> db_query\n"
    "Request: what is our SLA for P1 incidents? -> policy_qa\n"
    "Request: how do I escalate a critical incident? -> policy_qa\n"
    "Request: what does our runbook say about fiber cuts? -> policy_qa\n"
    "Request: my VPN won't connect, it's urgent -> ticket_triage\n"
    "Request: the office internet is down -> ticket_triage\n"
    "Request: please create a ticket — I can't access the billing system -> ticket_triage\n"
    "Request: how should I approach ticket INC-2026-017? -> ticket_advice\n"
    "Request: draft an email to Carrefour about the fiber outage -> email_draft\n"
    "Request: who are you? -> greeting"
)

_KEYWORD_HINTS = {
    "greeting": ("hi", "hello", "hey", "who are you", "what are you", "introduce", "your name",
                 "good morning", "good afternoon", "good evening", "howdy", "sup", "greetings"),
    "email_draft": ("email", "e-mail", "write to", "reply to", "draft an email", "compose an email"),
    "ticket_triage": ("not working", "won't connect", "wont connect", "can't access", "cant access",
                      "broken", "is down", "error message", "blue screen", "won't turn on",
                      "reset my password", "need access to", "laptop won't", "vpn issue",
                      "raise a ticket", "open a ticket", "log a ticket", "file a ticket",
                      "it support", "help desk", "helpdesk"),
    "db_query": ("incident", "incidents", "open p1", "open p2", "open p3", "open p4",
                 "show me incidents", "list incidents", "incident report", "recent incidents",
                 "how many incidents", "unresolved", "in progress incidents",
                 "query the database", "from the database", "from the system"),
}


# Phrases that signal the user wants ADVICE on resolving a ticket they already have,
# rather than to file a new one. These must win over ticket_triage's keyword hints.
_ADVICE_PHRASES = (
    "how should i approach", "how do i approach", "how would you approach",
    "how should i handle", "how do i handle", "how should i deal with", "how do i deal with",
    "how should i resolve", "how do i resolve", "how to resolve", "how can i resolve",
    "how should i fix", "how do i fix this", "how to fix this",
    "what should i do", "best way to handle", "best way to resolve",
    "help me with jira ticket", "help me resolve", "help me with ticket",
    "advise me", "what's the best approach", "how should i proceed",
)


def _ticket_advice_intent(query: str) -> bool:
    """True when the user is asking how to approach/resolve an EXISTING ticket.

    Requires both an advice cue and a ticket reference (a Jira key like OPS-2, or the word
    "ticket") so ordinary questions are unaffected — only ticket-flavored advice is rerouted.
    """
    import re
    q = query.lower()
    has_advice = any(p in q for p in _ADVICE_PHRASES)
    has_ticket_ref = "ticket" in q or bool(re.search(r"\b[A-Z][A-Z0-9]+-\d+\b", query))
    return has_advice and has_ticket_ref


_INFORMATIONAL_RE = None
_EXPLICIT_TICKET_RE = None

def _is_informational(query: str) -> bool:
    """True for 'how do I / what is / where do I' questions — the user wants info, not an action."""
    import re
    global _INFORMATIONAL_RE, _EXPLICIT_TICKET_RE
    if _INFORMATIONAL_RE is None:
        _INFORMATIONAL_RE = re.compile(
            r'^(how\b|what\b|when\b|where\b|who\b|why\b|can you (tell|explain)|what\'s\b)',
            re.IGNORECASE,
        )
        _EXPLICIT_TICKET_RE = re.compile(
            r'\b(create|file|open|raise|log|submit|make)\s+(a\s+)?ticket\b',
            re.IGNORECASE,
        )
    q = query.strip()
    return bool(_INFORMATIONAL_RE.match(q)) and not bool(_EXPLICIT_TICKET_RE.search(q))


def _heuristic(query: str) -> str | None:
    import re
    q = query.lower()
    informational = _is_informational(query)
    for key, words in _KEYWORD_HINTS.items():
        # Informational questions ("how do I...", "what is...") are never ticket_triage
        # via keyword — they describe a process question, not an active problem report.
        if informational and key == "ticket_triage":
            continue
        for w in words:
            if " " in w:
                if w in q:
                    return key
            else:
                if re.search(rf"\b{re.escape(w)}\b", q):
                    return key
    return None


async def route(state: AgentState) -> AgentState:
    """Decide which specialized agent handles the request."""
    query = state["query"]
    history = state.get("history") or []

    # "How should I approach ticket OPS-2?" is a request to REASON about an existing ticket,
    # not to file a new one — route it to the advisor before the ticket_triage keywords fire.
    if _ticket_advice_intent(query):
        return {"route": "ticket_advice"}

    # Cheap keyword pass first; fall back to the LLM for anything ambiguous.
    choice = _heuristic(query)
    if choice is None:
        # Include the last 2 turns so the LLM can resolve follow-up questions.
        history_block = ""
        if history:
            recent = history[-4:]  # up to 2 full turns (user+assistant each)
            history_block = "Recent conversation:\n" + "\n".join(
                f"{m['sender'].capitalize()}: {m['content']}" for m in recent
            ) + "\n\n"
        raw = (await generate(
            f"{history_block}New message: {query}",
            system=_ROUTER_SYSTEM,
            temperature=0.0,
            num_predict=16,
        )).strip().lower()
        choice = next((k for k in AGENT_KEYS if k in raw), "policy_qa")

    return {"route": choice}

"""Action drafts for an existing Jira ticket.

These are NOT routed through the agent graph — they are triggered by explicit buttons in the
ticket workspace with a known intent (alert / email / report), so they run deterministically
through dedicated functions instead of relying on the keyword router. Every draft is grounded
in the ticket's own text plus the company knowledge base, and is reviewed/edited by the human
before any side-effect is performed by the .NET gateway.
"""

from app.agents.specialists import _PERSONA, _parse_json
from app.llm.client import generate
from app.rag import service as rag


def _ticket_block(ticket: dict) -> str:
    """Render the ticket into a compact, readable block for the prompt."""
    lines: list[str] = []
    if ticket.get("key"):
        lines.append(f"Ticket: {ticket['key']}")
    if ticket.get("summary"):
        lines.append(f"Summary: {ticket['summary']}")
    meta = []
    if ticket.get("issue_type"):
        meta.append(f"type {ticket['issue_type']}")
    if ticket.get("status"):
        meta.append(f"status {ticket['status']}")
    if ticket.get("priority"):
        meta.append(f"priority {ticket['priority']}")
    if meta:
        lines.append("(" + ", ".join(meta) + ")")
    if ticket.get("description"):
        lines.append(f"\nDescription:\n{ticket['description']}")
    comments = ticket.get("comments") or []
    if comments:
        lines.append("\nComments:")
        for c in comments:
            author = c.get("author") or "Unknown"
            body = (c.get("body") or "").strip()
            if body:
                lines.append(f"- {author}: {body}")
    return "\n".join(lines).strip()


def _retrieve_context(org_id: str, role_level: int, ticket: dict) -> tuple[str, list]:
    """Pull role-scoped company knowledge relevant to the ticket."""
    query = " ".join(filter(None, [ticket.get("summary"), ticket.get("description")]))[:600]
    if not query.strip():
        return "", []
    sources = rag.retrieve(org_id=org_id, role_level=role_level, query=query)
    context = "\n\n".join(s.text for s in sources)
    return context, sources


async def draft_alert(*, org_id: str, role_level: int, ticket: dict) -> dict:
    """Draft a concise Slack/Teams warning about the ticket."""
    context, _ = _retrieve_context(org_id, role_level, ticket)
    context_block = f"Relevant company knowledge:\n{context}\n\n" if context else ""
    prompt = (
        f"{context_block}"
        f"{_ticket_block(ticket)}\n\n"
        "Write a SHORT team alert about this support ticket for a Slack/Teams channel. "
        "Return ONLY a JSON object with keys: "
        "title (string — a short headline), "
        "message (string — 2-4 sentences: what is happening, who/what is affected, and what is "
        "needed now; mention the ticket key), "
        "severity (one of: Low, Medium, High, Critical — infer from the ticket). "
        "Use only facts present in the ticket or the company knowledge above — never invent details."
    )
    raw = await generate(
        prompt,
        system=_PERSONA + "You write concise, accurate operational alerts. You output only valid JSON.",
        temperature=0.4,
        json_mode=True,
    )
    return _parse_json(raw) or {"title": ticket.get("summary", "Ticket alert"), "message": raw, "severity": "Medium"}


async def draft_email(*, org_id: str, role_level: int, ticket: dict, manager_name: str | None = None) -> dict:
    """Draft a manager-facing escalation/status email about the ticket."""
    context, _ = _retrieve_context(org_id, role_level, ticket)
    context_block = f"Relevant company knowledge:\n{context}\n\n" if context else ""
    to_line = f"The email is addressed to {manager_name}. " if manager_name else ""
    prompt = (
        f"{context_block}"
        f"{_ticket_block(ticket)}\n\n"
        f"{to_line}"
        "Write a professional email to a manager about this support ticket — a clear status/escalation "
        "update. Return ONLY a JSON object with keys: "
        "subject (string — include the ticket key), "
        "body (string — a courteous email: situation, impact, current status, and what you recommend "
        "or need from the manager). "
        "Use only facts present in the ticket or the company knowledge above — never invent names, "
        "dates, or figures. Sign off as Miriani."
    )
    raw = await generate(
        prompt,
        system=_PERSONA + "You draft clear, professional business emails. You output only valid JSON and never invent facts.",
        temperature=0.35,
        json_mode=True,
    )
    return _parse_json(raw) or {"subject": f"Update on {ticket.get('key', 'ticket')}", "body": raw}


async def draft_report(*, org_id: str, role_level: int, ticket: dict) -> dict:
    """Produce a structured incident/ticket report grounded in the ticket + KB."""
    context, _ = _retrieve_context(org_id, role_level, ticket)
    context_block = f"Relevant company knowledge (cite where it applies):\n{context}\n\n" if context else ""
    prompt = (
        f"{context_block}"
        f"{_ticket_block(ticket)}\n\n"
        "Produce a concise support/incident report for this ticket. "
        "Return ONLY a JSON object with keys: "
        "title (string — include the ticket key), "
        "summary (string — what the ticket is about), "
        "impact (string — who/what is affected and how badly), "
        "status (string — current state), "
        "root_cause_hypothesis (string — the most likely cause based on the evidence), "
        "recommended_actions (array of strings — concrete next steps), "
        "severity (one of: Low, Medium, High, Critical). "
        "Base everything on the ticket and the company knowledge above; do not invent facts."
    )
    raw = await generate(
        prompt,
        system=_PERSONA + "You write precise, factual incident reports. You output only valid JSON.",
        temperature=0.2,
        json_mode=True,
    )
    parsed = _parse_json(raw)
    if parsed is None:
        return {
            "title": ticket.get("summary", "Ticket report"),
            "summary": raw,
            "recommended_actions": [],
            "severity": "Medium",
        }
    if not isinstance(parsed.get("recommended_actions"), list):
        parsed["recommended_actions"] = [str(parsed.get("recommended_actions"))] if parsed.get("recommended_actions") else []
    return parsed


DRAFTERS = {
    "alert": draft_alert,
    "email": draft_email,
    "report": draft_report,
}

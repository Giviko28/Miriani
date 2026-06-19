"""Shared state passed between nodes in the agent graph."""

from typing import Any, TypedDict

# Stable keys for the specialized agents. These mirror the BusinessProcess.AgentKey
# values seeded in the .NET database.
AGENT_KEYS = ["greeting", "policy_qa", "email_draft", "ticket_triage", "ticket_advice", "db_query", "doc_qa"]


class AgentState(TypedDict, total=False):
    # Inputs
    query: str
    org_id: str
    role_level: int
    history: list[dict[str, str]]  # recent prior turns: {sender, content}
    attachment_text: str | None  # ephemeral file text for THIS message only (never stored)
    attachment_name: str | None
    user_name: str | None  # display name of the logged-in user (for personalization)

    # Router output
    route: str

    # Agent output
    answer: str
    used_context: bool
    sources: list[dict[str, Any]]
    structured: dict[str, Any] | None

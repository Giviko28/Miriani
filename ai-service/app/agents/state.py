"""Shared state passed between nodes in the agent graph."""

from typing import Any, TypedDict

# Stable keys for the specialized agents. These mirror the BusinessProcess.AgentKey
# values seeded in the .NET database.
AGENT_KEYS = ["policy_qa", "doc_summary", "email_draft", "report_draft", "invoice_gen", "greeting",
              "leave_request", "onboarding_gen", "contract_scan", "db_query"]


class AgentState(TypedDict, total=False):
    # Inputs
    query: str
    org_id: str
    role_level: int
    history: list[dict[str, str]]  # recent prior turns: {sender, content}

    # Router output
    route: str

    # Agent output
    answer: str
    used_context: bool
    sources: list[dict[str, Any]]
    structured: dict[str, Any] | None

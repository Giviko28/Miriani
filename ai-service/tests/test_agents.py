"""Unit tests for agent routing heuristics and invoice helpers (no LLM required)."""

import asyncio

import pytest

from app.agents.router import _heuristic
from app.agents.specialists import _find_number, _history_block, _parse_json


@pytest.mark.parametrize(
    "query, expected",
    [
        ("Please create an invoice for ACME", "invoice_gen"),
        ("Write an email to the team", "email_draft"),
        ("Draft a quarterly report", "report_draft"),
        ("Summarize the onboarding document", "doc_summary"),
    ],
)
def test_router_heuristic(query, expected):
    assert _heuristic(query) == expected


def test_router_heuristic_no_match_returns_none():
    assert _heuristic("How many vacation days do I get?") is None


def test_history_block_formats_recent_turns():
    block = _history_block({"history": [
        {"sender": "user", "content": "What is the remote policy?"},
        {"sender": "assistant", "content": "Up to 3 days a week."},
    ]})
    assert "Conversation so far:" in block
    assert "User: What is the remote policy?" in block
    assert "Assistant: Up to 3 days a week." in block


def test_history_block_empty_when_no_history():
    assert _history_block({}) == ""


def test_run_agents_accepts_history(monkeypatch):
    import app.agents.specialists as sp

    async def fake_generate(prompt, system=None):
        return "drafted"

    monkeypatch.setattr(sp, "generate", fake_generate)
    monkeypatch.setattr(sp, "_retrieve", lambda state: [])

    from app.agents.graph import run_agents

    history = [{"sender": "user", "content": "hi"}, {"sender": "assistant", "content": "hello"}]
    state = asyncio.run(run_agents(org_id="o", role_level=0, query="write an email to the team", history=history))
    assert state["route"] in {"email_draft", "policy_qa", "report_draft", "doc_summary", "invoice_gen"}
    assert "answer" in state


def test_find_number_tolerates_key_variants():
    assert _find_number({"quantity": 10}, ("quantity", "qty")) == 10.0
    assert _find_number({"_unit_price": 150}, ("unit_price", "price")) == 150.0
    assert _find_number({"Rate": "200"}, ("rate", "price")) == 200.0
    assert _find_number({"misc": 5}, ("quantity",)) == 0.0


def test_parse_json_handles_code_fences():
    raw = "```json\n{\"client\": \"ACME\", \"total\": 100}\n```"
    parsed = _parse_json(raw)
    assert parsed == {"client": "ACME", "total": 100}


def test_parse_json_returns_none_for_garbage():
    assert _parse_json("not json at all") is None

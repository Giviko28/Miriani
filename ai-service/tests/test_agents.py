"""Unit tests for agent routing heuristics and invoice helpers (no LLM required)."""

import pytest

from app.agents.router import _heuristic
from app.agents.specialists import _find_number, _parse_json


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

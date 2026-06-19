"""Specialized agents — one per business capability.

Each is a LangGraph node: reads the request (and caller's role-scoped context where useful)
and produces an answer. Kept agents: greeting, policy_qa, email_draft, ticket_triage,
ticket_advice, db_query.
"""

import json

from app.agents.state import AgentState
from app.db import connector as db_connector
from app.db import schema_cache
from app.llm.client import generate
from app.rag import service as rag

_PERSONA = (
    "You are Miriani, a warm and professional AI assistant for Silknet, Georgia's leading "
    "telecom operator. You go by Mirian for short. "
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


async def _check_required(query: str, history_block: str, required: str) -> str | None:
    """Return a clarifying question if the request is missing required info, else None."""
    raw = (await generate(
        f"{history_block}User request: {query}\n\nRequired information: {required}\n\n"
        "Does the user request contain ALL of the required information listed above?\n"
        "Reply with exactly one of:\n"
        "SUFFICIENT\n"
        "MISSING: <short comma-separated list of what is absent>",
        system="You check whether a request contains required information. Reply only with SUFFICIENT or MISSING: <list>.",
        temperature=0.0,
        num_predict=64,
    )).strip()
    if raw.upper().startswith("MISSING:"):
        missing = raw[len("MISSING:"):].strip()
        return (
            f"I'd be happy to help with that! To get started, I just need a few details:\n\n"
            f"**{missing}**\n\nCould you provide these?"
        )
    return None


def _history_block(state: AgentState) -> str:
    history = state.get("history") or []
    if not history:
        return ""
    lines = [f"{m['sender'].capitalize()}: {m['content']}" for m in history]
    return "Conversation so far:\n" + "\n".join(lines) + "\n\n"


def _attachment_text(state: AgentState) -> str:
    return (state.get("attachment_text") or "").strip()


def _attachment_block(state: AgentState) -> str:
    att = _attachment_text(state)
    if not att:
        return ""
    name = state.get("attachment_name") or "attached file"
    return f"Attached file for THIS message only — '{name}':\n{att}\n\n"


def _user_block(state: AgentState) -> str:
    name = (state.get("user_name") or "").strip()
    if not name:
        return ""
    return f"The person you are speaking with is: {name}.\n\n"


async def greeting(state: AgentState) -> AgentState:
    """Respond to greetings and introductions as Miriani."""
    prompt = (
        f"{_history_block(state)}{_user_block(state)}"
        f"The user said: {state['query']}\n\n"
        "Always start by introducing yourself as Miriani (or Mirian for short). "
        "Greet them back warmly, use their name if you know it, and briefly mention "
        "what you can help with: company policies and SLA documentation, "
        "drafting professional emails, filing IT support tickets, and pulling incident reports "
        "from the operations database. "
        "Keep it short, friendly, and natural — 2 to 4 sentences, no bullet lists."
    )
    reply = await generate(
        prompt,
        system=(
            _PERSONA +
            "You are having a casual, friendly conversation. "
            "Always introduce yourself by name (Miriani) early in your reply."
        ),
        temperature=0.7,
    )
    return {"answer": reply, "used_context": False, "sources": [], "structured": None}


async def policy_qa(state: AgentState) -> AgentState:
    """Answer a question grounded in role-scoped company documents."""
    result = await rag.answer(
        org_id=state["org_id"], role_level=state.get("role_level", 0), query=state["query"],
        history=state.get("history"),
        attachment_text=state.get("attachment_text"), attachment_name=state.get("attachment_name"),
    )
    return {
        "answer": result.answer,
        "used_context": result.used_context,
        "sources": _sources_to_dicts(result.sources),
        "structured": None,
    }


async def email_draft(state: AgentState) -> AgentState:
    """Draft a business email for the request, grounded in any relevant context."""
    clarify = await _check_required(
        state["query"], _history_block(state),
        "who the email is addressed to (recipient name or company), and the main topic or purpose",
    )
    if clarify:
        return {"answer": clarify, "used_context": False, "sources": [], "structured": None}

    sources = _retrieve(state)
    context = "\n\n".join(s.text for s in sources)
    context_block = f"Relevant company context:\n{context}\n\n" if context else ""
    user = (state.get("user_name") or "").strip()
    sign_instruction = f" Sign the email as {user}." if user else ""
    prompt = (
        f"{_history_block(state)}{_user_block(state)}{_attachment_block(state)}{context_block}"
        f"Write a professional business email for this request:\n{state['query']}\n\n"
        f"Include a subject line. Keep it concise and courteous.{sign_instruction}\n\n"
        "IMPORTANT: Use only facts explicitly stated in the request or the context above. "
        "Do not invent names, dates, figures, or details that were not provided."
    )
    reply = await generate(
        prompt,
        system=_PERSONA + "You draft clear, professional business emails. You never invent facts — if a detail is not in the request or context, you leave a placeholder like [client name] instead of guessing.",
        temperature=0.35,
    )
    return {"answer": reply, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": None}


async def ticket_triage(state: AgentState) -> AgentState:
    """Triage an IT/support request into a structured ticket ready to file in Jira."""
    sources = _retrieve(state)
    context = "\n\n".join(s.text for s in sources)
    context_block = f"Relevant internal knowledge (use to suggest a resolution):\n{context}\n\n" if context else ""
    prompt = (
        f"{_history_block(state)}{_attachment_block(state)}{context_block}"
        f"Support/IT request: {state['query']}\n\n"
        "Triage this into a support ticket. "
        "Return ONLY a JSON object with keys: "
        "summary (string — a short one-line ticket title), "
        "description (string — a clear restatement of the problem with any troubleshooting steps to try), "
        "priority (one of: Low, Medium, High, Critical — infer urgency from the wording), "
        "issue_type (one of: Bug, Task, Incident, Service Request), "
        "category (string — e.g. Network, Hardware, Access, Software, Other)."
    )
    raw = await generate(prompt, system="You output only valid JSON, no prose, no code fences.", temperature=0.1, json_mode=True)
    structured = _parse_json(raw)
    if structured is None:
        return {"answer": raw, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": None}
    summary = (
        "I've drafted a support ticket for you. Take a look at the details below — "
        "if everything looks right, click **Create Jira ticket** to file it."
    )
    return {"answer": summary, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": structured}


async def ticket_advice(state: AgentState) -> AgentState:
    """Reason about how to approach/resolve an EXISTING support ticket."""
    sources = _retrieve(state)
    context = "\n\n".join(s.text for s in sources)
    context_block = (
        f"Relevant company knowledge (use it where it applies and say when you are):\n{context}\n\n"
        if context else
        "No directly relevant company documents were found for this issue.\n\n"
    )
    prompt = (
        f"{_history_block(state)}{_attachment_block(state)}{context_block}"
        f"The user already has this support ticket and is asking how to approach it:\n{state['query']}\n\n"
        "Reason through it and give a practical, actionable answer:\n"
        "- A brief diagnosis of the most likely cause.\n"
        "- Clear step-by-step actions to resolve it (or to investigate further).\n"
        "- Who to involve or escalate to if those steps don't resolve it.\n\n"
        "Lean on the company knowledge above where it applies and say so; where the documents "
        "don't cover it, use sound general IT/support best practice and note that. "
        "The ticket already exists — do NOT suggest creating or filing a new one. "
        "Be concise and concrete; prefer short numbered steps."
    )
    reply = await generate(
        prompt,
        system=_PERSONA + "You are an experienced IT/support advisor. You reason through problems and give practical, step-by-step resolutions.",
        temperature=0.3,
    )
    return {"answer": reply, "used_context": bool(context), "sources": _sources_to_dicts(sources), "structured": None}


async def db_explore(state: AgentState) -> AgentState:
    """Explore every table in the connected DB, build a natural-language description, and save it."""
    cached = schema_cache.load(state["org_id"])
    if not cached:
        return {
            "answer": "No database is connected. Ask your admin to connect one via Admin → Database.",
            "used_context": False, "sources": [], "structured": None,
        }

    conn_str = cached["connection_string"]
    schema = cached["schema"]
    tables = schema.get("tables", [])

    samples: list[str] = []
    for table in tables:
        try:
            rows = db_connector.execute_select(conn_str, f"SELECT * FROM {table['name']} LIMIT 5")
            rows_text = json.dumps(rows, default=str)
        except Exception:
            rows_text = "(could not read)"
        col_list = ", ".join(f"{c['name']} ({c['type']})" for c in table["columns"])
        samples.append(f"Table: {table['name']}\nColumns: {col_list}\nSample rows: {rows_text}")

    sample_block = "\n\n".join(samples)
    prompt = (
        f"You have been given a database with the following tables and sample data:\n\n{sample_block}\n\n"
        "Write a clear, concise description of this database — what it stores, what each table "
        "represents, the key columns, and any notable data patterns you can see from the samples. "
        "This description will be used as permanent context so you always know what data is available. "
        "Write it as a single cohesive paragraph per table, no bullet lists."
    )
    summary = await generate(prompt, system=_PERSONA + "You write precise, factual database descriptions.")
    schema_cache.save_summary(state["org_id"], summary)
    table_names = ", ".join(t["name"] for t in tables)
    return {
        "answer": f"I've explored and memorized the database. Here's what I found:\n\n{summary}",
        "used_context": False,
        "sources": [],
        "structured": {"tables_explored": table_names, "summary_saved": True},
    }


async def db_query(state: AgentState) -> AgentState:
    """Answer questions by generating and executing a SELECT query against the org's connected DB."""
    cached = schema_cache.load(state["org_id"])
    if not cached:
        return {
            "answer": (
                "No external database is connected for your organization. "
                "Ask your admin to connect one via Admin → Database."
            ),
            "used_context": False, "sources": [], "structured": None,
        }

    schema = cached["schema"]
    conn_str = cached["connection_string"]
    saved_summary = cached.get("summary", "")
    schema_text = db_connector.render_schema(schema)

    from datetime import date
    _today = date.today()
    date_hint = (
        f"IMPORTANT: Today's date is {_today.isoformat()} (the current year is {_today.year}). "
        f"Do NOT assume any other year. For relative dates like 'this week', 'today', "
        f"or 'recently', reason from {_today.isoformat()} and prefer the database's own "
        f"current-date function over hard-coding a year.\n"
    )

    if conn_str.startswith("sqlite"):
        dialect_hint = "Database type: SQLite. Use date('now') for today, NOT CURDATE() or NOW(). Use strftime() for date formatting."
    elif conn_str.startswith("postgresql"):
        dialect_hint = "Database type: PostgreSQL. Use CURRENT_DATE for today."
    elif conn_str.startswith("mysql"):
        dialect_hint = "Database type: MySQL. Use CURDATE() for today."
    else:
        dialect_hint = "Database type: SQL Server. Use GETDATE() for today."

    context_block = f"Database description:\n{saved_summary}\n\n" if saved_summary else ""
    sqlite_examples = (
        "SQLite date examples:\n"
        "  incidents this week:  WHERE created_at >= date('now', '-7 days')\n"
        "  open P1s:             WHERE severity = 'P1' AND status != 'Resolved'\n"
        "  resolved today:       WHERE date(resolved_at) = date('now')\n"
        "  NEVER use MONTH(), YEAR(), CURDATE(), NOW(), GETDATE() — SQLite does not support them.\n"
    ) if conn_str.startswith("sqlite") else ""

    sql_prompt = (
        f"{context_block}"
        f"{date_hint}"
        f"{dialect_hint}\n"
        f"{sqlite_examples}"
        f"Database schema:\n{schema_text}\n\n"
        f"Question: {state['query']}\n\n"
        "Write a single valid SELECT SQL query. "
        "CRITICAL: only reference tables and columns that appear in the schema above — "
        "never invent or assume extra tables. Keep the query simple and direct. "
        "Output only the SQL, no explanation, no code fences, no trailing semicolon."
    )
    sql = (await generate(sql_prompt, system="You output only a valid SQL SELECT statement, nothing else.", temperature=0.0)).strip().rstrip(";")
    sql = _autocorrect_sql(sql, conn_str)

    try:
        rows = db_connector.execute_select(conn_str, sql)
    except Exception as exc:
        repair_prompt = (
            f"{date_hint}"
            f"{dialect_hint}\n{sqlite_examples}"
            f"Database schema:\n{schema_text}\n\n"
            f"Question: {state['query']}\n\n"
            f"This SQL failed:\n{sql}\n\nError: {exc}\n\n"
            "Write a corrected single valid SELECT query that fixes the error. "
            "Output only the SQL, no explanation, no code fences, no trailing semicolon."
        )
        repaired = (await generate(repair_prompt, system="You output only a valid SQL SELECT statement, nothing else.", temperature=0.0)).strip().rstrip(";")
        repaired = _autocorrect_sql(repaired, conn_str)
        try:
            rows = db_connector.execute_select(conn_str, repaired)
            sql = repaired
        except Exception as exc2:
            return {
                "answer": f"I couldn't retrieve that information from the database. The query failed: {exc2}",
                "used_context": False, "sources": [],
                "structured": {"sql": repaired, "error": str(exc2), "rows": [], "total_rows": 0},
            }

    capped = rows[:20]
    result_text = json.dumps(capped, indent=2, default=str)
    answer_prompt = (
        f"{_history_block(state)}"
        f"{context_block}"
        f"(Today is {_today.isoformat()}.)\n"
        f"Question: {state['query']}\n\n"
        f"Data retrieved ({len(rows)} row(s)):\n{result_text}\n\n"
        "Answer the question directly and naturally. "
        "Do NOT mention SQL, queries, databases, or how you retrieved the data. "
        "Just give a clean, friendly answer. If 0 rows, say nothing matched and keep it brief."
    )
    answer = await generate(answer_prompt, system=_PERSONA + "You answer questions naturally. Never mention SQL, queries, or databases in your response.", temperature=0.3)

    return {
        "answer": answer,
        "used_context": False,
        "sources": [],
        "structured": {"sql": sql, "rows": capped, "total_rows": len(rows)},
    }


def _autocorrect_sql(sql: str, conn_str: str) -> str:
    """Fix common cross-dialect mistakes the model makes for SQLite."""
    if not conn_str.startswith("sqlite"):
        return sql
    import re as _re
    sql = _re.sub(r'\bCURDATE\(\)', "date('now')", sql, flags=_re.IGNORECASE)
    sql = _re.sub(r'\bNOW\(\)', "datetime('now')", sql, flags=_re.IGNORECASE)
    sql = _re.sub(r'\bGETDATE\(\)', "datetime('now')", sql, flags=_re.IGNORECASE)
    sql = _re.sub(r'\bCURRENT_TIMESTAMP\b', "datetime('now')", sql, flags=_re.IGNORECASE)
    sql = _re.sub(r'\bMONTH\(([^)]+)\)', lambda m: f"CAST(strftime('%m', {m.group(1)}) AS INTEGER)", sql, flags=_re.IGNORECASE)
    sql = _re.sub(r'\bYEAR\(([^)]+)\)', lambda m: f"CAST(strftime('%Y', {m.group(1)}) AS INTEGER)", sql, flags=_re.IGNORECASE)
    sql = _re.sub(r'\bDAY\(([^)]+)\)', lambda m: f"CAST(strftime('%d', {m.group(1)}) AS INTEGER)", sql, flags=_re.IGNORECASE)
    return sql


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


SPECIALISTS = {
    "greeting":      greeting,
    "policy_qa":     policy_qa,
    "email_draft":   email_draft,
    "ticket_triage": ticket_triage,
    "ticket_advice": ticket_advice,
    "db_query":      db_query,
}

"""SQLAlchemy-backed connector: schema introspection and read-only query execution."""

from sqlalchemy import create_engine, inspect, text


def _engine_args(connection_string: str) -> dict:
    if connection_string.startswith("sqlite"):
        return {"connect_args": {"check_same_thread": False}}
    return {}


def introspect(connection_string: str) -> dict:
    """Return {tables: [{name, columns: [{name, type}]}]} for the target DB."""
    engine = create_engine(connection_string, **_engine_args(connection_string))
    try:
        inspector = inspect(engine)
        tables = []
        for table_name in inspector.get_table_names():
            columns = [
                {"name": col["name"], "type": str(col["type"])}
                for col in inspector.get_columns(table_name)
            ]
            tables.append({"name": table_name, "columns": columns})
        return {"tables": tables}
    finally:
        engine.dispose()


def execute_select(connection_string: str, sql: str) -> list[dict]:
    """Execute a SELECT statement and return rows as plain dicts. Rejects non-SELECT."""
    clean = sql.strip().lstrip(";").strip()
    if not clean.upper().startswith("SELECT"):
        raise ValueError("Only SELECT queries are permitted.")
    engine = create_engine(connection_string, **_engine_args(connection_string))
    try:
        with engine.connect() as conn:
            result = conn.execute(text(clean))
            return [dict(row._mapping) for row in result]
    finally:
        engine.dispose()


def render_schema(schema: dict) -> str:
    """Render a schema dict as a compact human-readable string for LLM prompts."""
    lines = []
    for table in schema.get("tables", []):
        cols = ", ".join(f"{c['name']} {c['type']}" for c in table["columns"])
        lines.append(f"Table {table['name']}({cols})")
    return "\n".join(lines) if lines else "(no tables found)"

"""Per-org DB config cache backed by JSON files — survives AI service restarts."""

import json
from pathlib import Path

_CACHE_DIR = Path(__file__).parent.parent.parent / "db_configs"
_CACHE_DIR.mkdir(exist_ok=True)


def _path(org_id: str) -> Path:
    return _CACHE_DIR / f"{org_id}.json"


def save(org_id: str, connection_string: str, schema: dict) -> None:
    _path(org_id).write_text(
        json.dumps({"connection_string": connection_string, "schema": schema}, indent=2),
        encoding="utf-8",
    )


def load(org_id: str) -> dict | None:
    p = _path(org_id)
    if not p.exists():
        return None
    return json.loads(p.read_text(encoding="utf-8"))


def delete(org_id: str) -> None:
    p = _path(org_id)
    if p.exists():
        p.unlink()


def get_connection_string(org_id: str) -> str | None:
    data = load(org_id)
    return data["connection_string"] if data else None


def get_schema(org_id: str) -> dict | None:
    data = load(org_id)
    return data["schema"] if data else None

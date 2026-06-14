"""Thin async client for the local Ollama chat model."""

import httpx

from app.config import settings


async def generate(prompt: str, *, system: str | None = None) -> str:
    """Generate a completion from the local LLM (non-streaming)."""
    payload: dict = {"model": settings.ollama_model, "prompt": prompt, "stream": False}
    if system:
        payload["system"] = system

    async with httpx.AsyncClient(timeout=settings.request_timeout_seconds) as client:
        resp = await client.post(f"{settings.ollama_base_url}/api/generate", json=payload)
        resp.raise_for_status()
        return resp.json().get("response", "").strip()

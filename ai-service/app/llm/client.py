"""Thin async client for the local Ollama chat model."""

import httpx

from app.config import settings


async def generate(
    prompt: str,
    *,
    system: str | None = None,
    temperature: float | None = None,
    num_predict: int | None = None,
    json_mode: bool = False,
) -> str:
    """Generate a completion from the local LLM (non-streaming).

    Args:
        system: optional system prompt.
        temperature: sampling temperature; defaults to settings.default_temperature.
            Pass ~0.0 for deterministic tasks (routing, SQL, JSON extraction) and a
            higher value (0.5-0.7) for creative drafting.
        num_predict: cap on generated tokens (None = model default).
        json_mode: when True, ask Ollama to constrain output to valid JSON. Use this
            for the structured agents so we stop fighting malformed JSON.
    """
    options: dict = {
        # Explicitly widen the context window — Ollama defaults to 2048, which truncates
        # retrieved context and history for this 32k-capable model.
        "num_ctx": settings.num_ctx,
        "temperature": settings.default_temperature if temperature is None else temperature,
        "top_p": settings.top_p,
        "repeat_penalty": settings.repeat_penalty,
    }
    if num_predict is not None:
        options["num_predict"] = num_predict

    payload: dict = {
        "model": settings.ollama_model,
        "prompt": prompt,
        "stream": False,
        "options": options,
    }
    if system:
        payload["system"] = system
    if json_mode:
        payload["format"] = "json"

    async with httpx.AsyncClient(timeout=settings.request_timeout_seconds) as client:
        resp = await client.post(f"{settings.ollama_base_url}/api/generate", json=payload)
        resp.raise_for_status()
        return resp.json().get("response", "").strip()

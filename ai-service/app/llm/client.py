"""Thin async client for the chat model.

Provider is selected by settings.llm_provider:
  - "ollama" (local default): the host Ollama at OLLAMA_BASE_URL.
  - "groq"   (cloud):         Groq's OpenAI-compatible chat completions API.

The public `generate(...)` signature is identical for both so callers (agents, RAG) never
need to know which backend is live.
"""

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
    """Generate a completion (non-streaming).

    Args:
        system: optional system prompt.
        temperature: sampling temperature; defaults to settings.default_temperature.
            Pass ~0.0 for deterministic tasks (routing, SQL, JSON extraction) and a
            higher value (0.5-0.7) for creative drafting.
        num_predict: cap on generated tokens (None = model default).
        json_mode: when True, constrain the output to valid JSON. Use this for the
            structured agents so we stop fighting malformed JSON.
    """
    temp = settings.default_temperature if temperature is None else temperature
    if settings.llm_provider.lower() == "groq":
        return await _generate_groq(
            prompt, system=system, temperature=temp, num_predict=num_predict, json_mode=json_mode
        )
    return await _generate_ollama(
        prompt, system=system, temperature=temp, num_predict=num_predict, json_mode=json_mode
    )


async def _generate_ollama(
    prompt: str,
    *,
    system: str | None,
    temperature: float,
    num_predict: int | None,
    json_mode: bool,
) -> str:
    options: dict = {
        # Explicitly widen the context window — Ollama defaults to 2048, which truncates
        # retrieved context and history for this 32k-capable model.
        "num_ctx": settings.num_ctx,
        "temperature": temperature,
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


async def _generate_groq(
    prompt: str,
    *,
    system: str | None,
    temperature: float,
    num_predict: int | None,
    json_mode: bool,
) -> str:
    """Call Groq's OpenAI-compatible /chat/completions endpoint."""
    messages: list[dict] = []
    if system:
        messages.append({"role": "system", "content": system})
    messages.append({"role": "user", "content": prompt})

    payload: dict = {
        "model": settings.groq_model,
        "messages": messages,
        "temperature": temperature,
        "top_p": settings.top_p,
        "stream": False,
    }
    if num_predict is not None:
        payload["max_tokens"] = num_predict
    if json_mode:
        # Supported by Groq's Llama/Mixtral models; the prompt must mention JSON, which the
        # structured agents already do.
        payload["response_format"] = {"type": "json_object"}

    headers = {"Authorization": f"Bearer {settings.groq_api_key}"}
    async with httpx.AsyncClient(timeout=settings.request_timeout_seconds) as client:
        resp = await client.post(
            f"{settings.groq_base_url}/chat/completions", json=payload, headers=headers
        )
        resp.raise_for_status()
        data = resp.json()
        return (data["choices"][0]["message"]["content"] or "").strip()

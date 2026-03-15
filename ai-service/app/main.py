"""AI service — smoke-test stage.

For now this exposes a health check and a single /ping-llm endpoint that forwards a prompt
to the local Ollama model and returns its reply. This proves the ai-service -> Ollama leg
of the integration. RAG, ingestion, and agents are added in later milestones.
"""

import time

import httpx
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

from app.config import settings

app = FastAPI(title="BPA AI Service", version="0.0.1")


class PingRequest(BaseModel):
    prompt: str = "Say hello in one short sentence."


class PingResponse(BaseModel):
    model: str
    reply: str
    elapsed_seconds: float


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/ping-llm", response_model=PingResponse)
async def ping_llm(req: PingRequest) -> PingResponse:
    """Forward a prompt to Ollama and return the generated reply."""
    started = time.perf_counter()
    payload = {"model": settings.ollama_model, "prompt": req.prompt, "stream": False}
    try:
        async with httpx.AsyncClient(timeout=settings.request_timeout_seconds) as client:
            resp = await client.post(
                f"{settings.ollama_base_url}/api/generate", json=payload
            )
            resp.raise_for_status()
            data = resp.json()
    except httpx.HTTPError as exc:
        raise HTTPException(
            status_code=502, detail=f"Ollama request failed: {exc}"
        ) from exc

    elapsed = time.perf_counter() - started
    return PingResponse(
        model=settings.ollama_model,
        reply=data.get("response", "").strip(),
        elapsed_seconds=round(elapsed, 2),
    )

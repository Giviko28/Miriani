"""ChromaDB embedding function backed by a local Ollama embedding model.

Keeps embeddings on the local stack (no sentence-transformers/torch), consistent with the
project's local-LLM constraint. Chroma calls this automatically on add() and query().
"""

import httpx
from chromadb import Documents, EmbeddingFunction, Embeddings

from app.config import settings


class OllamaEmbeddingFunction(EmbeddingFunction):
    def __init__(self) -> None:
        self._url = f"{settings.ollama_base_url}/api/embeddings"
        self._model = settings.embedding_model
        self._client = httpx.Client(timeout=settings.request_timeout_seconds)

    def __call__(self, input: Documents) -> Embeddings:
        vectors: Embeddings = []
        for text in input:
            resp = self._client.post(self._url, json={"model": self._model, "prompt": text})
            resp.raise_for_status()
            vectors.append(resp.json()["embedding"])
        return vectors

    # Chroma 1.x requires these for persistence/telemetry of custom embedders.
    def name(self) -> str:
        return f"ollama:{settings.embedding_model}"

    # Persisted alongside the collection so Chroma can reconstruct the embedder.
    def get_config(self) -> dict:
        return {"model": settings.embedding_model, "base_url": settings.ollama_base_url}

    @staticmethod
    def build_from_config(config: dict) -> "OllamaEmbeddingFunction":
        return OllamaEmbeddingFunction()

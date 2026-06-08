from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Service configuration, overridable via environment variables or a .env file."""

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # Ollama (chat + embeddings)
    ollama_base_url: str = "http://localhost:11434"
    ollama_model: str = "qwen2.5:3b"
    embedding_model: str = "nomic-embed-text"
    request_timeout_seconds: float = 120.0

    # Generation tuning. Ollama defaults num_ctx to 2048, which silently truncates RAG
    # context + history for this 32k-capable model; we raise it so the model actually
    # sees everything it's given. Temperature defaults are set per-call in the client:
    # near-0 for routing/SQL/JSON (determinism), higher for drafting (fluency).
    num_ctx: int = 8192
    default_temperature: float = 0.3
    top_p: float = 0.8
    repeat_penalty: float = 1.15

    # Vector store (embedded, persistent ChromaDB)
    chroma_path: str = "./chroma_store"
    collection_name: str = "knowledge_base"

    # Chunking
    chunk_size: int = 800
    chunk_overlap: int = 120

    # Retrieval
    top_k: int = 4
    # Cosine-distance ceiling (0 = identical, 2 = opposite). With nomic-embed-text,
    # genuine topic matches land at 0.3–0.55; anything above 0.65 is off-topic noise
    # that causes the model to hallucinate rather than say "I don't know".
    retrieval_max_distance: float = 0.65


settings = Settings()

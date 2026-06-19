from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Service configuration, overridable via environment variables or a .env file."""

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # --- Providers ---
    # Local dev defaults to Ollama for both chat and embeddings (GPU on the host). The cloud
    # deployment overrides these via env vars (no GPU there): LLM_PROVIDER=groq and
    # EMBEDDING_PROVIDER=local. Nothing about local behavior changes unless these are set.
    llm_provider: str = "ollama"          # "ollama" | "groq"
    embedding_provider: str = "ollama"    # "ollama" | "local"

    # Ollama (chat + embeddings)
    ollama_base_url: str = "http://localhost:11434"
    ollama_model: str = "qwen2.5:3b"
    embedding_model: str = "nomic-embed-text"
    request_timeout_seconds: float = 120.0

    # Groq (cloud chat, OpenAI-compatible). Only used when llm_provider == "groq".
    groq_api_key: str = ""
    groq_base_url: str = "https://api.groq.com/openai/v1"
    groq_model: str = "llama-3.1-8b-instant"

    # Local CPU embeddings (Chroma's bundled ONNX all-MiniLM-L6-v2, 384-dim). Only used when
    # embedding_provider == "local". Runs entirely in-process, no API key, no GPU.
    local_embedding_model: str = "all-MiniLM-L6-v2"

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

    # Per-org external-DB config cache. Empty = repo-relative ./db_configs (local default);
    # the cloud sets DB_CONFIGS_DIR to a path on the mounted disk so it survives restarts.
    db_configs_dir: str = ""

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

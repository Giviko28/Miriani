from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Service configuration, overridable via environment variables or a .env file."""

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # Ollama (chat + embeddings)
    ollama_base_url: str = "http://localhost:11434"
    ollama_model: str = "qwen2.5:3b"
    embedding_model: str = "nomic-embed-text"
    request_timeout_seconds: float = 120.0

    # Vector store (embedded, persistent ChromaDB)
    chroma_path: str = "./chroma_store"
    collection_name: str = "knowledge_base"

    # Chunking
    chunk_size: int = 800
    chunk_overlap: int = 120

    # Retrieval
    top_k: int = 4


settings = Settings()

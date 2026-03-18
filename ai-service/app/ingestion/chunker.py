"""Split extracted text into overlapping chunks for embedding (via LangChain)."""

from langchain_text_splitters import RecursiveCharacterTextSplitter

from app.config import settings


def chunk_text(text: str) -> list[str]:
    """Break text into semantically reasonable, overlapping chunks."""
    splitter = RecursiveCharacterTextSplitter(
        chunk_size=settings.chunk_size,
        chunk_overlap=settings.chunk_overlap,
        separators=["\n\n", "\n", ". ", " ", ""],
    )
    return [c.strip() for c in splitter.split_text(text) if c.strip()]

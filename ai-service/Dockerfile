# syntax=docker/dockerfile:1
# FastAPI AI service. Build context is the repo's ai-service/ directory.
FROM python:3.12-slim

WORKDIR /app

# Faster, quieter pip; unbuffered logs for Render's log stream.
ENV PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PYTHONUNBUFFERED=1

COPY requirements.txt .
RUN pip install -r requirements.txt

COPY app ./app

# Pre-download the local ONNX embedding model so the first request isn't slow (and so it
# works even if the disk is wiped). Cached under /root/.cache/chroma inside the image.
RUN python -c "from chromadb.utils import embedding_functions as ef; ef.ONNXMiniLM_L6_V2()"

# Cloud overrides: use Groq for chat and the bundled CPU model for embeddings (no GPU here).
ENV LLM_PROVIDER=groq \
    EMBEDDING_PROVIDER=local

# Render injects $PORT; bind uvicorn to it (fall back to 8001 for local `docker run`).
CMD ["sh", "-c", "uvicorn app.main:app --host 0.0.0.0 --port ${PORT:-8001}"]

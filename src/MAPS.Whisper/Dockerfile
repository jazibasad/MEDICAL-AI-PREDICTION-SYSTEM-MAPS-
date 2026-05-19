# ============================================================
# MAPS Whisper Service — Lightweight HTTP wrapper around whisper.cpp
# Exposes: POST /transcribe  → returns { text: "..." }
# ============================================================

FROM python:3.11-slim AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y \
    build-essential cmake git curl ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# Install whisper.cpp Python bindings + FastAPI wrapper
RUN pip install --no-cache-dir \
    fastapi==0.110.0 \
    uvicorn==0.27.1 \
    python-multipart==0.0.9 \
    openai-whisper==20231117

# Download base model on build (small model, ~150MB)
RUN python -c "import whisper; whisper.load_model('base')"

COPY src/MAPS.Whisper/main.py .

EXPOSE 8090

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8090/health || exit 1

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8090"]

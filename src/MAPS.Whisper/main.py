"""
MAPS Whisper Service
FastAPI wrapper around OpenAI Whisper for medical voice transcription.
POST /transcribe — accepts audio file, returns transcribed text.
"""

import whisper
import tempfile
import os
from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.responses import JSONResponse

app    = FastAPI(title="MAPS Whisper Service", version="1.0")
model  = whisper.load_model("base")

# Medical term correction dictionary
MEDICAL_CORRECTIONS = {
    "die a beat ease": "diabetes",
    "new monia":       "pneumonia",
    "high per tension":"hypertension",
    "card yak":        "cardiac",
    "my o card ial":   "myocardial",
    "tack y card ia":  "tachycardia",
}

def apply_medical_corrections(text: str) -> str:
    for wrong, correct in MEDICAL_CORRECTIONS.items():
        text = text.replace(wrong, correct)
    return text


@app.get("/health")
def health():
    return {"status": "healthy", "model": "whisper-base"}


@app.post("/transcribe")
async def transcribe(audio: UploadFile = File(...)):
    if not audio.content_type.startswith("audio/"):
        raise HTTPException(status_code=400, detail="File must be an audio file.")

    with tempfile.NamedTemporaryFile(
        suffix=os.path.splitext(audio.filename or ".wav")[1],
        delete=False
    ) as tmp:
        tmp.write(await audio.read())
        tmp_path = tmp.name

    try:
        result = model.transcribe(tmp_path, language="en", fp16=False)
        text   = result.get("text", "").strip()
        text   = apply_medical_corrections(text)
        return JSONResponse({"text": text, "language": "en"})
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Transcription failed: {str(e)}")
    finally:
        os.unlink(tmp_path)

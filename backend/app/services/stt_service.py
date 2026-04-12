import os
import wave
import tempfile

import numpy as np
import av
from fastapi import UploadFile

from app.services.ai_service import _call_llm

CONFIG = {
    "stt_model_dir": "voice/zipformer_stt",
    "target_sample_rate": 16000,
}

_recognizer = None


def _get_content_type_suffix(content_type: str) -> str:
    content_type = content_type.lower()
    if "mp4" in content_type or "m4a" in content_type:
        return ".m4a"
    if "ogg" in content_type:
        return ".ogg"
    if "webm" in content_type:
        return ".webm"
    if "mp3" in content_type:
        return ".mp3"
    return ".wav"


def _get_recognizer():
    global _recognizer

    if _recognizer is not None:
        return _recognizer

    model_dir = CONFIG["stt_model_dir"]
    encoder_path = os.path.join(model_dir, "encoder-epoch-20-avg-10.int8.onnx")
    decoder_path = os.path.join(model_dir, "decoder-epoch-20-avg-10.onnx")
    joiner_path = os.path.join(model_dir, "joiner-epoch-20-avg-10.int8.onnx")
    tokens_path = os.path.join(model_dir, "config.json")

    for fpath in [encoder_path, decoder_path, joiner_path, tokens_path]:
        if not os.path.exists(fpath):
            raise FileNotFoundError(f"Không tìm thấy file model: {fpath}")

    print(f"Loading Sherpa model từ: {os.path.abspath(model_dir)}")

    import sherpa_onnx

    _recognizer = sherpa_onnx.OfflineRecognizer.from_transducer(
        tokens=tokens_path,
        encoder=encoder_path,
        decoder=decoder_path,
        joiner=joiner_path,
        num_threads=2,
        sample_rate=CONFIG["target_sample_rate"],
        feature_dim=80,
        decoding_method="greedy_search",
    )
    print("Sherpa model loaded successfully.")
    return _recognizer


def _read_audio_generic(audio_filename: str) -> tuple:
    try:
        with wave.open(audio_filename) as f:
            assert f.getnchannels() == 1
            assert f.getsampwidth() == 2
            samples = f.readframes(f.getnframes())
            samples_int16 = np.frombuffer(samples, dtype=np.int16)
            return samples_int16.astype(np.float32) / 32768, f.getframerate()
    except Exception:
        return _read_audio_with_av(audio_filename)


def _read_audio_with_av(audio_filename: str) -> tuple:
    container = None
    samples_list = []

    try:
        container = av.open(audio_filename)
        audio_stream = container.streams.audio[0]
        resampler = av.AudioResampler(
            format="s16", layout="mono", rate=CONFIG["target_sample_rate"]
        )

        for frame in container.decode(audio_stream):
            for rf in resampler.resample(frame):
                samples_list.append(rf.to_ndarray().flatten())

        for rf in resampler.resample(None):
            samples_list.append(rf.to_ndarray().flatten())

        samples = (
            np.concatenate(samples_list)
            if samples_list
            else np.array([], dtype=np.int16)
        )
        return samples.astype(np.float32) / 32768.0, CONFIG["target_sample_rate"]
    finally:
        if container is not None:
            container.close()


def _transcribe(audio_filename: str) -> str:
    samples, sample_rate = _read_audio_generic(audio_filename)

    recognizer = _get_recognizer()
    stream = recognizer.create_stream()
    stream.accept_waveform(sample_rate, samples)
    recognizer.decode_stream(stream)

    return stream.result.text.strip()


async def _correct_transcript(raw_text: str) -> str:
    if not raw_text or len(raw_text.strip()) < 3:
        return raw_text

    prompt = f"""Bạn là một chuyên gia chỉnh sửa transcription tiếng Việt.
Nhiệm vụ của bạn:
1. Giữ nguyên nội dung tiếng Việt
2. Sửa các từ tiếng Anh bị nhận dạng sai thành tiếng Anh đúng
3. Nếu có từ tiếng Việt viết sai chính tả, sửa lại cho đúng
4. Giữ nguyên ý nghĩa và ngữ cảnh

Input: "{raw_text}"
Output (chỉ trả về text đã sửa, không giải thích):"""

    try:
        corrected = (await _call_llm(prompt)).strip()
        print(f"Transcript corrected: '{raw_text}' -> '{corrected}'")
        return corrected
    except Exception as e:
        print(f"Transcript correction failed: {e}")
        return raw_text


async def transcribe_audio(audio_file: UploadFile) -> str:
    audio_bytes = await audio_file.read()
    await audio_file.seek(0)

    suffix = _get_content_type_suffix(audio_file.content_type or "")

    with tempfile.NamedTemporaryFile(suffix=suffix, delete=False) as tmp:
        tmp_path = tmp.name
        tmp.write(audio_bytes)

    try:
        raw_text = _transcribe(tmp_path)
        return await _correct_transcript(raw_text)
    finally:
        if os.path.exists(tmp_path):
            os.remove(tmp_path)

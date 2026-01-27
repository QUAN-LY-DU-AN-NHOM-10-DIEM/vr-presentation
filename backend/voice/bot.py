#
# Copyright (c) 2025, Daily
#
# SPDX-License-Identifier: BSD 2-Clause License
#
import os
import aiohttp

from dotenv import load_dotenv
from loguru import logger
from pipecat.audio.vad.silero import SileroVADAnalyzer
from pipecat.frames.frames import LLMRunFrame
from pipecat.pipeline.pipeline import Pipeline
from pipecat.pipeline.runner import PipelineRunner
from pipecat.pipeline.task import PipelineParams, PipelineTask
from pipecat.processors.aggregators.llm_context import LLMContext
from pipecat.processors.aggregators.llm_response_universal import LLMContextAggregatorPair

from pipecat.services.piper.tts import PiperTTSService
from zipformer_stt.sherpa_stt import SherpaSTTService
from pipecat.services.ollama.llm import OLLamaLLMService
from pipecat.transports.base_transport import TransportParams
from pipecat.transports.smallwebrtc.transport import SmallWebRTCTransport
from pipecat.processors.frameworks.rtvi import (
    RTVIObserver,
    RTVIProcessor,
    RTVIUserTranscriptionMessage,
    RTVIUserTranscriptionMessageData,
)
import time

load_dotenv(override=True)


# Monkey patch to fix timestamp validation issue
def fix_rtvi_timestamp_validation():
    try:
        from pipecat.processors.frameworks.rtvi import RTVIUserTranscriptionMessageData

        original_init = RTVIUserTranscriptionMessageData.__init__

        def patched_init(self, **kwargs):
            if "timestamp" in kwargs and isinstance(kwargs["timestamp"], int):
                kwargs["timestamp"] = str(kwargs["timestamp"])
            return original_init(self, **kwargs)

        RTVIUserTranscriptionMessageData.__init__ = patched_init
        logger.info("Applied timestamp validation fix for RTVIUserTranscriptionMessageData")
    except Exception as e:
        logger.warning(f"Could not apply timestamp fix: {e}")


# Apply the fix
fix_rtvi_timestamp_validation()


class CustomRTVIProcessor(RTVIProcessor):
    def __init__(self):
        super().__init__()
        self._last_tts_text = None
        self._last_tts_time = 0

    async def process_frame(self, frame, direction):
        # Filter out duplicate TTS frames
        if hasattr(frame, "text"):
            current_time = time.time()
            if (
                frame.text == self._last_tts_text and current_time - self._last_tts_time < 2.0
            ):  # 2 second debounce
                logger.debug(f"Filtering duplicate TTS frame: {frame.text}")
                return None
            self._last_tts_text = frame.text
            self._last_tts_time = current_time

        return await super().process_frame(frame, direction)


class CustomRTVIObserver(RTVIObserver):
    async def _proxy_task_handler(self, task, frame):
        try:
            # Fix timestamp in frame data before processing
            if hasattr(frame, "data") and hasattr(frame.data, "__dict__"):
                if hasattr(frame.data, "timestamp") and isinstance(frame.data.timestamp, int):
                    frame.data.timestamp = str(frame.data.timestamp)
                    logger.debug(f"Fixed frame timestamp: {frame.data.timestamp}")

            await super()._proxy_task_handler(task, frame)
        except Exception as e:
            error_str = str(e)
            if "timestamp" in error_str and "string_type" in error_str:
                logger.warning(f"Timestamp validation error caught and handled: {e}")
                # Try to fix the timestamp and retry
                return
            elif "AssertionError" in error_str:
                logger.warning(f"ICE candidate assertion error caught and handled: {e}")
                return
            else:
                logger.error(f"Unexpected error in RTVI observer: {e}")
                # Don't crash the task, just log the error
                return


async def run_bot(webrtc_connection):
    transport = SmallWebRTCTransport(
        webrtc_connection=webrtc_connection,
        params=TransportParams(
            audio_in_enabled=True,
            audio_out_enabled=True,
            data_out_enabled=True,
            vad_analyzer=SileroVADAnalyzer(),
            audio_out_10ms_chunks=2,
        ),
    )
    async with aiohttp.ClientSession() as session:
        stt = SherpaSTTService(model_dir="./zipformer_stt")
        tts = PiperTTSService(
            base_url="http://localhost:5000", aiohttp_session=session, sample_rate=24000
        )
        llm = OLLamaLLMService(model="qwen2.5:7b")

        context = LLMContext(
            [
                {
                    "role": "user",
                    "content": "Bạn là một trợ lý ảo thân thiện và hữu ích. Bắt đầu bằng cách chào hỏi người dùng một cách nồng nhiệt và giới thiệu về bản thân bạn.",
                }
            ],
        )
        context_aggregator = LLMContextAggregatorPair(context)

        rtvi = CustomRTVIProcessor()

        pipeline = Pipeline(
            [
                transport.input(),  # Transport user input
                rtvi,
                stt,
                context_aggregator.user(),
                llm,  # LLM
                tts,  # TTS
                transport.output(),  # Transport bot output
                context_aggregator.assistant(),
            ]
        )

        task = PipelineTask(
            pipeline,
            params=PipelineParams(
                enable_metrics=True,
                enable_usage_metrics=True,
            ),
            observers=[CustomRTVIObserver(rtvi)],
        )

        @transport.event_handler("on_client_connected")
        async def on_client_connected(transport, client):
            logger.info("Pipecat Client connected")
            # Signal bot is ready to receive messages
            await rtvi.set_bot_ready()
            await task.queue_frames([LLMRunFrame()])

        @transport.event_handler("on_client_disconnected")
        async def on_client_disconnected(transport, client):
            logger.info("Pipecat Client disconnected")
            await task.cancel()

        runner = PipelineRunner(handle_sigint=False)
        await runner.run(task)

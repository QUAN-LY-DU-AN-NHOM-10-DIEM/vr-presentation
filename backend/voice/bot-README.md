# AI Voice Agent - Vietnamese AI Assistant

A real-time web application for voice conversations with AI assistant supporting Vietnamese language, running completely locally using WebRTC and modern speech recognition technologies.

## 🌟 Features

- **Real-time Voice Chat**: Direct voice conversation in Vietnamese
- **Speech-to-Text**: Accurate voice recognition with Sherpa-ONNX - Zipformer
- **Text-to-Speech**: Natural voice with Piper TTS
- **AI Assistant**: Qwen2.5 artificial intelligence from Ollama
- **Web-based Interface**: Modern, user-friendly web interface
- **Real-time Processing**: Instant signal processing with WebRTC

## 🏗️ System Architecture

```
┌─────────────────┐    WebRTC     ┌──────────────────┐
│   Browser UI    │ ◄──────────►  │   FastAPI Server │
│   (Frontend)    │               │   (Backend)      │
└─────────────────┘               └──────────────────┘
                                           │
                           ┌───────────────┼───────────────┐
                           │               │               │
                   ┌───────▼──────┐  ┌─────▼─────┐  ┌─────▼─────┐
                   │  Piper TTS   │  │Sherpa STT │  │ Ollama LLM│
                   │ (localhost)  │  │  Service  │  │ Service   │
                   │   :5000      │  │           │  │           │
                   └──────────────┘  └───────────┘  └───────────┘
```

## 📹 Demo Video (demo-voice-ai-local.mp4)

<div align="center">
  <video src="./demo-voice-ai-local.mp4" width="100%" controls title="Demo AI Voice Agent">
    Your browser does not support direct video playback.
  </video>
</div>

## 🚀 Quick Start

### Prerequisites

- Python 3.8+
- Node.js 16+
- Ollama
- Git

### Installation

1. **Clone repository**
```bash
git clone https://github.com/LTB122/pipecat-voice-agent-local-vietnamese.git
cd custom_bot
```

2. **Create virtual environment**
```bash
python -m venv venv
source venv/bin/activate  # Linux/Mac
# or
venv\Scripts\activate     # Windows
```

3. **Install dependencies**
```bash
# Full installation (recommended)
pip install -r requirements.txt

# Or minimal installation for faster setup
pip install -r requirements-minimal.txt

# TTS service dependencies
cd piper_tts
pip install -r requirements.txt
```

4. **Download Ollama model**
```bash
ollama pull qwen2.5:7b
```

5. **Install Piper TTS**

Piper TTS only requires downloading the executable binary to use immediately.

**Download Piper TTS:**
- **Linux (x86_64)**: Download `piper_amd64.tar.gz`
- **Windows (64-bit)**: Download `piper_windows_amd64.zip`
- **Mac/Raspberry Pi**: See more at Piper Releases

**Installation:**
- Download Piper from [Piper Releases](https://github.com/rhasspy/piper/releases)
- Extract to a folder (recommend placing in `piper_tts/piper` directory)
- Download voice models from [Hugging Face](https://huggingface.co/rhasspy/piper-voices)
- Place models in `piper_tts/piper/` directory
- Update `server.py` in `piper_tts` to point to correct model path

- **Start TTS server**
```bash
cd piper_tts
python server.py
```

6. **Install Speech-to-Text (STT) Models**

Clone full models from K2 Speech Recognition repository:

```bash
cd zipformer_stt
git clone https://huggingface.co/spaces/hynt/k2-automatic-speech-recognition-demo
```

Or manual download:
1. Visit [K2 Automatic Speech Recognition](https://huggingface.co/spaces/hynt/k2-automatic-speech-recognition-demo)
2. Download all files in the directory
3. Place in `zipformer_stt/` directory

7. **Start main application**
```bash
cd ..
python server.py
```

7. **Access application**
Open browser and navigate to: `http://localhost:7860`


## 🐳 Docker Deployment & TURN Server

### Why do we need TURN server?

When running the application in Docker or on Linux, WebRTC may encounter NAT traversal issues. TURN server helps:

1. **NAT Traversal**: Helps devices behind NAT connect
2. **Firewall Bypass**: Bypass firewalls blocking P2P connections
3. **Fallback Connectivity**: Provides backup connection method when STUN fails

### Platform-specific Details

#### ✅ **Linux**
Docker Linux allows advanced network configuration:

```bash
# Run with host networking (preferred for local dev)
docker run -d --network=host --name voice-agent voice-agent:latest

# Or expose specific ports
docker run --rm \
  --sysctl net.ipv4.ip_local_port_range="40000 40100" \
  -p 7860:7860 \
  -p 40000-40100:40000-40100/udp \
  --name voice-agent \
  voice-agent:latest
```

> ⚠️ **Port Limitation**
> 
> `SmallWebRTCConnection` uses aiortc, which has limitations in port control for `gather_candidates`. Docker configuration on Linux solves this by:
> 1. Limiting port range container can use
> 2. Explicitly exposing allowed ports

#### 🍏 **macOS & Windows Docker Desktop**

Docker Desktop on macOS **does not support `--network=host`** and UDP port forwarding has limitations:

```bash
docker run --rm \
  -p 7860:7860 \
  --name voice-agent \
  voice-agent:latest
```

> ⚠️ **macOS/Windows needs TURN server**  
> Because UDP ports used by aiortc cannot be configured, direct WebRTC P2P connection is unlikely to succeed.  
> TURN server is mandatory to relay media traffic when NAT traversal fails.

### Configure TURN Server

#### 1. Install coturn

```bash
# Ubuntu/Debian
sudo apt update
sudo apt install coturn

# CentOS/RHEL
sudo yum install coturn

# macOS
brew install coturn
```

#### 2. Configure coturn

```bash
sudo nano /etc/turnserver.conf
```

```conf
listening-port=3478
tls-listening-port=5349
listening-ip=0.0.0.0
total-quota=100
user-quota=12
max-bps=64000
use-auth-secret
static-auth-secret=your-secret-key-here
realm=your-domain.com
total-quota=100

# Log configuration
log-file=/var/log/turnserver.log
verbose
```

#### 3. Create user credentials

```bash
sudo turnadmin -a -u your-username -p your-password -r your-domain.com
```

#### 4. Start coturn service

```bash
# Linux
sudo systemctl start coturn
sudo systemctl enable coturn

# macOS
brew services start coturn
```

#### 5. Update WebRTC Configuration

In `index.html`, update ICE servers:

```javascript
const config = {
    iceServers: [
        {
            urls: "stun:stun.l.google.com:19302",
        },
        {
            urls: "turn:your-server.com:3478",
            username: "your-username",
            credential: "your-password"
        }
    ]
};
```

### Docker Compose with TURN

Create `docker-compose.yml` file:

```yaml
version: '3.8'

services:
  app:
    build: .
    ports:
      - "7860:7860"
    environment:
      - OLLAMA_BASE_URL=http://host.docker.internal:11434
      - PIPER_TTS_URL=http://host.docker.internal:5000
      - TURN_SERVER_URL=turn:your-turn-server.com:3478
      - TURN_USERNAME=your-username
      - TURN_CREDENTIAL=your-password
    depends_on:
      - tts
      - ollama
    volumes:
      - ./config:/app/config

  tts:
    build: ./piper_tts
    ports:
      - "5000:5000"
    volumes:
      - ./models:/app/models

  ollama:
    image: ollama/ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama

  # TURN server option (for production)
  coturn:
    image: coturn/coturn:latest
    ports:
      - "3478:3478/udp"
      - "3478:3478/tcp"
      - "5349:5349/udp"
      - "5349:5349/tcp"
    volumes:
      - ./turnserver.conf:/etc/coturn/turnserver.conf
    command: ["turnserver", "-c", "/etc/coturn/turnserver.conf"]

volumes:
  ollama_data:
```

## 🔍 STUN vs TURN

### STUN Server
- **Purpose**: Helps client discover public IP and port when behind NAT
- **Usage**: Attempts direct P2P connection
- **Example**: `stun:stun.l.google.com:19302`

### TURN Server  
- **Purpose**: Relays media when P2P fails
- **Usage**: Fallback when NAT/firewall blocks connection
- **Required**: macOS/Windows Docker, strict NAT environments

## 📁 Project Structure

```
pipecat-voice-agent-local-vietnamese
├── .gitignore
├── bot.py
├── bot_cli.py
├── demo-voice-ai-local.mp4
├── index.html
├── LICENSE
├── piper_tts
│   ├── piper
│   │   ├── espeak-ng-data/
│   │   ├── espeak-ng.dll
│   │   ├── libtashkeel_model.ort
│   │   ├── onnxruntime.dll
│   │   ├── onnxruntime_providers_shared.dll
│   │   ├── piper.exe
│   │   ├── piper_phonemize.dll
│   │   ├── pkgconfig
│   │   ├── vi_VN-vais1000-medium.onnx
│   │   ├── vi_VN-vais1000-medium.onnx.json
│   ├── requirements.txt
│   ├── server.py
│   └── __pycache__
│       └── server.cpython-312.pyc
├── pyproject.toml
├── README.md
├── requirements-minimal.txt
├── requirements.txt
├── server.py
└── zipformer_stt
    ├── .gitattributes
    ├── app.py
    ├── config.json
    ├── decode.py
    ├── decoder-epoch-20-avg-10.int8.onnx
    ├── decoder-epoch-20-avg-10.onnx
    ├── Dockerfile
    ├── encoder-epoch-20-avg-10.int8.onnx
    ├── encoder-epoch-20-avg-10.onnx
    ├── examples.py
    ├── jit_script.pt
    ├── joiner-epoch-20-avg-10.int8.onnx
    ├── joiner-epoch-20-avg-10.onnx
    ├── model.py
    ├── README.md
    ├── requirements.txt
    └── sherpa_stt.py
```

## 🔍 Troubleshooting

### Common Issues

1. **TTS not working**
   - Check if Piper TTS server is running on port 5000
   - Confirm models have been downloaded

2. **STT errors**
   - Check model files in `zipformer_stt` directory
   - Confirm microphone has access permissions

3. **LLM not responding**
   - Check if Ollama service is running
   - Confirm qwen2.5:7b model has been downloaded

4. **WebRTC connection failed**
   - Add TURN server to configuration
   - Check firewall settings
   - Confirm ports are open

5. **Docker connection issues**
   - macOS/Windows: Configure TURN server
   - Linux: Use `--network=host` or expose port range

### Debug Mode

Run server with verbose logging:

```bash
python server.py --verbose --verbose
```

### Testing TURN Server

Test TURN server with tool:

```bash
turnutils_uclient -T -u your-username -w your-password your-turn-server.com
```

## 🚀 Production Deployment

### Production TURN Providers

- **Twilio**: Managed TURN service
- **Xirsys**: Global TURN/STUN infrastructure  
- **Amazon Chime**: AWS-based TURN servers

### Security Considerations

1. **HTTPS mandatory**: WebRTC requires HTTPS in production
2. **TURN authentication**: Always use strong credentials
3. **Rate limiting**: Limit connections to prevent abuse
4. **Network security**: Configure firewall rules properly

## 🤝 Contributing

1. Fork repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Create Pull Request

## 📄 License

This project is licensed under the BSD 2-Clause License - see LICENSE file for details.

## 🙏 Credits

- [Pipecat](https://github.com/pipecat-ai/pipecat) - Real-time multimodal AI pipeline framework
- [Sherpa-ONNX](https://github.com/k2-fsa/sherpa-onnx) - Speech recognition toolkit
- [Zipformer](https://huggingface.co/spaces/hynt/k2-automatic-speech-recognition-demo) - Speech recognition model
- [Piper TTS](https://github.com/rhasspy/piper) - Text-to-speech system
- [Ollama](https://github.com/ollama/ollama) - Local LLM inference
- [FastAPI](https://fastapi.tiangolo.com/) - Modern web framework
- [coturn](https://github.com/coturn/coturn) - TURN server implementation

## 📞 Support

If you encounter any issues, please create an issue on the repository.

---

💡 **Tip**: 
- For local testing on Linux, STUN is usually sufficient. For production or macOS/Windows, always configure TURN server to ensure stable connection.
- For more details, you can refer to [p2p-webrtc/docker](https://github.com/pipecat-ai/pipecat-examples/tree/main/p2p-webrtc/docker)


## 📝 Author's Notes
This project was built based on learning and practicing with the **Pipecat** library. 
- ⚠️ There may be some technical errors that have not been controlled.
- 🛠️ I greatly appreciate community feedback to improve further.
- ❤️ I hope this small project brings value to your learning process.

Thank you for your interest!
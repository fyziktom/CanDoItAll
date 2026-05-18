# Scope Inventory

| Area | In Scope | Out Of Scope |
| --- | --- | --- |
| Voice contracts | TTS/STT interfaces, request/result records, driver enum, exact factory | Provider plugin marketplace |
| OpenAI driver | REST transcription and speech synthesis | Realtime duplex sessions, custom voice creation |
| Settings | General voice settings, per-agent access/voice override | Per-user voice personalization |
| Chat | Normal agent chat, floating contextual/project-structure chat | Process dashboard embedded chat unless it uses the shared panel automatically |
| Cognitive Memory | Probe ask/answer audio and confirmation-gated correction feedback | Direct canonical memory write via voice |
| Tests | Unit/component tests and browser proof | Live OpenAI integration tests in CI |

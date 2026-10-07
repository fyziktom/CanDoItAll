# Agent voice HTTP API

The two voice routes reuse `IAgentVoiceService` and the workspace voice settings.
They perform transcription or speech synthesis without starting an agent execution.
When API authorization is enabled, both require `api.agents.execute` (or the
existing compatible `api` umbrella). Read/write agent scopes alone do not permit
voice calls. Keep bearer tokens and provider credentials in the calling server.

## Transcription

`POST /api/agents/voice/transcriptions` accepts `multipart/form-data` with either
one `file` part or 1–12 repeated `chunks` parts in transcription order. Each chunk
must be an independently decodable audio file; arbitrary fragments from one
encoded stream are not separate files. Mixing these fields, duplicate `file`
parts, extra fields and empty files are rejected before provider dispatch.

The combined audio limit is 25 MiB (26,214,400 bytes), with 64 KiB additional
request space for multipart headers. Extension and media type must match:

| Extension | Accepted media types |
| --- | --- |
| `.mp3`, `.mpga` | `audio/mpeg`, `audio/mp3` |
| `.mpeg` | `audio/mpeg`, `video/mpeg` |
| `.mp4` | `audio/mp4`, `video/mp4` |
| `.m4a` | `audio/mp4`, `audio/x-m4a` |
| `.wav` | `audio/wav`, `audio/x-wav`, `audio/wave` |
| `.webm` | `audio/webm`, `video/webm` |

Media-type parameters such as WebM's codec parameter are accepted. Client filenames
are replaced with generated names before provider dispatch. Format declarations
are validated here; the provider still validates whether the bytes decode.

A successful response is JSON with `text` and `model`. Ordered chunks are sent
sequentially and their trimmed transcripts joined by line breaks. Failure of a
later chunk returns an error rather than a partial transcript. Earlier chunks may
already have incurred provider charges. Let the user review and edit a transcript
before using it as an instruction; proper names may be transcribed inaccurately.

## Speech

`POST /api/agents/voice/speech` accepts a JSON body such as:

```json
{"text":"Welcome to this synthetic resort demonstration.","voiceId":"marin"}
```

`text` must be nonblank and at most 4,096 UTF-16 characters. The entire JSON request
is limited to 64 KiB. Optional `agentId` selects an existing non-template agent and
applies its voice permission and preferred voice. It does not execute that agent
or read its conversations. Without it the request uses workspace-level speech.
An explicit `voiceId` overrides the agent preference, then the workspace default.
Supported IDs are `alloy`, `ash`, `ballad`, `coral`, `echo`, `fable`, `nova`, `onyx`,
`sage`, `shimmer`, `verse`, `marin` and `cedar`.

The existing speech preprocessor may omit technical identifiers and include an
omission notice. The workspace chooses the speech model and output format. The
response contains raw audio with its playback content type, not a JSON/base64
wrapper. PCM output is wrapped as WAV by the existing voice driver. Disclose to
listeners that the voice is AI-generated.

## Configuration, failures and cancellation

Configure each operation under **Agents → Voice** or through the existing
workflow-settings API. A settings update replaces the document: read and preserve
the full document before changing its voice settings. Audio requires an enabled
native OpenAI profile with purpose Chat and a supported voice driver. Imported
shared providers and image-only profiles cannot provide these operations.

| Status / safe code | Meaning |
| --- | --- |
| 400 `agents.voice-input-invalid` | Invalid text, fields, audio size or declared format. |
| 401 / 403 | Missing/invalid authorization or missing execute authority. |
| 403 `agents.voice-access-denied` | The selected agent disallows voice. |
| 404 `agents.voice-agent-not-found` | The supplied agent is absent or a template. |
| 409 `agents.voice-disabled` | The requested workspace voice operation is disabled. |
| 409 `agents.voice-capability-unavailable` | The provider or driver does not support native voice. |
| 503 `agents.voice-provider-unavailable` | The configured provider is missing, disabled, failed or returned empty output. |

Framework rejection of request size, binding or content type can return 400, 413
or 415; callers must handle the HTTP status even when no voice-specific envelope
is available. Provider errors are mapped to safe messages. Logs contain operation,
safe code, optional agent ID, trace ID and exception type, without input text,
audio, tokens or provider diagnostics.

Cancellation propagates to the voice service and provider, and stops remaining
chunks. It cannot reverse work already accepted by a provider. There are no
automatic retries or alternate providers/models; offer an explicit retry after
the user or operator addresses the error.

Successful responses use `Cache-Control: no-store`. The routes create no durable
audio or transcript assets. ASP.NET Core multipart binding may buffer uploads in
temporary files for the request lifetime; these routes are not an upload archive.
Provider retention follows the configured provider's policies.

## Validation

`AgentVoiceTests` owns voice-service tests, and `AgentVoiceApiIntegrationTests`
owns HTTP scope, input, error, cancellation, body-limit and OpenAPI tests. The HTTP
fixture runs the real host and voice service with synthetic provider drivers; it
does not require external credentials. Before a release, also test a short
synthetic phrase against the configured provider in the built container and
record its source commit and OpenAPI hash.

# Protocol and API contract

## Two surfaces

### Native CanDoItAll catalog

`GET /api/shared-providers/v1/catalog`

Purpose:

- source identity and protocol version;
- sanitized list of published provider capabilities;
- import/sync metadata not representable in OpenAI `/models`;
- ETag/304.

Suggested response shape:

```json
{
  "schemaVersion": "1.0",
  "sourceInstanceId": "2d2db3c0-...",
  "catalogRevision": "sha256:...",
  "protocols": {
    "openAiCompatibleBasePath": "api/shared-providers/openai/v1"
  },
  "providers": [
    {
      "publicationId": "50f8b7d8-...",
      "revision": "sha256:...",
      "displayName": "Company OpenAI",
      "purpose": "chat",
      "defaultModelId": "sp.50f8...gpt5",
      "models": [
        {
          "id": "sp.50f8...gpt5",
          "displayName": "GPT model",
          "capabilities": [
            "chat-completions",
            "responses",
            "streaming",
            "function-tools",
            "structured-output"
          ]
        }
      ],
      "health": {
        "state": "available",
        "checkedAtUtc": "2026-08-24T00:00:00Z"
      }
    }
  ]
}
```

This is illustrative, not a generated schema. SB01 owns the exact repository records and JSON
names.

Catalog must omit:

- internal provider profile ID;
- connector private configuration;
- upstream base URI;
- secret ID/name/value/environment variable;
- internal notes;
- raw health error;
- private pricing/cost contracts unless explicitly approved;
- untested capabilities.

Headers:

- `ETag: "<canonical-public-hash>"`
- `Cache-Control: private, no-cache`
- `CanDoItAll-Request-Id`
- conditional `If-None-Match` returns `304` with no body.

### OpenAI-compatible inference

Routes:

- `GET /api/shared-providers/openai/v1/models`
- `POST /api/shared-providers/openai/v1/responses`
- `POST /api/shared-providers/openai/v1/chat/completions`
- `POST /api/shared-providers/openai/v1/images/generations`

The base URI stored by the client is the source/EGCP root; path joining must preserve a reverse
proxy base path.

## Compatibility scope

### Chat Completions v1

Required supported subset:

- `model`;
- `messages` with text and policy-approved image content;
- `stream`;
- function `tools`;
- `tool_choice`;
- `parallel_tool_calls` only when advertised;
- `response_format`/JSON schema when advertised;
- bounded generation parameters already supported by the upstream;
- stop/temperature/top-p/max output parameters when mapped and tested.

Denied by default:

- unknown properties;
- provider-controlled retrieval/built-in tool types;
- caller-controlled URLs outside allowed vision input policy;
- provider-side persistence fields;
- arbitrary user identity fields interpreted as access control;
- unsupported modalities.

### Responses v1

Required supported subset:

- `model`;
- bounded text/image input;
- instructions;
- `stream`;
- function tools and tool choice;
- text/JSON format;
- tested generation/reasoning parameters;
- tool-call output events needed by the local runtime.

Denied by default:

- `store=true`;
- `background=true`;
- provider-managed conversations;
- file IDs/uploads;
- web search, file search, MCP, computer use, code interpreter, hosted shell, or other
  server-side built-in tools;
- unknown `include` expansions;
- unsupported input/output modalities.

### Images v1

Required subset:

- `model`;
- prompt;
- supported size/quality/output format;
- bounded count;
- `b64_json` response;
- URL response only when it uses an existing authorized CanDoItAll artifact route with expiry
  and authorization, never an internal file path or upstream private URL;
- ComfyUI mapping through the existing image capability.

Audio routes are not part of v1 unless SB00 discovers real current production drivers and the
same bundle adds exact compatibility tests. Protocol records may reserve capability names but
must not register OpenAPI operations or advertise them.

## Routing model ID

The caller supplies only a public routing model ID from catalog/models. A codec/index resolves
it to:

- publication public ID;
- central provider profile;
- exact upstream model;
- purpose/operation support.

The ID must:

- be URL/JSON safe and bounded;
- be stable for the same publication/model;
- not expose internal profile ID;
- distinguish duplicate model names;
- fail closed on malformed/unknown IDs;
- not contain a caller-controlled upstream path.

## Errors

### Native catalog

Use current CanDoItAll structured API errors/Problem Details with stable codes such as:

- `shared-provider.catalog.unauthorized`
- `shared-provider.source.unavailable`
- `shared-provider.catalog.version-unsupported`

### OpenAI-compatible routes

Return:

```json
{
  "error": {
    "message": "Sanitized message",
    "type": "invalid_request_error",
    "param": "model",
    "code": "shared_provider_model_not_found"
  }
}
```

Map internal categories consistently:

- validation -> 400;
- missing publication/model -> 404;
- unauthorized -> 401;
- insufficient scope -> 403;
- conflict/unavailable state -> 409 or 503 as contract defines;
- upstream rate limit -> 429 with safe `Retry-After`;
- upstream failure -> 502;
- timeout -> 504;
- cancelled client -> server cancellation, not a fabricated success.

Do not return raw upstream body or central stack trace.

## Access context and tracing

Optional request header:

`CanDoItAll-Access-Context-Ref: <opaque-reference>`

It is common to catalog/inference and future APIs through middleware/accessor, not repeated in
each DTO. Preserve `traceparent`/`tracestate` using normal .NET diagnostics. Do not automatically
forward access context as W3C baggage to external providers.

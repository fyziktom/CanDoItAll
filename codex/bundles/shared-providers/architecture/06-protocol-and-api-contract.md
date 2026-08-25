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
      "transport": "openai-compatible",
      "defaultModelId": "sp1.50f8b7d8....<base64url-sha256>",
      "models": [
        {
          "id": "sp1.50f8b7d8....<base64url-sha256>",
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
        "state": "available"
      }
    }
  ]
}
```

This is illustrative, not a generated schema. SB01 owns the exact repository records and JSON
names. Its frozen v1 record is strict and case-sensitive, rejects unknown or duplicate
properties, requires all constructor parameters, and uses the exact enum strings defined by
`SharedProviderProtocolJson`. Provider, model, and capability arrays are recursively copied and
canonicalized before serialization. Strong publication/catalog revisions are computed from the
sanitized canonical representation: advertised health state participates, while revision fields
and volatile check timestamps do not.

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

The frozen function-tool shape is
`{"type":"function","function":{"name", "description"?, "parameters"?, "strict"?}}`.
A named `tool_choice` nests its name under `function` and must match a declared tool. Chat
structured output uses nested `response_format.json_schema`. A vision part is
`{"type":"image_url","image_url":{"url":"data:...","detail"?}}`, is allowed only on a
user message, and accepts `detail` values `auto`, `low`, or `high`.

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

The frozen Responses function-tool shape is flattened:
`{"type":"function","name", "description"?, "parameters"?, "strict"?}`. A named
`tool_choice` is likewise flattened and must match a declared tool. Structured output is under
flattened `text.format`; text/image discriminators are `input_text` and `input_image`, and an
image part is `{"type":"input_image","image_url":"data:..."}`. Chat shapes are rejected on
this surface and Responses shapes are rejected on Chat.

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
- ComfyUI mapping through the existing image capability.

The implemented v1 Images contract is Base64-only. `n` may be absent (default 1) or an explicit
JSON integer within the selected descriptor maximum; null, strings, fractions, overflow, zero,
and out-of-range values fail closed. No artifact-URL response mode is implemented or advertised.

For both text surfaces, function description is bounded and type-checked, `strict` is Boolean,
and the optional schema must have the exact surface-specific shape. Merely supplying
`parallel_tool_calls` on Chat, including `false`, requires advertised parallel-tools support.
Vision data URIs must contain nonempty whitespace-free valid Base64 and be PNG, JPEG, or WebP.
The five production rows advertise no vision input; vision acceptance is policy proof only, not
a production capability claim.

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

SB01 freezes the exact format as
`sp1.<publication-guid-N-lowercase>.<base64url-full-SHA256>`. The digest input is the exact,
validated UTF-8 upstream model token; callers cannot trim, normalize, or recover that token from
the public ID. The complete ID is exactly 80 ASCII characters.

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

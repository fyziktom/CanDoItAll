# Standards research

Primary sources consulted during preparation and revalidated on 2026-08-24. SB11 must
re-check the generated product contract against the same primary sources before freezing the
OpenAPI artifact.

## OpenAI

- Responses create: `https://developers.openai.com/api/reference/cli/resources/responses/methods/create`
- Chat Completions: `https://developers.openai.com/api/reference/resources/chat`
- Models: `https://developers.openai.com/api/reference/resources/models`
- Images: `https://developers.openai.com/api/reference/resources/images`

Architectural use:

- route and envelope compatibility;
- streaming event semantics;
- function tool and structured output wire shapes;
- image request/response fields;
- error envelope expectations.

The current Responses contract accepts text, image, or file input, supports streaming,
structured JSON, function/custom tools, and provider-hosted tools. The current Chat
Completions contract remains a separate message/choice surface. Models and Images remain
separate resources. The bundle does not claim those complete surfaces: it implements a tested
allowlist and rejects every unsupported field or tool category explicitly.

## Ollama

- OpenAI compatibility: `https://docs.ollama.com/api/openai-compatibility`

Architectural use:

- Ollama documents compatibility with only parts of the OpenAI API.
- Both `/v1/chat/completions` and `/v1/responses` are documented, and the OpenAI client still
  requires an API-key value even though Ollama ignores it.
- Compatibility remains partial, so capability advertisement must be adapter- and test-driven.

## HTTP

- RFC 9110: `https://www.rfc-editor.org/rfc/rfc9110`
  - entity tags and `If-None-Match` provide the conditional-request basis for catalog
    synchronization;
- RFC 9457: `https://www.rfc-editor.org/rfc/rfc9457`
  - defines `application/problem+json`, obsoletes RFC 7807, and requires the actual HTTP status
    to remain authoritative when a `status` member is present;
- RFC 6648: `https://www.rfc-editor.org/rfc/rfc6648`
  - deprecates new `X-` conventions, supporting `CanDoItAll-Access-Context-Ref` rather than an
    `X-` prefixed business header.

## Distributed tracing

- W3C Trace Context: `https://www.w3.org/TR/trace-context/`
- W3C Baggage: `https://www.w3.org/TR/baggage/`

Architectural use:

- preserve standard tracing;
- keep access-object correlation separate;
- do not leak business context through baggage to the upstream provider.

`traceparent`/`tracestate` remain trace propagation, while `baggage` is application-defined
context with different privacy implications. The access-context reference is not authorization
and is not forwarded upstream as baggage.

## Inference

A native catalog plus OpenAI-compatible inference is the least-inventive truthful design:
OpenAI `/models` is useful for SDKs but cannot represent source/import/publication state. The
CanDoItAll catalog fills that gap without replacing standard inference routes.

## SB00 decision

The compatibility surface is frozen as an intersection, not a proxy promise:

1. the public route and documented wire subset;
2. the central relay adapter descriptor;
3. the selected publication capability snapshot;
4. the selected upstream connector's proven behavior;
5. passing positive and adversarial contract tests.

Unknown fields, unsupported built-in tools, caller-selected upstream addresses, and
publication/model cross-routing are failures. They are never silently dropped or forwarded.

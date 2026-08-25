# SB00 standards revalidation

Revalidated: 2026-08-24  
Product behavior changed by this artifact: **No**

## Primary sources

- OpenAI Responses create:
  `https://developers.openai.com/api/reference/cli/resources/responses/methods/create`
- OpenAI Chat Completions:
  `https://developers.openai.com/api/reference/resources/chat`
- OpenAI Models: `https://developers.openai.com/api/reference/resources/models`
- OpenAI Images: `https://developers.openai.com/api/reference/resources/images`
- Ollama OpenAI compatibility: `https://docs.ollama.com/api/openai-compatibility`
- W3C Trace Context: `https://www.w3.org/TR/trace-context/`
- W3C Baggage: `https://www.w3.org/TR/baggage/`
- RFC 9457 Problem Details: `https://www.rfc-editor.org/rfc/rfc9457`
- RFC 6648 deprecation of `X-` prefixes: `https://www.rfc-editor.org/rfc/rfc6648`
- RFC 9110 HTTP Semantics: `https://www.rfc-editor.org/rfc/rfc9110`

## Frozen interpretation

1. Responses, Chat Completions, Models, and Images are distinct OpenAI resources. Responses
   supports streaming, structured output, function/custom tools, and hosted tools, but this
   bundle claims only its explicit tested subset.
2. Ollama documents compatibility with parts of both `/v1/chat/completions` and
   `/v1/responses`; partial compatibility must be proven per adapter and publication.
3. Native catalog errors follow the repository's current `ApiErrorResponse` convention. The
   OpenAI-compatible routes need their own OpenAI-shaped error boundary, including 401/403.
4. Catalog ETags and `If-None-Match` follow RFC 9110 conditional-request semantics.
5. `CanDoItAll-Access-Context-Ref` is a bounded opaque correlation reference. It is neither
   authorization nor W3C baggage, and it is never forwarded upstream as business context.
6. Unknown fields, built-in tools outside the allowlist, caller-selected upstream addresses,
   and cross-publication model IDs fail explicitly. Nothing is silently ignored or blindly
   proxied.

The detailed rationale is also recorded in `bundle://evidence/01-standards-research.md`.


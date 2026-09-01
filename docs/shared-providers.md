# Shared providers

A publisher exposes selected models from a local provider profile. A consumer registers that publisher as a source, synchronizes its catalog, and imports a publication. The import remains linked to its source and publication identity; it is not an independent copy of the publisher's credentials.

## Configure and operate

1. Configure and test the publisher's local provider and secret through provider administration. Only eligible connector/capability combinations can be published.
2. Publish the desired provider/model selection. Use the catalog's opaque routing model IDs when invoking it; do not substitute the publisher's private upstream model name.
3. Issue an appropriately scoped managed API token on the publisher. Store that token as a source secret on the consumer.
4. Add the source base URI, synchronize its catalog, and import the desired publication. Select the resulting shared provider in the normal provider/model picker.
5. Refresh after publication, model, capability, or pricing changes. An unavailable source or publication leaves the import visible but operationally disabled; it must not silently route to an unrelated local provider.

Local and shared providers can coexist. Shared model selection remains constrained to the imported publication, including models intentionally omitted from suggestions. Deletion checks protect source/import/secret references; resolve reported references before deleting an entity.

## HTTP surface

| Method | Path | Required scope |
| --- | --- | --- |
| GET | `/api/shared-providers/v1/catalog` | `api.shared-providers.catalog.read` |
| GET | `/api/shared-providers/openai/v1/models` | `api.shared-providers.catalog.read` |
| POST | `/api/shared-providers/openai/v1/chat/completions` | `api.shared-providers.invoke` |
| POST | `/api/shared-providers/openai/v1/responses` | `api.shared-providers.invoke` |
| POST | `/api/shared-providers/openai/v1/images/generations` | `api.shared-providers.invoke` |

Use `/api/shared-providers/openai/v1` as an OpenAI-compatible client endpoint. The catalog and model listing do not grant invocation authority. Source/import/publication administration and request-history services do not add remote administration or history HTTP routes.

The native catalog includes versioned publication identities, revisions, availability, supported operations, model thinking capabilities and public pricing. ETags permit conditional retrieval. Internal profile IDs, upstream credentials, private endpoint addresses and raw diagnostics are not public catalog fields.

The OpenAI compatibility surface is a bounded subset. Chat accepts supported messages, function tools, structured output, sampling/token controls, `reasoning_effort`, and `stream_options.include_usage`. Responses is stateless: use `store:false` and `background:false`; supported input items, function tools and reasoning controls are validated against the publication. Stored-response continuation and arbitrary provider-specific fields are not supported. Image generation returns bounded base64 data, not remote image URLs. Consult the target host's [OpenAPI document](api-control-plane.md), since advertised capabilities and per-provider limits can be narrower.

## Network and credential policy

HTTPS public destinations are the default. Plain HTTP loopback (`localhost`, `127.0.0.1`, `[::1]`) is allowed for local development without granting general private-network access. Non-loopback HTTP and private-network access require explicit source approval and remain subject to destination restrictions. Discovery and imported runtime selection share the same URI policy. Destination/DNS checks and redirect restrictions remain enforced by the selected transport; approving one source is not a global network bypass.

Managed source tokens are resolved through their typed secret binding. A missing or invalid binding fails explicitly. Keep the token out of URLs, prompts, logs, screenshots and exported configuration. Use [secure configuration](secure-configuration.md) for production secret storage.

## Correlation, usage and failures

`CanDoItAll-Access-Context-Ref` and `CanDoItAll-Access-Context-Type` are optional opaque audit headers. The type requires a reference. They do not authorize requests and are not forwarded upstream. `CanDoItAll-Request-Id` identifies the publisher's invocation. Managed credential identity is server-derived; a client-supplied identity header cannot impersonate another token.

Usage remains unavailable or partial when the provider omits evidence. A configured zero tariff means free; a missing tariff means unpriced. Execution freezes price provenance; later catalog edits do not rewrite historical prices.

Invalid requests return sanitized errors. Native catalog errors and OpenAI inference errors use their respective envelopes. Upstream rate limits remain 429 with a safe optional Retry-After; timeouts remain 504 before headers. Oversized or unreadable diagnostic bodies do not change those categories. A buffered Responses envelope must be completed and contain no non-null error.

After streaming headers have started, an upstream failure cannot change HTTP 200. The connection is aborted instead of ending successfully; consumers must observe transport failure and must not treat partial text as a completed answer. No synthetic success marker or raw upstream error is emitted. Caller cancellation disposes the stream and preserves already-observed terminal evidence.

Generic AI-provider database transfer is blocked when either database contains publication/import references, or when it would replace a secret used by a target shared source. It is not a complete sharing-state migration. Do not delete references or bypass the guard to force a transfer; use a reviewed backup/restore plan that preserves database and vault identities together.

See [request history](provider-request-history.md), [pricing](provider-capability-and-pricing.md), and [backup and restore](operations/backup-and-restore.md).

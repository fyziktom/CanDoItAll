# SB01 security and wire constraints

## Contract ownership

`CanDoItAll.SharedProviders.Abstractions` owns immutable SDK-neutral protocol records, public
identities, routing IDs, typed failure categories, and catalog/inference ports. It must contain
no `HttpContext`, EF entity, `ProviderProfile`, secret type, provider SDK type, authentication
principal, or concrete HTTP client. Web owns header binding and the request-scoped mutable state.

The generic `security-best-practices` skill has no C# or ASP.NET reference and therefore supplies
no framework-specific rule for this subbundle. These constraints use the repository security
architecture plus the ASP.NET Core program/pipeline, Minimal API, identity, and integration-test
guidance loaded for SB01.

## Access-context wire contract

| Case | Required behavior |
| --- | --- |
| Header absent | Valid; scoped accessor exposes `null`. |
| One value | Accept only the exact, untrimmed token when it is 1..256 ASCII characters from `[A-Za-z0-9._~:-]`. |
| Repeated identical values | Return native `ApiErrorResponse` with HTTP 400 before endpoint dispatch. The header must have exactly one value. |
| Repeated conflicting values | Return native `ApiErrorResponse` with HTTP 400 before endpoint dispatch. The header must have exactly one value. |
| Empty, whitespace, comma-combined, non-ASCII, control, or oversized value | Return native `ApiErrorResponse` with HTTP 400. Do not trim or decode it into validity. |
| Valid value | Bind once to a scoped accessor; concurrent requests must not observe each other's value. |

The header name is exactly `CanDoItAll-Access-Context-Ref`. It is an opaque business correlation
reference: no JSON/JWT parsing, no identity extraction, and ordinal comparison only. It never
satisfies authentication, an authorization policy, a scope, or tenant selection. JWT bearer
authentication remains the caller authority. Authentication/authorization may reject a request
before this optional metadata is bound; the middleware still validates it before application
endpoint dispatch.

Use constructor/scoped injection for `IAccessContextReferenceAccessor`. The contract project must
not depend on `IHttpContextAccessor`; no static or `AsyncLocal` fallback is allowed. Register the
state and accessor in `AddCanDoItAllApi`, and add the middleware to both production `Program.cs`
and `ApiTestHost` in the same relative pipeline position. Binding is single-assignment per scope.

## Tracing and hop boundaries

- Keep `traceparent` and `tracestate` under normal .NET `Activity` propagation.
- Do not copy the access reference into W3C baggage.
- Do not copy it into `Authorization`, claims, cookies, request DTOs, or upstream provider
  headers.
- A later shared-source client may forward it only to the configured central/EGCP hop through an
  explicit allowlist; SB01 performs no outbound forwarding.
- Logs and errors must not include tokens, secrets, prompts, tool payloads, raw upstream errors,
  or private endpoint URIs. The opaque reference may be recorded only as the explicitly approved
  metadata field, never concatenated into an authorization decision.

## Public JSON allowlist

Every public protocol record uses a stable explicit JSON name and an immutable bounded value.
Enum-like wire values require explicit stable strings and must fail on unknown input. The
sanitized catalog representation may contain public publication/source identities, schema and
catalog revisions, display metadata, advertised operations/capabilities, public routing model
IDs, and sanitized availability. It must not serialize:

- internal provider profile IDs or EF concurrency values;
- connector-private configuration or upstream/base URIs;
- secret IDs, names, values, environment-variable names, or authorization headers;
- raw health errors, internal notes, prompts, messages, tool arguments/results, or attachments;
- untested capabilities or private pricing contracts.

The protocol version is exactly `1.0`. Unknown/unsupported versions fail explicitly. Native
routes are stable repository constants. Joining a source base URI to a route must preserve a
reverse-proxy base path, remove query/fragment state, and never let a caller-supplied model ID
become an upstream path.

## Routing identity constraints

The routing format is versioned and bounded:

`sp1.<publication-guid-N-lowercase>.<base64url-full-SHA256>`

The hash input is UTF-8 for the validated canonical upstream model token. A full 256-bit digest
is retained. The public ID includes only the public publication identity and model fingerprint;
it contains no internal profile ID, upstream URI, credential, or reversible model text. Parsing
is structural and fails closed. Resolution to an exact model is through the server's sanitized
catalog/index, and cross-publication matches must fail.

## Required proof

- serialization snapshots prove exact names, deterministic round-trip, unknown-version failure,
  and absence of forbidden fields;
- routing vectors prove stability, duplicate-name separation, malformed rejection, full digest,
  cross-publication rejection, and no internal identifier leakage;
- integration tests prove absent/valid/malformed/oversized/conflicting header behavior, scoped
  isolation across concurrent requests, and that a forged access reference grants no auth scope;
- a source/package scan proves Abstractions has no forbidden namespaces or package references;
- a credential/redaction scan covers source plus captured transcripts.

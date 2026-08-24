# Security, access context, and future EGCP

## Authentication and scopes

Central API remains protected by the current JWT Bearer mechanism.

Granular scopes:

- `api.shared-providers.catalog.read`
- `api.shared-providers.invoke`

Current umbrella `api` compatibility may satisfy both only if that is the repository-wide
policy. Tests must prove missing/expired/malformed token and wrong scope.

The API token subject is the authenticated caller. It is never replaced by access context.

## Access context

Header:

`CanDoItAll-Access-Context-Ref`

Suggested value object constraints:

- absent is valid in v1;
- 1 to 256 ASCII characters when present;
- allow only a conservative opaque set such as letters, digits, `.`, `_`, `~`, `:`, `-`;
- no whitespace/control characters;
- no JSON/JWT decoding;
- no assumption that it contains user/project/session identity;
- compare/log ordinally;
- error on malformed value before dispatch.

Exact grammar may be adjusted after current header conventions are inspected, but it must
remain opaque and bounded.

## Cross-cutting placement

Preferred:

- `AccessContextReference` and accessor interface in a tiny existing lower-level shared
  contract location, likely SharedKernel;
- Web scoped state/middleware reads the header;
- local outbound shared-source handler reads the accessor and forwards to central/EGCP;
- central invocation service records it;
- upstream adapter header allowlist excludes it.

Do not make every shared-provider request DTO carry this field. Do not store a mutable static
or use `HttpContextAccessor` inside inner domain types.

## W3C tracing

Use normal .NET Activity propagation for `traceparent`/`tracestate`. Access context is a
business correlation reference and remains independent.

Do not put full access-object data into W3C baggage. Baggage propagates broadly and could reach
the upstream provider. If future EGCP uses baggage internally, it must have an explicit
hop-boundary stripping policy.

## Future EGCP compatibility

A future gateway may:

- terminate client auth;
- map client identity/session/project to an internal access object;
- mint or replace `CanDoItAll-Access-Context-Ref`;
- enforce provider/publication/model policy;
- route to central CanDoItAll;
- aggregate usage and cost.

The current protocol remains compatible because:

- source is an arbitrary trusted base URI;
- paths and catalog contract are stable;
- auth and access context are separate;
- routing model IDs are opaque;
- central does not require user fields in every DTO;
- central audit can correlate the reference without understanding its payload.

## SSRF and trust policy

Client source is administrator configured but still an outbound request target. Implement:

- canonical URI parser;
- explicit allowed scheme;
- DNS/IP policy;
- redirect disabled or destination-revalidated;
- connection-time IP validation;
- no embedded credentials;
- no global TLS bypass;
- bounded response size before JSON parsing;
- content type check;
- source identity pinning.

Central upstream target is not caller controlled. It comes only from a stored provider profile
and connector adapter. Existing upstream profiles may intentionally use private endpoints; the
central adapter uses the provider administration trust boundary, not client-supplied URL.

## Redaction and audit

Invocation record and logs may contain:

- request ID;
- trace ID;
- token subject;
- opaque access reference;
- publication ID;
- public/upstream model;
- operation;
- timestamps/outcome/status;
- usage/cost completeness.

They must not contain:

- Authorization;
- secret ID/name/value;
- prompt/messages/instructions;
- tool definitions/arguments/results;
- attachments/images/audio;
- response text;
- raw upstream error body;
- private endpoint URI;
- source catalog token;
- cookies.

Add serialization/log scans and deterministic upstream capture proof.

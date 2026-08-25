# SB01 security and trust-boundary proof

## Boundary result

`CanDoItAll.SharedProviders.Abstractions` contains only SDK-neutral public contracts. Direct
project/source inspection and focused tests establish that it has no package/project reference
and no dependency on ASP.NET Core, EF, Workspace, Web, AgentFramework runtime, provider SDKs,
secret services, `HttpContext`, or authentication principals. Web owns only validation and
request-scoped binding.

## Access-context result

The exact header is `CanDoItAll-Access-Context-Ref`. It is optional opaque metadata, is never
parsed as JWT/JSON/identity, never copied to claims or W3C baggage, and cannot grant
authentication, authorization, scope, or tenant access. SB01 has no outbound provider/source
transport implementation, so the value cannot be forwarded beyond the Web request. Repeated or
comma-combined headers are rejected even when values are identical.

## Privacy and content result

Catalog serialization is an allowlist. It exposes public identities, display metadata, purpose,
transport, routing IDs, capabilities, sanitized health state, and strong public revisions. It
cannot expose internal provider IDs, secret IDs/names/values, environment names, private base
URIs, connector configuration, raw health errors, notes, prompts/messages, tool arguments/results,
attachments, or volatile check timestamps.

Routing IDs retain the full SHA-256 digest of the exact model token and the public publication ID
only. The codec does not make the upstream model reversible and does not accept an upstream URI
or route.

## Scans

- `sb01-forbidden-dependency-scan.txt`: no forbidden project, package, namespace, framework,
  ambient service-locator, or dynamic/reflection boundary.
- `sb01-access-boundary-scan.txt`: no auth/claims/baggage/outbound-header/`IHttpContextAccessor`
  coupling.
- `sb01-secret-scan.txt`: no credential-shaped token or private-key material in SB01 source, tests,
  or governed evidence.
- `sb01-anti-stub-audit.txt`: all selected implementation/test files contain executable behavior;
  no placeholder or fabricated proof surface.

All scans pass. Captured transcripts contain build/test metadata and sanitized deterministic test
values only; no live provider, paid service, or external network was used.

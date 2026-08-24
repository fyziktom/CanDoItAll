# Security and non-functional requirements

## Security

- **NFR-001 Secret containment:** upstream secret values remain central. Catalog, errors, logs,
  OpenAPI examples, proof artifacts, and client persistence must not reveal them.
- **NFR-002 Least privilege:** separate catalog-read and invoke scopes; preserve the existing
  umbrella `api` scope convention only where current authorization policy requires it.
- **NFR-003 Authentication separation:** access context is not an authentication claim.
- **NFR-004 SSRF resistance:** source URIs reject userinfo/query/fragment, use explicit
  scheme/network policy, revalidate redirects or disable them, and prevent DNS rebinding.
- **NFR-005 TLS:** HTTPS is the default. Loopback/private HTTP requires an explicit development
  or trusted-network policy; never disable certificate validation globally.
- **NFR-006 Payload limits:** enforce bounded JSON, message/tool count, attachment/image size,
  output count, and timeout limits before upstream dispatch.
- **NFR-007 Feature denylist:** provider-side storage, background jobs, built-in remote tools,
  MCP, web/file search, code interpreter, computer use, and arbitrary provider URLs are denied
  by default.
- **NFR-008 Logging:** structured metadata only; redact Authorization, API keys, cookies,
  source secrets, upstream endpoints where private, request/response bodies, and binary data.
- **NFR-009 Error safety:** do not reflect raw upstream payloads or stack traces to callers.
- **NFR-010 Revocation:** unpublish, disable, token revocation/expiry, source disable, and
  permission failure take effect without requiring client reinstallation.
- **NFR-011 No open proxy:** callers select only published routing IDs; no caller-controlled
  upstream URI, header, or secret.
- **NFR-012 Concurrency:** use optimistic concurrency for publication/source/import updates and
  deterministic conflict responses.
- **NFR-013 Audit:** invocation metadata has retention/cleanup policy and no content.
- **NFR-014 Supply chain:** do not add a new external relay/proxy package when .NET HTTP and
  existing SDK-neutral infrastructure are sufficient.

## Architecture quality

- **NFR-015 Dependency direction:** inner MAF/provider contracts never reference Workspace,
  Web, UI, EF, or HTTP implementation projects.
- **NFR-016 Wire isolation:** public HTTP DTOs are repository-owned and SDK-free; internal
  provider profiles never cross the wire.
- **NFR-017 Implementation isolation:** provider-specific HTTP/protocol details stay in an
  integration implementation behind a registry/factory.
- **NFR-018 One source of truth:** Workspace EF provider data remains canonical; AgentFramework
  catalog is a runtime projection.
- **NFR-019 Relational model:** publication, source, and import identities are explicit
  entities/relationships, not only JSON blobs.
- **NFR-020 No partial sprawl:** add cohesive top-level files; do not grow a large runtime
  partial or the existing `WorkspaceModels.cs` monolith.
- **NFR-021 Extension path:** a future upstream connector adds one adapter/registration and
  focused tests, not a switch in every runtime.
- **NFR-022 Compatibility honesty:** capability advertisement is generated from tested adapter
  support, not user-editable booleans alone.

## Reliability and performance

- **NFR-023 Streaming:** response headers are sent promptly; streaming does not buffer the full
  result.
- **NFR-024 Cancellation:** downstream cancellation closes upstream work promptly.
- **NFR-025 Timeouts:** connect, overall, and streaming-idle timeouts are explicit and distinct.
- **NFR-026 Backpressure:** bounded buffers and no unbounded in-memory transcript/image
  accumulation.
- **NFR-027 Catalog efficiency:** ETag/304 and deterministic representation.
- **NFR-028 Reconciliation:** transactions preserve local ID and source/import/profile
  consistency.
- **NFR-029 Outage behavior:** clear transient failures and retry hints; no destructive sync on
  temporary failure.
- **NFR-030 Scale:** no per-request full catalog scan after routing caches are warmed; cache
  invalidation is tied to publication/profile changes.
- **NFR-031 Multi-host correctness:** no reliance on static process memory for publication,
  import, or audit identity.
- **NFR-032 Portable tooling:** E2E and validation work on Windows/Linux hosts with PowerShell 7
  or Python and Docker Compose.

## Test economy

- **NFR-033 Focused tests:** exact affected builds and filters per subbundle.
- **NFR-034 Broad gate once:** stable aggregate only at final frozen checkpoint.
- **NFR-035 Image reuse:** build one app image and reuse it across three app containers.
- **NFR-036 Determinism:** no paid/live provider in automated proof.
- **NFR-037 Durable proof:** commands, discovery counts, hashes, and artifacts are recorded.

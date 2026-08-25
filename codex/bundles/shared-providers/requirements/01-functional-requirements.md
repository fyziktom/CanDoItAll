# Functional requirements

Identifiers are stable and used by the traceability matrix.

## Publication and catalog

- **FR-001** A local provider is not shared by default.
- **FR-002** Sharing requires an explicit central administrator action.
- **FR-003** Publication uses a stable public identifier distinct from the internal
  `ProviderProfile.Id`.
- **FR-004** Only enabled, valid, supported production connectors can be published.
- **FR-005** Synthetic scenario/process mocks and runtime fallback profiles are not publishable
  in production.
- **FR-006** The catalog contains only sanitized public fields.
- **FR-007** The catalog describes purpose, protocol, models, and tested capabilities.
- **FR-008** Catalog representation has a schema version, stable source-instance identity,
  public revision, ETag, and `If-None-Match`/`304` support.
- **FR-009** Unpublishing removes the item from discovery and prevents new invocation.
- **FR-010** Publication does not expose upstream credentials or upstream private endpoint data.

## Standard-compatible inference

- **FR-011** Expose `GET /api/shared-providers/openai/v1/models`.
- **FR-012** Expose a bounded `POST /api/shared-providers/openai/v1/responses`.
- **FR-013** Expose a bounded `POST /api/shared-providers/openai/v1/chat/completions`.
- **FR-014** Support streaming SSE without buffering the complete response.
- **FR-015** Preserve function-tool definitions and tool calls for client-side tool execution.
- **FR-016** Support structured output only when both relay and upstream publication advertise
  it.
- **FR-017** Support vision inputs only when explicitly advertised and policy-allowed.
- **FR-018** Expose `POST /api/shared-providers/openai/v1/images/generations` for publishable
  OpenAI image and ComfyUI image profiles.
- **FR-019** Do not advertise or expose audio operations until a real production driver and
  contract tests exist.
- **FR-020** Resolve public routing model IDs to one publication and one upstream model.
- **FR-021** Two publications exposing the same upstream model name remain unambiguous.
- **FR-022** Unsupported fields or capabilities fail explicitly; they are not silently ignored
  or forwarded.
- **FR-023** Preserve cancellation and client disconnect behavior.
- **FR-024** Return an OpenAI-compatible error envelope on OpenAI-compatible routes and existing
  CanDoItAll API errors on native routes.

## Client source and imports

- **FR-025** A shared source owns one normalized base URI, one credential reference, network
  policy, status, remote instance identity, and last ETag.
- **FR-026** A source can be tested before import.
- **FR-027** Catalog sync uses conditional GET.
- **FR-028** User selection creates or reactivates a stable local provider profile.
- **FR-029** Unique `(source, remote publication)` identity prevents duplicates.
- **FR-030** Repeated sync is idempotent and preserves local provider ID and agent bindings.
- **FR-031** Local alias and local enabled intent survive remote display-name changes.
- **FR-032** Remote-owned purpose, protocol, model routing, and capability fields are read-only.
- **FR-033** Unpublished or removed remote profiles become unavailable without destructive
  deletion.
- **FR-034** Reappearance reconciles the existing import.
- **FR-035** A remote source-instance identity change is a trust mismatch, not silent remapping.
- **FR-036** A source outage produces an explicit unavailable error and never silently selects a
  personal provider.
- **FR-037** Personal and shared provider profiles coexist in the same provider catalog.
- **FR-038** De-selection retires or disables the import safely; it does not break referenced
  profiles through unconditional hard deletion.
- **FR-039** Source endpoint or secret-reference changes propagate consistently to all imports.
- **FR-040** Local source credentials are stored in the existing secret system, not plaintext.

## Runtime use

- **FR-041** Imported text/image profiles use a `provider.candoitall-shared` connector identity.
- **FR-042** Runtime projection materializes the existing OpenAI-compatible MAF path rather than
  introducing a second complete agent runtime.
- **FR-043** Shared profiles work through the same provider selection surfaces as local profiles.
- **FR-044** Health and availability distinguish source connectivity, authorization, remote
  publication state, and upstream health.
- **FR-045** Legacy Workspace provider execution is either a thin compatible facade over the
  canonical path or explicitly out of scope; it is not a duplicate implementation.

## Access context, usage, and audit

- **FR-046** Accept the optional `CanDoItAll-Access-Context-Ref` request header.
- **FR-047** Validate it as a bounded opaque value; do not parse identity/project/session fields.
- **FR-048** Propagate it from local app to central or future EGCP but never to the real upstream
  provider.
- **FR-049** Preserve W3C trace context independently.
- **FR-050** Record authenticated subject, opaque access reference, publication, model,
  operation, timing, outcome, and available usage/cost metadata.
- **FR-051** Never record prompts, responses, images, attachments, tool arguments, or secrets in
  shared-provider invocation audit.
- **FR-052** Reuse the existing provider usage direction; do not create a competing cost ledger.
- **FR-053** Missing usage is represented as unavailable/incomplete, never fabricated as zero.

## UI and operations

- **FR-054** Central profiles show publication state and ineligibility reason.
- **FR-055** Client UI supports source CRUD, connection test, catalog load, multi-select import,
  sync, and status.
- **FR-056** Imported profiles are visually distinct and remote-owned fields are read-only.
- **FR-057** UI is optimized for CanDoItAll's supported large-screen desktop layout.
- **FR-058** The E2E stack starts one central and two independent client applications.
- **FR-059** The final stack remains running for manual testing.
- **FR-060** OpenAPI and SharedInfo skills reflect the final route and schema surface.
- **FR-061** Product documentation and the operator runbook cover architecture, central and
  client setup, the tested compatibility subset, security boundaries, troubleshooting,
  repeatable three-instance tooling, and deliberate cleanup.

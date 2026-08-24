# C# pattern selection records

Required by the C# architecture bundle guard.

## ADR-001: Publication as a separate entity

**Decision:** use `ProviderSharePublication` as a one-to-one Workspace-owned entity over a
provider profile.

**Why:** sharing is an explicit security boundary and needs a public ID, publication state,
timestamps, concurrency, and future policy without exposing internal provider ID.

**Rejected:** `ProviderProfile.IsShared` alone. It is initially simpler but couples public
identity/policy to the core profile and invites exposing the internal ID.

## ADR-002: Source and import are separate entities

**Decision:** `SharedProviderSource` owns endpoint and credential reference;
`SharedProviderImport` owns remote publication relationship and local provider profile link.

**Why:** one source credential serves many imports, sync is idempotent, and remote state can
change without destroying local profile identity.

**Rejected:** one copied base URL/API secret per imported profile; JSON-only metadata.

## ADR-003: Native catalog plus bounded OpenAI-compatible inference

**Decision:** use a CanDoItAll versioned catalog for discovery and an OpenAI-compatible subset
for inference.

**Why:** `/v1/models` cannot express source/import/publication ownership and capability detail,
while existing local runtimes already understand OpenAI-style inference.

**Rejected:** inventing a complete custom inference API; pretending the whole OpenAI API is
supported.

## ADR-004: Shared connector projects to existing OpenAI runtime

**Decision:** persist connector identity `provider.candoitall-shared`, but materialize an
effective AgentFramework OpenAI-compatible profile.

**Why:** ordinary agent creation already branches by provider kind; a new Shared kind would
spread switches and duplicate SDK behavior.

**Rejected:** `ProviderKind.Shared` across all MAF paths; a second shared-agent runtime.

## ADR-005: Relay adapter registry

**Decision:** central upstream adaptation is registered by connector and operation capability.

**Why:** OpenAI/Ollama can relay compatible text/image HTTP while ComfyUI needs an image
mapping. Future providers add an adapter, descriptor, and tests.

**Rejected:** endpoint switch over every connector; blind arbitrary reverse proxy.

## ADR-006: Allowlisted compatibility subset

**Decision:** parse, validate, and relay only documented fields/features for each surface.

**Why:** blindly forwarding standard fields can enable provider storage, background execution,
built-in tools, remote file identifiers, and unbounded cost/data paths.

**Rejected:** raw body proxy; silently dropping unknown fields.

## ADR-007: Opaque access-context header

**Decision:** `CanDoItAll-Access-Context-Ref` is a bounded opaque header and scoped context.

**Why:** future EGCP can associate user/session/project details externally without expanding
every DTO. The name avoids deprecated `X-` conventions.

**Rejected:** many optional fields on every request; treating W3C baggage as the business
identity; trusting the reference for authorization.

## ADR-008: Public representation hash

**Decision:** compute catalog/per-publication revision and ETag from canonical sanitized public
representation.

**Why:** internal concurrency/health writes should not necessarily invalidate public catalog,
and manual version increments are error-prone.

**Rejected:** exposing EF concurrency token; timestamp-only ETag.

## ADR-009: Stable opaque routing model IDs

**Decision:** create a repository-owned codec that namespaces a public model route by
publication and model token, with reversible lookup only through the server catalog/index.

**Why:** same upstream model names across publications are unambiguous, internal profile IDs
stay private, and callers cannot choose an upstream URI.

**Rejected:** raw model name as route key; database profile GUID in model ID.

## ADR-010: One invocation record, existing usage direction

**Decision:** persist metadata-only `SharedProviderInvocationRecord` and project it into the
existing provider usage model when truthful.

**Why:** it supports audit, access-context correlation, usage completeness, and cost without a
second cost ledger or content storage.

**Rejected:** logging bodies; forcing external requests into Agent/SimpleChat category;
separate unrelated cost database.

## ADR-011: Explicit unavailable state

**Decision:** source/import availability is separate from local enabled intent.

**Why:** temporary outage or unpublish must not overwrite user intent or trigger destructive
deletion. Runtime gates fail explicitly.

**Rejected:** automatically setting `ProviderProfile.IsEnabled=false` on transient failures;
silent personal-provider fallback.

## ADR-012: Application-service E2E seeding

**Decision:** a dedicated non-production orchestrator uses canonical application services and
shared data roots to prepare E2E instances.

**Why:** direct SQL does not exercise validation/secret handling. A production bootstrap
endpoint would be a security liability.

**Rejected:** raw SQL fixtures; unauthenticated test admin API; paid provider.

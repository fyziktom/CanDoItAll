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

**Why:** the strong revision changes whenever the canonical sanitized public representation
changes, including advertised availability state. Internal concurrency values, raw health
details, and volatile health-check timestamps are excluded because they are not public contract
state. Manual version increments are error-prone.

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

## SB00 checkpoint confirmation — 2026-08-24

The current repository evidence confirms all twelve records without requiring an adjacent
pattern. In particular:

- Workspace EF remains the canonical provider master and the AgentFramework catalog remains a
  projection, confirming ADR-001, ADR-002, and ADR-004;
- the six registered Workspace connectors and their distinct capabilities require the registry
  and allowlist decisions in ADR-005 and ADR-006;
- current usage categories cannot truthfully represent external relay traffic, so ADR-010 owns a
  dedicated relay classification instead of reusing Agent or Simple Chat;
- current provider deletion has no general reference-policy seam, so SB02 must implement the
  explicit publication/import relationships required by ADR-001 and ADR-002;
- no product type or project reference changed in SB00. The next implementation step is the
  SDK-free Abstractions project in SB01, followed by the Workspace entities in SB02.

Reopen this checkpoint if implementation requires a switch in every provider path, a second
provider master, HTTP/EF types in Abstractions, or an unregistered compatibility capability.

## SB01 checkpoint confirmation — 2026-08-24

- ADR-007 is realized by the exact `CanDoItAll-Access-Context-Ref` parser, scoped accessor, and
  Web middleware. The value is opaque metadata and grants no authentication or scope.
- ADR-008 is realized by deterministic SHA-256 revisions over canonical, recursively sorted,
  sanitized public state. Revision fields themselves and volatile timestamps are excluded;
  advertised health state is included.
- ADR-009 is realized by the single `sp1.<publication-guid-N-lowercase>.<base64url-sha256>` codec
  and catalog-backed resolution contract. It does not expose a profile ID, model text, URI, or
  caller-controlled path.
- Ports-and-adapters and value-object decisions are realized without introducing an adjacent
  pattern, reflection bridge, service locator, duplicated DTO, or partial-class extension.

The remaining ADRs retain their downstream owners. No record is reopened by SB01.

## SB02 checkpoint confirmation — 2026-08-24

- ADR-001/ADR-002 are realized with explicit relational publication/source/import entities and
  stable local profile/public/service identities.
- ADR-004 is preserved: source/import state transactionally maintains derived effective profile
  URI/secret-reference caches and notifies existing projection observers after commit.
- ADR-010 is realized at the persistence boundary with one metadata-only invocation record and an
  appended shared-relay usage classification, not a competing ledger.
- ADR-011 is realized by distinct local intent, remote availability, transient failure,
  authoritative absence, mismatch, retire, and reappearance transitions.
- Application-managed optimistic tokens, serializable reconciliation, restrictive FKs, and one
  typed deletion policy follow existing repository patterns; no adjacent outbox, event bus,
  generic repository, service locator, or partial-class pattern was introduced.

No ADR is reopened. Network validation, relay adapters, runtime projection, and editor ownership
retain their downstream owners.

## SB06 checkpoint confirmation — 2026-08-25

- ADR-002 and ADR-004 are realized by one canonical EF-backed effective-profile projection and the
  existing AgentFramework catalog/runtime invalidation path.
- ADR-005 is preserved: `provider.candoitall-shared` is an origin adapter mapped to the existing
  OpenAI driver and MAF SDK, not a new provider kind or runtime branch family.
- ADR-006 is enforced with exact publication-owned model IDs and remote feature constraints. The
  unadvertised audio surface fails closed through a typed source-credential policy.
- ADR-007 remains request scoped: access context is added to each outbound request and never stored
  in `HttpClient.DefaultRequestHeaders`.
- ADR-011 is enforced at catalog, runtime, and voice selection boundaries; unavailable or
  ineligible shared selection never substitutes a personal provider.

No ADR is reopened. The selected anti-corruption/effective-profile adapter and thin compatibility
facade were sufficient.

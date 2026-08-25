# SB03 semantic invariant contract

State: `COMPLETE`. Source assertions, focused behavior, architecture, security, and transcript
bindings are frozen. Per-file hashes are centralized in `proof/hashes.sha256`.

## SB03-INV-01 — Publication eligibility is a fail-closed intersection

- **Raw note / requirement:** FR-004, FR-005, NFR-022: only enabled, valid, supported production
  connectors may publish; synthetic scenario/process mocks and runtime fallback profiles must not.
- **Expected behavior:** profile validity, current connector manifest/schema, required secret
  existence, exact typed publication metadata, production connector provenance, exact purpose and
  transport, and production relay support all intersect before publication or discovery.
- **Disallowed shallow implementation:** trusting user-editable capability booleans, connector key
  alone, numeric enum strings, a test descriptor, inferred Azure/fallback/imported provenance, or a
  non-existent secret reference.
- **Failing-first proof:**
  `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-failing-first-unit.txt`.
- **Passing test proof:** the exact unit selection discovered 18 and passed 18/18 in
  `sb03-list-unit-release.txt` and `sb03-run-unit-release.txt`.
- **Changed source:** `SharedProviderProfilePublicationMetadata.cs` and
  `SharedProviderPublicationEligibilityPolicy.cs`; see `proof/hashes.sha256`.
- **Production assertions:** Workspace and AgentFramework save paths write the shared constants;
  eligibility uses strict JSON, exact named enum tokens, exact OpenAI/ComfyUI image transports,
  production classification, and `requiredSecretExists` with no default-true overload.
- **Red-team negative:** disabled, malformed, numeric classification, missing/dangling secret,
  unknown/non-execution connector, missing/Test relay descriptor, imported/fallback/mock/Azure,
  unsupported transport, and invalid models all fail closed.
- **Downstream dependency:** SB04 may dispatch only a routing target emitted by this policy; SB08
  must present the same sanitized reason rather than duplicate eligibility logic.

## SB03-INV-02 — Real provider save paths produce canonical publication metadata

- **Raw note / requirement:** FR-004, NFR-018, NFR-022: Workspace EF provider data remains the
  source of truth and actual saved profiles must be publishable only from typed canonical metadata.
- **Expected behavior:** Workspace editor save, AgentFramework registry save, and managed runtime
  bootstrap write the same canonical property names, enum tokens, default model, and explicit
  suggested-model array while preserving compatible OpenAI chat/image/Azure classification.
- **Disallowed shallow implementation:** hand-seeding metadata only in unit fixtures, silent
  eligibility inference from connector strings, omitting `suggestedModels`, or advertising the
  full pricing table as supported public models.
- **Failing-first proof:** the initial unit source used strict metadata and failed before its
  production writer existed in `sb03-failing-first-unit.txt`.
- **Passing test proof:** the existing publication positive Fact exercises
  `WorkspaceService.SaveProviderAsync`, reloads the stored row, verifies `suggestedModels: []`,
  re-evaluates eligibility, then publishes; `sb03-run-unit-release.txt` records 18/18.
- **Changed source:** `WorkspaceModels.cs`, `AgentFrameworkProviderMetadata.cs`,
  `WorkspaceBackedAgentProviderProfileRegistry.cs`, `RuntimeHostServiceCollectionExtensions.cs`,
  and `SharedProviderProfilePublicationMetadata.cs`; see `proof/hashes.sha256`.
- **Production assertions:** pricing metadata is written first, then the publication writer owns
  only the default plus explicitly preserved suggested models. Pricing rows are not treated as
  public support declarations.
- **Red-team negative:** a missing target secret or unsupported classification returns validation
  before persistence, observer, or activity; empty AgentFramework suggested models remain an
  explicit empty array.
- **Downstream dependency:** SB08 can use the real Workspace save path without a hidden metadata
  backfill; SB04 can trust advertised model IDs as declared support rather than price-table names.

## SB03-INV-03 — Publication mutation is explicit, concurrent, and post-commit

- **Raw note / requirement:** FR-001, FR-002, FR-009, NFR-012: sharing defaults off, requires an
  explicit administrator action, unpublish removes discovery, and stale writers fail
  deterministically.
- **Expected behavior:** `ChangeAsync` validates the expected publication token, re-evaluates an
  unpublished publish candidate, commits one state transition, then invokes invalidation observers
  and records metadata-only activity with a non-request cancellation token.
- **Disallowed shallow implementation:** toggling a profile flag, publishing without eligibility,
  updating on a stale token, invalidating before commit, or emitting activity after a rejected
  request.
- **Failing-first proof:** `sb03-failing-first-unit.txt` failed on the absent application service
  and observer seam.
- **Passing test proof:** the two application Facts cover publish/unpublish, stable public ID,
  actual Workspace save, ineligible/stale rejection, persisted state, and zero side effects;
  `sb03-run-unit-release.txt` records 18/18.
- **Changed source:** `SharedProviderPublicationApplicationService.cs`,
  `SharedProviderCatalogCache.cs`, and Workspace DI registration; see `proof/hashes.sha256`.
- **Production assertions:** publication rows retain the SB02 stable `PublicId`; EF application
  concurrency tokens are checked before transition and regenerated on commit.
- **Red-team negative:** stale token and dangling-secret publish leave the row unpublished and
  produce no activity or observer notification.
- **Downstream dependency:** SB08 publication UI must submit the exact concurrency token; SB04
  routes only current published catalog entries.

## SB03-INV-04 — Public catalog and routing are deterministic and sanitized

- **Raw note / requirement:** FR-003, FR-006 through FR-010, FR-020, FR-021, NFR-001, NFR-016.
- **Expected behavior:** explicit public DTOs expose stable source/publication/routing identities,
  display name, purpose, public transport, tested capabilities, bounded health, canonical revision,
  and strong ETag; a private routing index retains the exact profile/upstream target in process.
- **Disallowed shallow implementation:** serializing EF/AgentFramework profiles, embedding internal
  profile IDs or raw upstream model names in routing IDs, hashing private configuration, using list
  order as identity, or collapsing duplicate upstream model names across publications.
- **Failing-first proof:** `sb03-failing-first-unit.txt` failed on the absent projector/routing/cache
  types; `sb03-failing-first-catalog-api.txt` later failed all 14 route cases.
- **Passing test proof:** `sb03-run-unit-release.txt` records 18/18 and
  `sb03-run-catalog-api-release.txt` records 14/14.
- **Changed source:** `SharedProviderCatalogProjection.cs`,
  `SharedProviderCatalogQueryService.cs`, and the SB01 Abstractions canonical contracts; see
  `proof/hashes.sha256`.
- **Production assertions:** routing model IDs contain publication identity plus a SHA-256 model
  fingerprint; the catalog is serialize/deserialize normalized before caching; exact raw upstream
  IDs exist only in `SharedProviderRoutingTarget`.
- **Red-team negative:** unpublished/ineligible rows and unknown routing IDs are absent; internal
  profile ID, base URL, secret ID/value/name, configuration, notes, and raw health status cannot
  appear in serialized catalog or models output.
- **Downstream dependency:** SB04 must resolve caller model input only through
  `ISharedProviderRoutingResolver`; SB05 consumes the stable source/catalog identity and ETag.

## SB03-INV-05 — Cache correctness survives host boundaries and eligibility changes

- **Raw note / requirement:** NFR-010, NFR-027, NFR-030, NFR-031: revocation must take effect,
  ETag must be deterministic, warmed routing must be cached, and correctness cannot depend on one
  process's observer.
- **Expected behavior:** every query derives a stamp from persisted source identity,
  publication/profile concurrency tokens, and current referenced-secret existence; it re-evaluates
  current eligibility before reusing or replacing the projection cache.
- **Disallowed shallow implementation:** process-static source identity, observer-only
  invalidation, caching eligibility forever, or retaining a route after a required secret becomes
  dangling.
- **Failing-first proof:** `sb03-failing-first-unit.txt` failed on the absent query/cache service.
- **Passing test proof:** the cross-host Fact uses two independent caches, deletes the referenced
  secret directly, and requires both hosts to remove the route and converge on the new ETag;
  `sb03-run-unit-release.txt` records 18/18.
- **Changed source:** `SharedProviderCatalogCache.cs`, `SharedProviderCatalogQueryService.cs`,
  `SharedProviderServiceIdentityStore.cs` (SB02 producer), and profile/publication commit wiring;
  see `proof/hashes.sha256`.
- **Production assertions:** the cache stores no secret material and is keyed by a SHA-256 of
  persisted public-change tokens/existence, not by local observer generation alone.
- **Red-team negative:** secret deletion without a local observer still invalidates both host
  views; a malformed/disabled profile is re-evaluated away.
- **Downstream dependency:** SB07 owns real multi-instance checkpoint proof; SB12 owns the one
  authorized broad frozen gate. The current per-request eligibility-input reload is deliberate
  fail-closed behavior; future scale work must not replace it with observer-only correctness.

## SB03-INV-06 — Conditional GET and error envelopes are protocol-correct

- **Raw note / requirement:** FR-008, FR-011, FR-024, NFR-009, NFR-027.
- **Expected behavior:** both GET routes emit the catalog-derived strong ETag, private/no-cache and
  server request-id headers, accept RFC 9110 weak comparison/list/wildcard validators, return 304
  only on a valid match, and use native versus OpenAI error envelopes by route.
- **Disallowed shallow implementation:** raw string equality, accepting `*` mixed with an entity-tag
  list, treating malformed input as a cache hit, reflecting exception messages, or returning one
  generic envelope for both surfaces.
- **Failing-first proof:** `sb03-failing-first-catalog-api.txt` records 14/14 failures before route
  implementation.
- **Passing test proof:** `sb03-list-catalog-api-release.txt` discovers exactly 14 and
  `sb03-run-catalog-api-release.txt` records 14/14.
- **Changed source:** `SharedProviderCatalogApi.cs`, `SharedProviderApiResponseWriter.cs`, and
  `SharedProviderCatalogOpenApiContract.cs`; see `proof/hashes.sha256`.
- **Production assertions:** endpoint code delegates to `ISharedProviderCatalogQueryService`,
  parses `If-None-Match` with strict framework header types plus the wildcard grammar check, and
  logs controlled metadata without response bodies/private exceptions.
- **Red-team negative:** malformed and mixed-wildcard validators, malformed access context, and a
  controlled catalog exception return safe route-specific 400/503 responses with request IDs.
- **Downstream dependency:** SB05 relies on trustworthy ETag/304; SB11 exports the final OpenAPI
  document and owns the broader auth-scheme documentation surface.

## SB03-INV-07 — Catalog authorization is least-privilege and access context is not auth

- **Raw note / requirement:** NFR-002, NFR-003, NFR-010 and acceptance criterion 3.
- **Expected behavior:** catalog-read and invoke are distinct typed scope constants/policies; both
  catalog GETs require catalog-read, the existing umbrella `api` convention remains compatible,
  and the opaque access-context reference neither grants nor changes authentication.
- **Disallowed shallow implementation:** accepting invoke-only for discovery, treating access
  context as a claim, allowing malformed/expired tokens, or enabling optional-auth routes when API
  authorization is disabled.
- **Failing-first proof:** `sb03-failing-first-authorization.txt` records 10/10 failures before
  policy/endpoint wiring.
- **Passing test proof:** `sb03-list-authorization-release.txt` discovers exactly 10 and
  `sb03-run-authorization-release.txt` records 10/10.
- **Changed source:** `ApiAccessScopeNames.cs`, `ApiAuthorizationPolicies.cs`,
  `ApiServiceCollectionExtensions.cs`, and endpoint metadata; see `proof/hashes.sha256`.
- **Production assertions:** endpoints use the catalog-read policy; invoke has its own policy for
  SB04; optional authorization mode keeps catalog routes anonymous only when auth is globally
  disabled.
- **Red-team negative:** missing, malformed, expired, and invoke-only tokens return native/OpenAI
  401/403 envelopes; malformed access context remains a 400 input error, not an auth decision.
- **Downstream dependency:** SB04 binds inference endpoints to invoke; SB11 owns exported security
  scheme documentation.

## SB03-INV-08 — Secret deletion and provider saves cannot create dangling publication inputs

- **Raw note / requirement:** FR-004, NFR-001, NFR-010, NFR-012, NFR-018.
- **Expected behavior:** any provider save targeting a secret and secret deletion use the same
  stable mutation key; deletion checks Workspace provider/source references before commit; required
  references must exist without resolving their value.
- **Disallowed shallow implementation:** checking only non-null GUIDs, defaulting secret existence
  to true, locking only the target but not the old reference, logging the secret GUID, or relying on
  an absent `ProviderProfile` FK.
- **Failing-first proof:** the original unit negative set exposed the missing-secret application
  gap; the deterministic interleaving was added during review without increasing the frozen 18
  Facts. The initial red transcript remains the honest pre-production record.
- **Passing test proof:** the final existing Fact blocks inside the deletion policy while Delete
  owns the mutation scope, starts Save, then proves typed deletion failure, successful save, both
  secret rows present, and no dangling profile; `sb03-run-unit-release.txt` records 18/18.
- **Changed source:** `SecretDeletionReferencePolicy.cs`, `SecurityModels.cs`,
  `ProviderProfileSecretMutationScope.cs`, `WorkspaceProviderSecretDeletionReferencePolicy.cs`,
  and both provider save paths; see `proof/hashes.sha256`.
- **Production assertions:** PostgreSQL uses serializable transactions plus advisory locks;
  InMemory uses deterministic process locks; exception text omits the secret GUID while preserving
  a typed property for trusted callers.
- **Red-team negative:** referenced delete, concurrent save/delete, missing target secret, empty
  required GUID, and direct dangling-secret catalog recheck all remain fail-closed.
- **Downstream dependency:** SB04 cannot invoke an advertised route backed by a deleted required
  secret; SB07/SB12 must revalidate multi-host/database behavior.

## SB03-INV-09 — Dependency direction and composition remain explicit

- **Raw note / requirement:** NFR-015 through NFR-017, NFR-020, NFR-021.
- **Expected behavior:** Abstractions is SDK-free and inward; Http implements descriptor support
  behind the catalog port; outer Composition registers Http; Workspace references only
  Abstractions; Web maps thin routes over Workspace's query port.
- **Disallowed shallow implementation:** Workspace-to-Http, Abstractions-to-product, upstream
  dispatch in Web, reflection/service-location bridges, duplicated DTOs, or a new large partial.
- **Failing-first proof:** before snapshot/reference artifacts establish the absent Http project and
  expected two-edge delta; Web discovery exposed the missing explicit Http test reference.
- **Passing/source proof:** source-level reference review is complete; the after snapshot
  `snap-20260825012213-a17e36ed` reports 14 projects, 33 direct references, no project cycle,
  unchanged two module and one type cycles, and no error finding.
- **Changed source:** Http project/catalog/registration, Composition reference/registration,
  Workspace query/policy files, Web route files, and focused project references; see
  `proof/hashes.sha256`.
- **Production assertions:** the actual host resolves `SharedProviderRelaySupportCatalog` with
  exactly five production rows and no mock/import/fallback/audio/Azure row.
- **Red-team negative:** test-classified descriptors fail eligibility and forbidden connector rows
  are absent from actual host composition.
- **Downstream dependency:** SB04 extends adapter dispatch without reversing dependencies; any
  after-graph cycle or unexpected edge reopens SB03 and blocks downstream progression.

## Governed bindings

`SB03-INV-01` through `SB03-INV-05` and `SB03-INV-08` bind to the exact unit list/run
transcripts. `SB03-INV-04` and `SB03-INV-06` additionally bind to the exact catalog/API
transcripts; `SB03-INV-07` binds to the authorization transcripts; `SB03-INV-09` binds to the
after CodeAnalytics/reference artifacts and actual-host composition case. The anti-stub and
secret/content scan transcripts are clean. File hashes are recorded centrally in
`proof/hashes.sha256`.

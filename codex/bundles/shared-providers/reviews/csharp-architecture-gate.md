# C# architecture gate

Current checkpoint result: `PASS_CP04_SB06`

Complete at every architecture checkpoint and finally in SB12.

CP-04 is closed. SB07 owns the separate multi-instance backend checkpoint before UI unlocks.

## Gate questions

### Ownership

- Are publication/source/import/audit entities owned by Workspace?
- Are public protocol records independent of Workspace EF and MAF models?
- Are provider-specific HTTP details isolated in Integration implementation?
- Are Web endpoints thin?
- Is local tool/workflow execution still local?

### Dependency direction

- Is Abstractions free of Web/UI/EF/SDK dependencies?
- Does Workspace reference only Abstractions?
- Is concrete Http wiring in Composition/Web?
- Do inner MAF projects have no new outer reference?
- Are there zero cycles by CodeAnalytics/project graph?

### Canonical model

- Is Workspace provider data still master?
- Is publication a separate explicit public projection?
- Is source credential stored once?
- Does import preserve one stable local provider ID?
- Is availability distinct from enabled intent?
- Is invocation record the one relay usage source?

### Pattern selection

- Is upstream dispatch adapter/registry driven?
- Are compatibility fields policy driven?
- Is routing ID handled by one codec/index?
- Is sync one deterministic reconciliation service?
- Is legacy execution thin or explicitly excluded?

### Testability

- Can each policy be directly unit tested?
- Are real PostgreSQL/API/streaming/three-instance seams present?
- Are negative tests meaningful?
- Are test filters/discovery recorded?
- Is broad gate count within budget?

### Partial class policy

- Were cohesive top-level files added?
- Did `WorkspaceModels.cs` shrink/remain stable rather than absorb the feature?
- Did runtime partials avoid new provider-specific behavior?

## SB05 decision

- Result: `PASS_SB05` for the source/synchronization half of CP-04; not the final CP-04 result.
- Ownership: Workspace owns source/import use cases and transactions behind neutral ports; Http owns
  safe outbound transport; Composition owns concrete wiring.
- Dependency direction: exact before/after comparison has no product-reference delta. Snapshot
  `snap-20260825070408-300644c7` has 14 projects, 34 direct references, zero project cycles, and no
  error finding.
- Patterns: typed redacted credential, safe named client with connection-time destination policy,
  pinned identity, deterministic reconciliation plan/coordinator, and post-commit observers.
- Testability: exact 18/22/16 lanes cover actual registered handlers, deterministic state planning,
  and real HTTP/secret/PostgreSQL use cases; all owning builds are warning/error free.
- Review repairs: unhealthy-state conditional GET/304 recovery, default HttpClient URI logging,
  request stringification, special-purpose address classification, post-enable sync, remote-owned
  refresh, and real replacement retirement proof were closed.
- Public/partial review: typed neutral/application surfaces expose no token value, URI through
  `ToString()`, EF entity over wire, or implementation type. No partial class was introduced.
- Evidence: `bundle://subbundles/SB05-client-source-sync-selection-reconciliation/proof/manifest.md`.

## SB06 decision

- Result: `PASS_SB06`; CP-04 is complete.
- Ownership: Workspace validates and materializes the canonical effective profile; the outer
  AgentFramework module projects it; Models carries typed neutral constraints; Providers/MAF reuse
  existing OpenAI runtime paths; Composition owns hardened clients and request-scoped context.
- Dependency direction: normalized before/after references have zero delta. Force-refreshed
  snapshot `snap-20260825100508-300644c7` reports 14 projects, 766 documents, 35 modules, 5,281
  dependency facts, 34 direct references, zero project cycles, unchanged governed non-project
  cycles, and zero error findings.
- Patterns: one anti-corruption/effective-profile adapter, exact publication model allow-list,
  typed source credential/network/feature/audio policies, post-commit catalog projection, and no
  `ProviderKind.Shared` or second runtime.
- Testability: exact frozen materializer/runtime/hybrid lanes pass 18/18, 16/16, and 10/10. The
  post-review audio repair passes feature/UI policy 16/16, concrete drivers 54/54, and personal
  voice 29/29; both owning solution builds have zero warnings/errors.
- Review chronology: the final architecture audit initially blocked source-managed audio egress.
  Runtime now rejects both audio operations before credential/HTTP use, voice settings omit shared
  profiles, and an explicit ineligible persisted voice selection remains empty instead of falling
  back to a personal provider. Architecture and security re-audits then returned PASS with no
  remaining P1/P2 blocker.
- Evidence: `bundle://subbundles/SB06-shared-connector-runtime-projection-hybrid-use/proof/manifest.md`.

## August 25 SB04/SB06 invalidation addendum

- Result: `PASS_CP04_SB06` remains valid after the SB07 semantic repairs.
- SB04 was reopened because Responses `store:false` canonicalization and operation/model mismatch
  rejection changed named wire-contract and adapter/capability invalidation keys. Current Unit,
  Web, and Integration Release builds are clean; exact SB04 relay-policy, compatibility, and
  streaming selections pass 24/24, 22/22, and 12/12. The review found no new public API,
  project-reference edge, partial class, caller-controlled proxy seam, or access-context leak.
- SB06 was then revalidated because its older authority resolved to Debug assemblies. The exact
  frozen materializer, runtime-projection, and hybrid selections were freshly listed and passed in
  genuine Release at 18/18, 16/16, and 10/10. The connector-neutral outer-projection decision,
  explicit selection, capability intersection, and no-fallback boundary remain intact.
- Evidence:
  - `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/architecture/august-25-contract-revalidation.md`;
  - `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/security/august-25-contract-revalidation.md`;
  - `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-reopen-closure-validator.txt`;
  - `bundle://subbundles/SB06-shared-connector-runtime-projection-hybrid-use/proof/architecture/sb04-downstream-invalidation-release-revalidation.md`.

## SB07 large-file exception record

- `E2eScenarioRunner.cs` is intentionally one non-production, stateful coordinator for the frozen
  19-scenario phased proof. Its phase methods share one captured baseline, one bounded HTTP client,
  one credential/options set, and one ordered evidence accumulator across unpublish, identity,
  outage, and recovery mutations. Splitting those phase methods into independently injectable
  services would expose cross-phase mutable state and make the required ordering implicit.
- The reusable responsibilities are already separated: `E2eScenarioHttpClient` owns bounded HTTP
  and SSE transport; `E2eScenarioData` owns typed snapshot/evidence schemas; `E2eScenarioResults`
  owns exact result merging; `E2eArtifactStore` owns bounded artifact persistence; preparation,
  command parsing, fixtures, and service hosting are separate files. The runner is not referenced
  by production code and cannot become an alternate runtime path.
- `SharedProviderBackendCheckpointIntegrationTests.cs` is likewise a frozen ten-Fact executable
  checkpoint, not a production class. Each Fact owns one named behavior partition while the real
  host, persistence, relay, streaming, and hybrid fixtures remain in their owning integration-test
  files. Keeping the ten exact checkpoint cases together preserves discovery/count review and does
  not add product coupling.
- This exception is narrow to the two frozen SB07 proof coordinators. It does not permit large
  production files, partial-class growth, or moving transport, persistence, fixture, or result
  responsibilities back into either file.

## Required evidence

- before/after ProjectReference tables;
- CodeAnalytics snapshot IDs and no-cycle output;
- changed namespace/type dependency report;
- direct project builds;
- architecture guardrail tests;
- review of every new public type;
- explanation for every exception.

## SB00 decision

- Result: `PASS_SB00` for the baseline/decision-lock checkpoint. This is not the final SB12
  architecture decision.
- Evidence:
  - before references: `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/project-references-before.md`;
  - after references: `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/project-references-after.md`;
  - changed namespace/type review: `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/changed-namespace-type-report.md`;
  - CodeAnalytics snapshots `snap-20260824190346-9451b9e9` and
    `snap-20260824195319-b6470538`, with 11 projects, 23 direct references, and zero
    project-level cycles before and after;
  - 8/8 architecture characterization tests and 6/6 runtime-path characterization tests;
  - canonical persistence, call-path, connector/capability, usage/deletion, API/OpenAPI/SSE,
    and standards evidence in the SB00 governed proof.
- Repairs:
  - locked Workspace EF as the provider master and AgentFramework as projection/runtime;
  - confirmed the two-project SharedProviders Abstractions/Http boundary;
  - resolved Azure as an effective runtime kind stored through the OpenAI connector, without an
    invented Azure manifest;
  - excluded shared audio from v1 despite existing OpenAI STT/TTS drivers because Workspace has
    no audio manifest/purpose and the shared relay has no audio contract proof;
  - assigned the missing provider-reference policy and truthful relay workload to SB02;
  - classified the two pre-existing module cycles and one nested-type cycle as unchanged.
- Public-type review: SB00 added no product or public type. Its two added sealed test classes are
  cohesive and add no project reference.
- Partial-class review: no partial class or product source changed. `WorkspaceModels.cs` and the
  existing runtime partials did not grow.
- Downstream work:
  - SB01 may add only `CanDoItAll.SharedProviders.Abstractions`, with no external package or
    product reference;
  - every production project/reference change must refresh the reference table and CodeAnalytics
    checkpoint;
  - capability advertisement remains gated by tested adapter support;
  - any forbidden edge, dynamic/reflection bridge, duplicated public DTO, or unresolved cycle
    fails the next checkpoint.

## SB01 checkpoint decision

- Result: `PASS_SB01`. This is an implementation checkpoint, not the final SB12 decision.
- Boundary: `CanDoItAll.SharedProviders.Abstractions` owns strict SDK-neutral protocol, identity,
  routing, canonical-revision, failure, and port contracts. Web owns only internal scoped state
  and header middleware.
- Dependency proof: snapshots `snap-20260824204913-6a7763ae` and
  `snap-20260824213007-c65710b4` show 11 to 12 projects, 23 to 24 direct production references,
  and zero project-level cycles. The sole new production edge is `Web -> Abstractions`;
  Abstractions has no package/project reference. Baseline module/type cycles are unchanged.
- Public-type review: all 36 top-level public contract types and five nested closed-result shapes
  are cohesive and accounted for. No catalog record can carry internal provider/secret/URI/raw
  error/content fields; implementation converters, state, and middleware remain internal.
- Request pipeline: the optional exact header binds to one scoped accessor after the existing
  authentication/authorization pair and before endpoint dispatch. It is separate from claims,
  scopes, W3C baggage, outbound HTTP, and `IHttpContextAccessor`.
- Testability: Release builds pass with zero warnings/errors; exact focused selections discover
  and pass protocol 12/12, routing 10/10, and access context 10/10. Baseline failing controls,
  anti-stub, dependency, access-boundary, and secret scans are durable in the SB01 proof.
- Partial-class review: no partial class was added or extended; Workspace/runtime monoliths did
  not grow.
- Independent frozen-code review: `PASS`, with no correctness/security blocker across strict
  parsing, deterministic health-sensitive revisions, capability coherence, defensive copies,
  routing privacy, invalid defaults, base-path semantics, or scoped isolation.
- Evidence:
  - `bundle://subbundles/SB01-protocol-identities-and-access-context/proof/manifest.md`;
  - `bundle://subbundles/SB01-protocol-identities-and-access-context/proof/architecture/public-type-inventory.md`;
  - `bundle://subbundles/SB01-protocol-identities-and-access-context/proof/architecture/codeanalytics-after.md`;
  - `bundle://subbundles/SB01-protocol-identities-and-access-context/proof/behavior/protocol-routing-access.md`;
  - `bundle://subbundles/SB01-protocol-identities-and-access-context/proof/security/contract-boundary.md`.
- Downstream constraint: SB02 may add only the Workspace-to-Abstractions product edge and the
  locked persistence/state model. Http implementation, catalog endpoints, and relay remain
  downstream-owned.

## SB02 checkpoint decision

- Result: `PASS_SB02`. This is the persistence checkpoint, not the final SB12 decision.
- Ownership: Workspace owns all five relational entities, configurations, pure transitions,
  scoped application services, reconciliation, audit metadata, and provider-reference policy.
  Existing provider/profile and secret records remain canonical.
- Dependency proof: snapshots `snap-20260824213007-c65710b4` and
  `snap-20260824231242-d9fc36b9` show 12 projects, 24 to 25 direct product references, and zero
  project cycles. The sole new edge is `Workspace -> Abstractions`; baseline module/type cycles
  are unchanged.
- Persistence proof: the generated migration/model snapshot contains all five tables, 13 indexes,
  five restrictive FKs, uniqueness/identity/completion checks, and no content/secret-value
  column. Clean PostgreSQL migration and EF pending-model checks pass.
- Lifecycle proof: pure and real-database tests preserve profile/import/public/service identity,
  local alias/enabled intent, source pin, and remote state across transient failure,
  authoritative absence, mismatch, retire, and reappearance. A source edit updates two linked
  profiles atomically; stale state remains rolled back.
- Deletion/usage proof: both production delete paths and transfer preflight use one typed policy;
  database `Restrict` is authoritative. Existing usage enums retain numeric values and gain an
  explicit shared-relay classification rather than misclassifying cost.
- Public/partial review: all 36 new Workspace public declarations, the Usage enum additions, and
  the Infrastructure uniqueness helper are reviewed. Configurations/guards/classifier stay
  internal; no partial type, reflection bridge, dynamic contract, or service locator was added.
- Testability: Release builds pass with zero warnings/errors; exact focused selections discover
  and pass state 18/18, persistence 14/14, and deletion 6/6. EF, anti-stub, secret/content, and
  independent review gates pass.
- Evidence:
  - `bundle://subbundles/SB02-publication-source-import-persistence/proof/manifest.md`;
  - `bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/changed-namespace-public-surface-review.md`;
  - `bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/entity-index-fk-inventory.md`;
  - `bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/codeanalytics-after.md`;
  - `bundle://subbundles/SB02-publication-source-import-persistence/proof/behavior/persistence-and-reconciliation.md`;
  - `bundle://subbundles/SB02-publication-source-import-persistence/proof/security/persistence-containment.md`.
- Downstream constraints: SB03 owns eligibility, administrator publication, catalog/auth/ETag;
  network/SSRF and HTTP sync remain SB05; connector/runtime enforcement remains SB06; generic
  editor ownership remains SB08; relay use and retention cleanup remain SB04/SB12.

## SB03 central checkpoint decision

- Result: `PASS_SB03` for the central-publication/catalog/API half. Shared CP-03 remains `OPEN`;
  this is not a pass for SB04 relay behavior or the final SB12 architecture decision.
- Ownership: Workspace owns eligibility, explicit publication mutation, canonical sanitized
  projection/routing, and persisted invalidation. Abstractions owns SDK-neutral OpenAI discovery and
  support contracts. The Http integration project owns production support descriptors only,
  Composition owns registration, and Web owns thin GET/auth/error/conditional/OpenAPI concerns.
- Dependency proof: snapshots `snap-20260824235022-a4b340a8` and
  `snap-20260825012213-a17e36ed` show 13 to 14 scoped product projects, 31 to 33 direct references,
  and zero project cycles. The exact authorized delta is
  `SharedProviders.Http -> SharedProviders.Abstractions` plus outer
  `Composition -> SharedProviders.Http`; Workspace retains no Http edge and Abstractions remains a
  leaf contract assembly. Baseline module/type cycles are unchanged.
- Publication/catalog proof: eligibility intersects enabled valid production metadata with the
  registered support descriptor; synthetic, imported, fallback, unknown, malformed, and
  unsupported profiles fail closed. Explicit publish/unpublish is concurrency checked and emits
  activity/cache invalidation only after commit. Canonical projection preserves public identity,
  disambiguates duplicate model names, excludes private changes from public revisions, and uses a
  persisted stamp across query instances.
- Web/security proof: native catalog and OpenAI models GET routes use catalog-read or umbrella
  scope, path-correct bounded native/OpenAI errors, server request ID, `private, no-cache`, and a
  strong public ETag. RFC 9110 weak/list/multiple-line/wildcard matching works; malformed, bounded
  overflow, and mixed wildcard/tag lists fail closed. Missing, malformed, expired, and wrong-scope
  tokens cannot read the catalog. Marker-scoped OpenAPI describes both request headers and the
  actual common/ETag response headers without claiming SB11 security-scheme ownership.
- Public/partial review: new public types remain cohesive in Abstractions, Workspace, or the
  descriptor integration boundary; Web implementations are internal. No partial type, reflection
  bridge, dynamic dispatch, alternate DTO family, or large endpoint-extension append was added.
- Testability: Unit, Web, and Integration Release builds pass with zero warnings/errors; exact
  selections discover and pass publication/catalog 18/18, catalog API 14/14, and authorization
  10/10. Failing-first chronology is preserved. Independent review found no remaining Web,
  authorization, ETag, header, error-envelope, or OpenAPI blocker after its negative vectors landed.
- Evidence:
  - `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/manifest.md`;
  - `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/architecture/project-references-after.md`;
  - `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/architecture/codeanalytics-after.md`;
  - `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-run-unit-release.txt`;
  - `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-run-catalog-api-release.txt`;
  - `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-run-authorization-release.txt`.
- Downstream constraints: SB04 alone may continue CP-03 and owns inference POST routes, real relay
  adapters/dispatch, no-open-proxy and field-denylist enforcement, streaming/cancellation, tools,
  structured output, vision/images, upstream error mapping, and invocation audit/usage population.
  SB05 network/sync, SB06 runtime projection, SB07 multi-host proof, and SB11 final OpenAPI security
  scheme/export remain downstream-owned.

## SB04 central relay checkpoint decision

- Result: `PASS_SB04`; shared CP-03 is complete. This is not the final SB12 architecture decision.
- Ownership: Abstractions owns neutral relay/runtime contracts; Workspace re-resolves canonical
  publication, profile, eligibility, routing model, secret, and capability state and owns the
  metadata-only invocation lifecycle. Http owns strict request policy, connector URI/auth policy,
  the production adapter registry, bounded response rewriting, and SSE transport. Web owns three
  thin authorized POST surfaces. AgentFramework owns only the narrow existing image-driver and
  existing usage-projection bridges. Composition remains the outer Http registration boundary.
- Dependency proof: CodeAnalytics snapshot `snap-20260825051057-300644c7` records 14 scoped product
  projects, 34 direct references, and zero project cycles. The sole SB04 product-reference delta is
  the authorized `Modules.AgentFramework -> SharedProviders.Abstractions` bridge; Workspace and Web
  do not reference the Http implementation, Abstractions remains free of product dependencies, and
  the pre-existing module/type cycles are unchanged.
- Dispatch/security proof: exactly five production connector/purpose rows bind OpenAI Chat,
  Responses and Images, Ollama Chat, and ComfyUI Images to real adapters. Every invocation
  re-resolves current persisted ownership and secret existence; caller URI/header/upstream-model
  overrides, unknown fields, unsupported capabilities, built-in/hosted tools, private response
  headers, raw upstream errors, image paths/URLs, and unpublished or mismatched routes fail closed.
- Streaming/image/audit proof: `ResponseHeadersRead`, bounded SSE parsing, prompt first-byte flush,
  ordered events, strict UTF-8, terminal usage, downstream cancellation/upstream disposal, separate
  connect/overall/idle timeout handling, and no synthetic success after midstream failure are
  covered. Images use bounded base64 only. Invocation finalization is idempotent and its bounded
  retry loop is source-reviewed; the behaviorally proven hosted Workspace worker finalizes only
  stale `InProgress` rows without changing fresh/terminal rows or persisting content, secrets,
  private endpoints, or fabricated zero usage. No forced finalizer-save failure is claimed.
- Persistence/usage trust: SB04's additive ImageCount surfaces preserve constructor/deconstruction
  ABI and enforce operation-disjoint token/image completeness in EF configuration, migration,
  designer, snapshot, projection, and aggregation. Because this invalidated SB02-owned proof,
  exact SB02 18/14/6, supporting usage 7/7, and EF pending-model/no-drift were rerun and pass.
- Public/partial review: contracts and implementations are cohesive top-level types in their
  owning boundaries. No partial class, reflection/dynamic bridge, duplicate DTO family, service
  locator, caller-controlled proxy seam, or competing usage ledger was introduced.
- Testability: Unit, Web, and Integration Release builds pass with zero warnings/errors. Exact
  selections discover and pass relay policy 24/24, OpenAI compatibility 22/22, and streaming 12/12;
  anti-stub and secret/content/access-context containment scans pass. CodeAnalytics reports no
  error finding.
- Evidence:
  - `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/proof-manifest.json`;
  - `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-project-references-after-semantic-final.txt`;
  - `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-run-relay-policy-release-after-image-count-fix.txt`;
  - `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-run-openai-compatibility-release-semantic-final.txt`;
  - `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-run-streaming-release-semantic-final.txt`;
  - `bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/sb04-downstream-invalidation-revalidation.md`;
  - `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/security/relay-containment.md`.
- Downstream constraints: SB05 retains source URI/network/TLS/DNS policy and conditional sync; SB06
  retains imported-provider projection and no-fallback runtime selection; SB07 retains real
  three-app/multi-host propagation and E2E; SB08/SB09 retain UI; SB11 retains final OpenAPI security
  scheme/export and SharedInfo; SB12 retains retention cleanup, one broad aggregate, and final
  running-stack closure.

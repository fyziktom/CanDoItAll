# Documentation, API skills, and schema review

Reviewed on 2026-08-30. Product baseline: development `1625b336e4f60ddb64987240c3a3dc485591d20f`; reviewed head: `3fc10d2db7ba7e4e15bc94f50e66f815f31c4219`. This is preparation evidence, not an implementation or merge approval. SharedInfo was inspected read-only.

## Verified findings

### DC01 — P2: the maintained documentation gate fails on six new projects

Executed `./tools/Validation/Test-Documentation.ps1` from CanDoItAll. Exit code 1; exactly six missing project README findings:

- `src/Integration/CanDoItAll.SharedProviders.Abstractions/README.md`
- `src/Integration/CanDoItAll.SharedProviders.Http/README.md`
- `src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Abstractions/README.md`
- `src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Application/README.md`
- `src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/README.md`
- `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/README.md`

The source files exist, but their maintained owner/dependency/run/validation contracts do not. The historical bundle success reports did not close the repository documentation gate.

Minimal repair: add concise project READMEs using the local project README shape, describing actual boundaries and focused build/test commands. Link durable sharing/history guides from product indexes. Do not copy execution proof into maintained docs.

### DC02 — P2: the generated shared-provider contract is not implementation-ready

`src/App/CanDoItAll.Web/Api/SharedProviderInferenceOpenApiContract.cs:45` emits only an object schema and prose description for each request; no properties, required model/content fields, or unknown-field rules describe the actual accepted subset. At lines 156-159, that prose is already stale: Chat Completions omits `stream_options` and `reasoning_effort`; Responses omits `parallel_tool_calls`, `store`, `background`, and `reasoning`. The actual allowlists are in `src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayRequestPolicy.cs:21` and `:38`.

A read-only inspection of the existing localhost:5032 OpenAPI document also found empty schemas `{}` for custom-serialized protocol scalar/enum types, including `SharedProviderSourceInstanceId` and `SharedProviderTransport`. Their serializers emit constrained strings, e.g. `SharedProviderIdentifiers.cs:158` and `SharedProviderCatalogContracts.cs:503`. No schema transformer for these types is registered in `src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs:55`. Clients generated from these schemas cannot learn the scalar wire types or valid enum tokens.

This is a contract-completeness defect, not evidence that current relay requests fail. The live host revision was not attested; source inspection confirms the missing schema transformation and request descriptions at the reviewed head.

Minimal repair: keep HTTP schema metadata in Web; add explicit scalar/enum schema mapping for the protocol types and bounded per-operation request schemas matching the existing relay policy. Preserve protocol/runtime ownership; do not replace working runtime models or relax validation to fit the schema. Document non-stored Responses behavior, reasoning controls, image base64-only output, supported streaming frames, and denied routes/features. Freeze these contracts before export.

Validation must assert schema semantics and representative positive/negative payload conformance, not just route presence. Existing tests `SharedProviderCatalogApiIntegrationTests.OpenApi_DescribesCatalogAndModelsOperationsAndResponses` (line 282) and `SharedProviderOpenAiCompatibilityIntegrationTests.OpenApi_ContainsExactlyThreePostSurfacesWithoutAudioOrEtag` (line 968) check operation/header/content presence but do not establish the above scalar or payload contract. The originally planned `SharedProviderOpenApiIntegrationTests` class does not exist; do not retain its planned count of 10 as if it were discovery evidence.

### DC03 — P2: SharedInfo publishes an older API contract and no shared-provider skill

Canonical source package:

- `../CanDoItAll.SharedInfo/codex/skills/_candoitall-api-shared/references/candoitall-web.openapi.json`
- `../CanDoItAll.SharedInfo/codex/skills/_candoitall-api-shared/manifest.json`
- `../CanDoItAll.SharedInfo/codex/skills/_candoitall-api-shared/README.md`

The manifest records branch `simple-chats`, baseline commit `827fa425c30404ab910a363962d00fd1479f87c1`, `workingTreeClean: false`, generation date 2026-08-18, 270 paths, 302 operations, 467 schemas, SHA-256 `376E5EA35D4C5FF99FEDC32012E67F18033F54245C10CE0C054FF0FAE997B797`. It correctly labels its old dirty-tree provenance, but contains no shared-provider route or schema. The planned maintained `candoitall-api-shared-providers` skill is absent.

Read-only observation of existing localhost:5032 returned 276 paths, 308 operations, and 486 schemas. Both runtime document endpoints returned identical text, with the canonical `http://localhost:5032/` server URL. The snapshot lacks these live paths:

- `GET /api/shared-providers/v1/catalog`
- `GET /api/shared-providers/openai/v1/models`
- `POST /api/shared-providers/openai/v1/responses`
- `POST /api/shared-providers/openai/v1/chat/completions`
- `POST /api/shared-providers/openai/v1/images/generations`
- `GET /api/workflows/external-response-operations/{operationId}`

This live comparison is drift evidence only, not a final-source export. No artifact was replaced or published. Recount the complete final document rather than assuming only five additions; the shared snapshot also missed the workflow route.

Executed the existing SharedInfo `tools/validation/Test-CanDoItAllWebOpenApi.ps1`: exit 0, Passed, zero findings against the old snapshot. That proves internal hash/count/appendix consistency, not parity with the product head. Add final runtime-vs-snapshot comparison to closure evidence.

Minimal repair after product contract freeze: capture both runtime endpoints from a freshly built, identified final host; compare bytes, hash, count all routes/operations/schemas, regenerate manifest operation sets and provenance, update support README, create `codex/skills/candoitall-api-shared-providers/SKILL.md` plus focused references and appendix, and refresh affected Agents/LLM Chats/Workflows guidance. Do not hand-edit generated OpenAPI JSON.

### DC04 — P2: the original documentation/export closure is still explicitly unfinished

`codex/bundles/shared-providers/STATUS.md:4` remains `BLOCKED_SB07_TEST_BUDGET_AUTHORITY`; SB10 operator docs, SB11 OpenAPI/SharedInfo, and SB12 final closure remain LOCKED. `subbundles/SB11-openapi-export-sharedinfo-skills/SESSION-HANDOFF.md:3` states `NOT_EXECUTED`. The original README expressly says the later two-instance acceptance does not close its distinct three-application SB07 gate. Do not treat later feature-bundle success as closure of that contract.

The provider-history bundle records completion on 2026-08-29; its closure evidence predates head's `20260830104752_AddProviderHistoryExternalReference` migration and associated follow-up behavior. Historical reports are useful baseline evidence, but not final reviewed-head verification.

The new repair bundle should explicitly own the remaining documentation, API export, SharedInfo update, and current-head validation. Reconcile/supersede historical locks in a traceable handoff; do not silently change old budget authorizations or claim the old three-instance gate passed. No Docker lifecycle was started in this review.

## Required durable documentation update

Only `docs/llm-chats-api.md` and `docs/operations/containers.md` changed under docs between development and reviewed head. There is no maintained sharing/history guide equivalent to the extensive bundle-only instructions. Keep the product-owned behavior in product docs:

| Target | Required content |
| --- | --- |
| New `docs/shared-providers.md` | Publication versus local profile/source/import, hybrid selection, routing IDs, refresh/reconciliation/deletion, availability, scopes, supported OpenAI subset, cancellation, errors, SSRF/private-network policy, safe credential configuration. |
| New `docs/provider-request-history.md` | Provider/global lazy search, metadata/content/manage authorities, caller versus managed credential identity, opaque external reference, canonical source ownership, Light/Detailed differences, explicit unknown usage/price, retention/quota, maintenance/outbox/backfill failure diagnosis and privacy. State current unsupported federation/body export/wire replay honestly. |
| `docs/api-control-plane.md` | Five shared-provider operations and exact catalog/invoke scope rules; typed access-reference headers; native versus OpenAI error envelope. Distinguish administration/history UI services from exposed HTTP routes. |
| `docs/provider-capability-and-pricing.md` | Shared catalog capability/model thinking metadata; configured-free versus missing price; immutable execution price provenance; public sanitized display versus internal route identity. |
| `docs/architecture/overview.md` and architecture index | ProviderManagement ownership, Integration protocol/HTTP adapters, neutral ProviderHistory contracts/application/persistence and inward dependency direction. |
| `docs/secure-configuration.md`, `docs/operations/backup-and-restore.md` | Managed-token attribution, redaction limits, Detailed content risk, database plus canonical history/file-journal/DP-key backup boundaries, cleanup and recovery. |
| `src/Foundation/CanDoItAll.Migrations.PostgreSql/README.md` | Seven additive shared-provider/history migrations, schema/model/transfer alignment, deployment and rollback-backup instructions, focused upgrade validation. |
| Root `README.md`, `docs/README.md`, Web README, six missing project READMEs | Discoverability and accurate ownership/build/validation entry points. |

Scope constants live in `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccessScopeNames.cs:25`: `api.shared-providers.catalog.read`, `api.shared-providers.invoke`, `api.provider-history.read`, `api.provider-history.content.read`, `api.provider-history.manage`. Route registration currently maps only catalog/inference endpoints for this feature; do not invent remote history or source-management APIs when documenting these scopes.

## SharedInfo source package handoff

SharedInfo owns reusable API skills and the single generated OpenAPI snapshot. Product owns implementation, API schema generation, durable product docs, export adapter if one is needed, and this repair bundle/proof. Inspected canonical guidance: `AGENTS.md`, `docs/architecture/source-of-truth.md`, `docs/standards/documentation.md`, `docs/standards/codex.md`. No SharedInfo changes were made.

Future SharedInfo targets:

1. The three `_candoitall-api-shared` files listed under DC03, including all route-family counts and documented operation sets.
2. New `codex/skills/candoitall-api-shared-providers/SKILL.md` and focused protocol/operations reference. It must link the shared artifact and provenance, prefer target live OpenAPI on version mismatch, describe exactly five implemented operations, access-context semantics and both error envelopes, and distinguish source/import UI setup from remote inference.
3. `codex/skills/candoitall-api-agents/SKILL.md`: shared local profiles, caller/history interpretation, and corrected reasoning guidance. Its older provider test SSE route remains a status stream, even though the underlying provider contract now supports token streaming; do not relabel that route as token streaming.
4. `codex/skills/candoitall-api-llm-chats/SKILL.md`: imported provider/model selection and canonical-history ownership. Do not merge governed agent conversations with ordinary Simple Chats.
5. `codex/skills/candoitall-api-workflows/SKILL.md`: verify the external-response operation route and final route-set parity against the refreshed artifact; update only actual drift.
6. `tools/validation/Test-CanDoItAllWebOpenApi.ps1` and related skill-index validation as required for new operation set/skill. Existing metadata-driven parity should be extended instead of duplicated.

Validation: SharedInfo `./tools/validation/Test-CanDoItAllWebOpenApi.ps1`, `./tools/validation/Test-SharedInfo.ps1`, current Codex skill package validator; inspect `./tools/install/codex/Install-CodexSkills.ps1 -PackageName _candoitall-api-shared,candoitall-api-shared-providers,candoitall-api-agents,candoitall-api-llm-chats,candoitall-api-workflows -WhatIf` before any authorized active-copy synchronization. Do not modify installed copies in place as the source of truth.

## Distinguish API schema export from database schema evidence

The original bundle explicitly promised OpenAPI export. Treat that as the primary meaning of the requested new schema. PostgreSQL is a separate contract and should receive migration/export verification because the branch adds seven migrations:

- `20260824224847_AddSharedProviderPersistence`
- `20260828153731_AddProviderInvocationPriceEvidence`
- `20260828164039_AddProviderRequestHistory`
- `20260828175631_AddProviderHistoryCallerAttribution`
- `20260828195043_AddProviderHistoryCanonicalEvidence`
- `20260828205045_AddProviderHistoryLocatorsAndChatCaller`
- `20260830104752_AddProviderHistoryExternalReference`

No durable schema-only export tool or committed SQL schema snapshot was found. Do not introduce a second authoritative schema: migrations plus `AppDbContextModelSnapshot.cs` remain authoritative. Use EF-generated idempotent SQL as product-owned review/deployment evidence and retain a schema-only snapshot only if required by rollout practice. Never copy database schema, data, or provider credentials into the SharedInfo OpenAPI package.

After repair migrations are final, use the existing design-time factory and tools:

```powershell
dotnet ef migrations has-pending-model-changes --project src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --context AppDbContext
dotnet ef migrations script --idempotent --project src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --context AppDbContext --output artifacts/providers-shared-premerge/schema/postgresql-idempotent.sql
```

Generate into the product's ignored artifacts directory; create that exact directory first. SQL generation is not database application. Do not edit applied migrations to satisfy a snapshot check. Use isolated databases, never the user's live active profile, and distinguish two required baselines:

1. Exact development-to-final upgrade: development ends at `20260822013043_AddWorkflowNativeCheckpointRequestUniqueness`. Seed only records that exist there: local provider profiles/configuration and canonical agent/Simple Chat/workflow evidence. Assert preservation and correct creation/constraints/backfill of the new sharing/history schema. Publication/import/provider-history rows cannot be seeded at this pre-feature baseline; creating current tables first would invalidate the rehearsal.
2. Reviewed-feature-head-to-repairs preservation: start at `20260830104752_AddProviderHistoryExternalReference`. Seed populated publications/sources/imports, history entries/details/owners, policy/quota and relevant transfer state. Apply any actual repair migration and prove preservation. If no schema repair is required, record an empty migration delta and execute populated persistence/retention/transfer regression instead; do not create a migration merely for this phase.

For a baseline-specific SQL artifact, add the relevant baseline migration ID as the positional `from` argument immediately after `dotnet ef migrations script` in the export command above, retain `--idempotent`, and choose a distinct output filename. Omit `to` only when the final migrations list has been frozen, so it resolves to that recorded latest migration. The complete all-migrations script remains useful separately; its existence does not prove either upgrade lane.

Focused integration targets already present: `MigrationBootstrapIntegrationTests` (in `DatabaseMigrationIntegrationTests.cs`), `SharedProviderPersistenceIntegrationTests`, `ProviderHistoryPersistenceIntegrationTests`, `ProviderHistorySourceProjectionIntegrationTests`. Discover and freeze actual test counts before execution. Add exact-baseline tests for the two distinct obligations above where existing coverage does not prove them. The current older upgrade test begins before Simple Chats rather than at the shared-provider development baseline. SB09 may reuse matching SB08 evidence; rerun only if source/config/schema changes invalidate it.

## Review validation record and limits

- Product documentation validator: FAILED, six concrete missing READMEs.
- SharedInfo existing OpenAPI snapshot validator: PASSED, only proves old artifact consistency.
- Source/diff inspection: verified seven new migrations, five shared-provider routes, API allowlist/schema gaps, absent dedicated API skill, and explicitly unexecuted legacy export phase.
- Existing localhost:5032 read-only probes: runtime Ready; both OpenAPI URLs HTTP 200 and equal text; counts/delta recorded above. No host restart or final-build identity claim.
- No product fixes, migration execution, live/provider requests, Docker lifecycle, shared-repository writes, snapshot publication, installation, commit, or merge occurred in this sub-review.

## Exact export metadata and capture commands

Refresh the existing manifest shape rather than inventing a replacement:

- `schemaVersion`.
- `source.repository`, `source.branch`, `source.commit`, `source.workingTreeClean`, and, for an uncommitted capture, `source.workingTreeNote` and `source.workingTreeStatusSha256`.
- `source.captureNote`, `source.webProject`, `source.environment`, `source.generatedUtc`, `source.runtimeDocumentPaths`.
- `artifact.file`, `artifact.sha256`, `artifact.openapiVersion`, `artifact.documentTitle`, `artifact.documentVersion`, `artifact.serverUrl`, `artifact.pathCount`, `artifact.operationCount`, `artifact.schemaCount`.
- Every `routeFamilies` entry's `prefix`, `pathCount`, and `operationCount`, accounting for all document paths and operations.
- `documentedOperationSets`: exact skill-file link, prefix, method/path/operationId triples, and route-appendix marker, including the new shared-provider set.

No existing product OpenAPI export adapter was found; the maintained support README prescribes runtime capture. The product-owned capture phase can use these commands after final source/host identity is established, not against an arbitrary existing process:

```powershell
New-Item -ItemType Directory -Force -Path artifacts/providers-shared-premerge/openapi
Invoke-WebRequest -Uri http://localhost:5032/openapi/v1.json -OutFile artifacts/providers-shared-premerge/openapi/openapi.json
Invoke-WebRequest -Uri http://localhost:5032/swagger/v1/swagger.json -OutFile artifacts/providers-shared-premerge/openapi/swagger.json
Get-FileHash -Algorithm SHA256 -LiteralPath artifacts/providers-shared-premerge/openapi/openapi.json,artifacts/providers-shared-premerge/openapi/swagger.json
```

Compare actual file bytes as well as SHA-256, calculate counts from captured JSON, and record command/build/source identity. If authentication is enabled, obtain a properly scoped credential through authorized setup and pass it without recording its value; do not turn authorization off to capture. Final SharedInfo replacement is a subsequent bundle work unit, not an effect of these product artifact commands.


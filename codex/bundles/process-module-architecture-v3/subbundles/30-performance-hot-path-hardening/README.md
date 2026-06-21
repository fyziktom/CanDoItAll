# SB30 Performance Hot Path Hardening

## Status

Completed on 2026-06-17.

## Objective

Apply the Process module .NET performance guardrails to the current implementation after SB29, focusing on hot-path projection/catalog shaping and process-related AgentFramework harness parsing.

## Covered Inputs

- User request on 2026-06-17 to use `candoitall-bundle-workflow` and `optimizing-dotnet-performance`.
- Existing v3 Process architecture bundle and its `architecture/19-dotnet-performance-guardrails.md`.
- Existing post-bundle process data performance hardening proof.
- Active process implementation in `CanDoItAll.Processes.Application` and related AgentFramework process harness code.

## Prerequisites

- SB29 generic runtime-dispatcher leak repair is complete.
- Existing performance guardrails and validation checklist are present in the v3 bundle.
- Active Process Application and related process harness source files build before implementation starts.
- Focused unit tests are available for Process catalog projections and runtime projection pipelines.

## Pass 1: Initial Performance Review

The current implementation had already fixed the highest-risk EF and event-store issues. The remaining actionable risks were allocation and deterministic-comparison issues in projection services:

- Definition/template catalog projections rebuilt stable item lists on each shell refresh.
- Catalog sorting/searching used current-culture comparisons, which are slower and locale-dependent for template/catalog identifiers.
- Runtime shell metric/tool-usage projections used grouping and repeated group enumeration on every dashboard refresh.
- Process mock/scenario harness code used static compiled regexes for literal parse patterns.

The architectural improvement is to keep source-of-truth loading unchanged but cache derived stable projections per service instance, use ordinal comparisons for catalog data, and use single-pass accumulators for runtime dashboard projection shaping.

## Pass 2: Deep Pattern Scan

The deep scan followed `analyzing-dotnet-performance` recipes over the modified hot-path files. Final scan counts are recorded at `bundle://proof/SB30-performance-hot-path-hardening/transcripts/performance-scan.txt`.

Key final confirmations:

- Sync-over-async: 0.
- Missing non-linguistic `StringComparison`: 0.
- `RegexOptions.Compiled`: 0.
- `new Regex(`: 0.
- `new JsonSerializerOptions` in modified scope: 0.
- `[GeneratedRegex]`: 5.
- Unsealed leaf candidates in modified production scope: 0.

## Proposed Architecture Improvements

1. Stable catalog projections should be cached at the application service boundary because `ProcessTemplatePackLoader` already treats the pack as a lazy immutable source for the service lifetime.
2. Catalog identifier, template title, and summary filtering should use ordinal ignore-case comparison for deterministic Process behavior across host cultures.
3. Runtime dashboard aggregates should be built from explicit accumulators so the projection service owns bounded single-pass shaping instead of LINQ grouping chains.
4. Process mock/scenario parser regexes should be source-generated to remove compiled-regex startup cost and keep the code AOT/trimming-friendly.

## Deliverables

- Cached definition catalog projection source list.
- Cached template catalog source item list.
- Ordinal catalog sort/search behavior.
- Explicit runtime metric and tool usage accumulators.
- Source-generated process mock and scenario harness regex methods.
- Focused unit tests covering culture-independent catalog search and runtime dashboard aggregation.
- SB30 proof manifest, semantic invariants, changed-file hashes, scan summary, and command proof.

## Dependency Impact

- Process UI and live dashboard projections keep the same DTO contracts.
- Template pack loading semantics remain unchanged; stable derived lists are cached consistently with the existing lazy pack loader.
- AgentFramework process mock/scenario behavior keeps the same regex patterns and parser outputs while changing regex construction.
- No persistence schema, runtime state, route, or public API contract changes are introduced.

## Validation Depth

- Build `CanDoItAll.Processes.Application`.
- Build `CanDoItAll.Modules.AgentFramework`.
- Run focused unit tests for Process catalog projections and runtime projection pipeline behavior.
- Run modified-scope performance scan from `validation/05-dotnet-performance-antipattern-checklist.md`.
- Run static regression scan for sync-over-async, current-culture catalog comparison, compiled/per-call regex, and per-call HTTP client regressions.

## Implementation Steps

1. Run Pass 1 direct performance review over active Process hot paths.
2. Run Pass 2 deep pattern scan with `analyzing-dotnet-performance`.
3. Cache stable definition and template catalog source projections.
4. Replace locale-dependent catalog comparisons with ordinal comparisons.
5. Replace runtime dashboard grouping chains with explicit accumulators.
6. Convert process mock/scenario literal regex patterns to `[GeneratedRegex]`.
7. Add behavior tests for deterministic catalog search and runtime aggregation.
8. Run focused builds, tests, performance scan, static scans, and record proof.

## Do Not Do

- Do not change Process runtime semantics, persistence schema, event contracts, or UI DTO shapes for this hardening pass.
- Do not hide missing evidence behind broad residual-risk prose.
- Do not replace readable bounded authoring code with speculative micro-optimizations.
- Do not introduce fallback behavior if template pack loading or projection construction fails.

## Implementation Subbundles

| Slice | Scope | Status |
| --- | --- | --- |
| SB30-A Catalog Projection Cache And Determinism | Cache definition/template source projections and switch catalog sort/search to ordinal comparisons. | Completed |
| SB30-B Runtime Dashboard Aggregation | Replace metric/tool-usage grouping chains with explicit accumulators and deterministic sorting. | Completed |
| SB30-C Process Harness Regex Generation | Convert process mock and scenario harness literal regex patterns to `[GeneratedRegex]`. | Completed |
| SB30-D Proof And Guardrail Validation | Add focused tests, builds, scan counts, hashes, semantic invariants, and manifest. | Completed |

## Exact Source References

- `repo://src/CanDoItAll.Processes.Application/ProcessDefinitionCatalogProjectionService.cs`
- `repo://src/CanDoItAll.Processes.Application/ProcessTemplateCatalogProjectionService.cs`
- `repo://src/CanDoItAll.Processes.Application/ProcessWorkspaceShellProjectionService.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ScenarioHarnessAgentRuntime.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDefinitionCatalogProjectionTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs`

## Out Of Scope

- EF/event-store changes already covered by `proof/post-bundle-process-data-performance-hardening`.
- BenchmarkDotNet microbenchmarks; the changes are structural hot-path cleanup with behavior tests, not measured throughput claims.
- Broad LINQ rewrites in authoring-only services where data is bounded and clarity is currently more valuable.

## Acceptance Checklist

- [x] Definition catalog stable source items are cached per service instance.
- [x] Template catalog source items are cached per service instance.
- [x] Catalog search/sort no longer uses `CurrentCultureIgnoreCase` in modified Process catalog services.
- [x] Runtime metric/tool usage projection no longer uses grouping chains for the modified dashboard aggregations.
- [x] Process mock/scenario literal regex patterns use `[GeneratedRegex]`.
- [x] Focused builds and unit tests pass.
- [x] Modified-scope performance scan and static regression scan are recorded.

## Validation

- `CanDoItAll.Processes.Application` build passed with 0 warnings and 0 errors.
- `CanDoItAll.Modules.AgentFramework` build passed with 0 warnings and 0 errors.
- Focused unit slice passed 34/34.
- Final modified-scope performance scan recorded zero critical findings.

## Proof Required

- `bundle://proof/SB30-performance-hot-path-hardening/manifest.md`
- `bundle://proof/SB30-performance-hot-path-hardening/semantic-invariants.md`
- `bundle://proof/SB30-performance-hot-path-hardening/changed-file-hashes.txt`
- `bundle://proof/SB30-performance-hot-path-hardening/transcripts/performance-scan.txt`
- `bundle://proof/SB30-performance-hot-path-hardening/transcripts/test-focused-performance.txt`
- `bundle://proof/SB30-performance-hot-path-hardening/transcripts/build-application.txt`
- `bundle://proof/SB30-performance-hot-path-hardening/transcripts/build-agentframework-module.txt`
- `bundle://proof/SB30-performance-hot-path-hardening/transcripts/static-regression-scans.txt`

## Browser Validation Logging

- Browser proof is not required because SB30 changes backend projection services and deterministic process harness parsing without changing Blazor markup or visual interaction behavior.

## Progression Gate

- SB30 is complete when the focused builds/tests pass, the modified-scope scan has no critical findings, source hashes are recorded, and the execution report references the proof artifacts.

## Suggested Agent Prompt

Execute SB30 from `codex/bundles/process-module-architecture-v3/subbundles/30-performance-hot-path-hardening`. Keep Process runtime semantics unchanged while applying performance guardrails to catalog projection caching, deterministic ordinal filtering, runtime dashboard aggregation, and process harness regex construction. Validate with focused Process Application and AgentFramework builds, focused unit tests, modified-scope performance scans, static regression scans, and artifact-backed proof under `proof/SB30-performance-hot-path-hardening/`.

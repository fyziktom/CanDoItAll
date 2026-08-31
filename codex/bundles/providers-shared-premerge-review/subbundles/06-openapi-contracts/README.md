# SB06 — OpenAPI contract semantics

## Status

- State: Completed
- Proof tier: Behavioral
- Execution: authorized 2026-08-30; current results and limits in reviews/01-execution-report.md and proof/SB09/manifest.md.

## Objective

Generated schema describes the actual supported HTTP subset and custom scalar/enum wire forms before export.

## Covered Inputs

- R06/R10; N02/N05/N06; DC02

## Prerequisites

- SB01/SB02 accepted wire/network behavior frozen. Coordinate any SB05 request-policy edits before final schema assertions.
- Read root constraints, analysis evidence and plan/02-validation-strategy.md before edits.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/Api/SharedProviderInferenceOpenApiContract.cs`
- `repo://src/App/CanDoItAll.Web/Api/SharedProviderCatalogOpenApiContract.cs`
- `repo://src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderIdentifiers.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderCatalogContracts.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayRequestPolicy.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderCatalogApiIntegrationTests.cs`

repo:// paths resolve from the product repository; bundle:// paths resolve from this bundle. Absolute SharedInfo references identify the inspected sibling checkout; resolve its actual root with the shared-standards skill when executing elsewhere. Planned new tests below are not claimed to exist.

## Deliverables

- Add Web-owned schema mappings for custom-serialized protocol identifiers/enums based on their converters and limits.
- Describe operation-specific properties, required fields, accepted nested shapes, limits and additionalProperties behavior matching runtime validation.
- Include stream_options/reasoning_effort and Responses reasoning/parallel_tool_calls/store/background semantics; preserve non-stored/stateless constraints and base64-only image result contract.
- Add semantic schema and representative valid/invalid request conformance tests; do not change runtime acceptance merely to simplify documentation.
- Keep exactly five implemented shared-provider operations; do not invent source administration or history HTTP endpoints.

## Dependency Impact

- Critical export foundation; unlocks SB07/SB08. Any wire/property changes invalidate schema snapshot and affected API skill guidance.
- Reopen on changes to: OpenAPI transformers, protocol converters/enums, allowlists, route registration, terminal/error forms.

## Validation Depth

- Proof tier: Behavioral.
- Test project/check selection: Integration SharedProviderCatalogApiIntegrationTests and SharedProviderOpenAiCompatibilityIntegrationTests.
- Selection reason: tests own the changed behavior and concrete regression; no unrelated suite substitutes for missing cases.
- Expected discovery: existing selected classes must be nonzero; enumerate and freeze their exact current FQNs/data-row counts before execution. The following exact named/scenario cases are required, with planned new-case counts where stated:
- OpenApi_CustomProtocolScalarsAndEnums_MatchWireShape (1)
- OpenApi_OperationSchemas_MatchAcceptedSubset (chat/responses/images = 3)
- OpenApi_UnsupportedFieldsAndFeatures_AreExplicit (1)
- Invalidation keys: OpenAPI transformers, protocol converters/enums, allowlists, route registration, terminal/error forms.
- Broad-gate decision: Focused contract tests here; generated/public-contract trigger covered at SB09.

## Acceptance Checklist

- [x] Protocol identifier/enum schemas express real string forms/tokens, not empty objects.
- [x] Representative accepted payloads conform; prohibited fields/values are documented and rejected consistently.
- [x] Generated errors/stream terminals match SB01; route/schema tests prove semantics beyond endpoint presence.
- [x] Keep strong identifiers/enums, explicit errors, safe logs, Egyptian braces and one statement per line.
- [x] No production XML comments, unrelated refactor, silent fallback or inferred permission expansion.

## Proof Required

- Follow plan/02-validation-strategy.md for exact Release build/discovery/test command form; record commands, exit codes, expected/actual cases, source hashes and dependency mode.
- Route counts or descriptions alone can pass with empty schemas; assert concrete property types, required sets, enum tokens and valid/invalid payload behavior.
- Record realistic positive and adversarial negative proof, source producer/consumer/lifecycle assertions where applicable, and anti-stub review. Failing-first proof must exercise the reported defect.
- Record evidence in reviews/01-execution-report.md; separate governed manifests are not required for this unit.

## C# Architecture Impact

Schema metadata belongs to Web adapter; protocol types remain framework-neutral. Reuse current OpenAPI extension mechanisms, no framework dependency added to Abstractions.

## Boundary Ownership

- Keep the responsibility in the named current owner. Any extraction must be independently testable and remove moved logic from the old class.

## Dependency Direction

- Preserve architecture/02-csharp-dependency-direction.md; no new project/reference is assumed. If needed, stop that edit and amend the boundary/checkpoint before proceeding.

## Pattern Decision

- Follow architecture/03-csharp-pattern-selection-records.md. Prefer current adapters/decorators and small functions; avoid abstractions without a concrete boundary.

## Testability Contract

- Pure policies use direct isolated tests; persistence/network behavior uses the selected integration seam and a real production consumer. Do not construct the full runtime for a pure rule.

## Partial Class Policy

- No new runtime partial. Existing generated code and cohesive UI code-behind are allowed; no nested service used to hide responsibility.

## Architecture Proof Required

- Relevant checkpoint: plan/architecture-checkpoints.md. Review .csproj diff, policy placement, production registration, independent tests and no-new-partial proof.
- If behavior is extracted, show old-owner shrink/thin facade and a negative test rejecting delegation back to the monolith. No extraction is required solely for this metric.

## Progression Gate

- Pass only after acceptance and required proof agree; otherwise record precise failed/blocked cases.
- Critical export foundation; unlocks SB07/SB08. Any wire/property changes invalidate schema snapshot and affected API skill guidance.
- Scope beyond the listed repair, new wire support, database destruction, hosted authority or installed-path permission must be handled explicitly; finish all unaffected authorized work first.

## Non-goals

- No merge/push/deployment, paid upstream call, unrelated sibling refactor, invented remote history API or broad UI redesign.

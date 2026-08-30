# SB02 — Consistent source network policy

## Status

- State: `Ready`
- Proof tier: Behavioral
- Execution: not started; this file is a plan, not proof.

## Objective

A loopback source accepted by discovery also invokes under the same network authority, without broadening private-network access.

## Covered Inputs

- R02/R10; N02/N04/N06; SP-02

## Prerequisites

- Reviewed baseline reconciled; coordinate SharedProviderSourceUriPolicy ownership separately from SB01.
- Read root constraints, analysis evidence and plan/02-validation-strategy.md before edits.

## Exact Source References

- `repo://src/App/CanDoItAll.Composition/SharedProviderRuntimeHttpClientSelector.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderSourceUriPolicy.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderCatalogClient.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderRuntimeProjectionIntegrationTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/SharedProviderSourceUriPolicyTests.cs`

repo:// paths resolve from the product repository; bundle:// paths resolve from this bundle. Absolute SharedInfo references identify the inspected sibling checkout; resolve its actual root with the shared-standards skill when executing elsewhere. Planned new tests below are not claimed to exist.

## Deliverables

- Characterize localhost, IPv4 loopback and IPv6 loopback with the default PublicOnly policy through discovery/import/materializer/mapper/runtime selector.
- Reuse the canonical loopback exception in destination selection; preserve bounded/DNS-pinned handler behavior and explicit private-network opt-in.
- Retain rejected public HTTP, non-loopback private HTTP, redirects and rebinding cases.

## Dependency Impact

- Critical foundation; unlocks SB05/SB06 and source setup docs. Any authority widening reopens protocol/source negative proof.
- Reopen on changes to: source URI normalization, HTTP selector/handlers, runtime mapping/materialization, source policy serialization.

## Validation Depth

- Proof tier: Behavioral.
- Test project/check selection: Unit SharedProviderSourceUriPolicyTests; Integration SharedProviderRuntimeProjectionIntegrationTests.
- Selection reason: tests own the changed behavior and concrete regression; no unrelated suite substitutes for missing cases.
- Expected discovery: existing selected classes must be nonzero; enumerate and freeze their exact current FQNs/data-row counts before execution. The following exact named/scenario cases are required, with planned new-case counts where stated:
- DefaultLoopbackImport_InvokesWithUnchangedPolicy (localhost/127.0.0.1/[::1] = 3)
- DefaultNonLoopbackHttp_RemainsRejected (public/private = 2)
- Invalidation keys: source URI normalization, HTTP selector/handlers, runtime mapping/materialization, source policy serialization.
- Broad-gate decision: No broad gate here; final composition checkpoint in SB09.

## Acceptance Checklist

- [ ] All three accepted loopback forms invoke a fake provider under default policy.
- [ ] Non-loopback HTTP remains rejected unless explicitly allowed by current policy.
- [ ] No persisted flag is silently changed to AllowPrivateNetwork and no transport bypass is introduced.
- [ ] Keep strong identifiers/enums, explicit errors, safe logs, Egyptian braces and one statement per line.
- [ ] No production XML comments, unrelated refactor, silent fallback or inferred permission expansion.

## Proof Required

- Follow plan/02-validation-strategy.md for exact Release build/discovery/test command form; record commands, exit codes, expected/actual cases, source hashes and dependency mode.
- Passing catalog download alone is insufficient: the same persisted imported graph must reach runtime dispatch.
- Record realistic positive and adversarial negative proof, source producer/consumer/lifecycle assertions where applicable, and anti-stub review. Failing-first proof must exercise the reported defect.
- Record evidence in reviews/01-execution-report.md; separate governed manifests are not required for this unit.

## C# Architecture Impact

Composition selects runtime clients; existing shared URI policy owns destination rules. Reuse or expose a narrow typed policy decision instead of cloning logic or adding permissive fallback.

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
- Critical foundation; unlocks SB05/SB06 and source setup docs. Any authority widening reopens protocol/source negative proof.
- Scope beyond the listed repair, new wire support, database destruction, hosted authority or installed-path permission must be handled explicitly; finish all unaffected authorized work first.

## Non-goals

- No merge/push/deployment, paid upstream call, unrelated sibling refactor, invented remote history API or broad UI redesign.

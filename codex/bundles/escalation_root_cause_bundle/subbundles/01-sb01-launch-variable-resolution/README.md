# SB01 - Launch Variable Resolution

## Status

- `Completed`
- Critical foundation: yes

## Objective

Introduce a deterministic launch-variable template resolver and enforce it before agent dispatch, tool-plan preflight, and rework packet generation. Tool-critical values must not reach an agent with unresolved placeholders such as `{CurrentProcessRunId}`, `${CurrentProcessRunId}`, or `{{CurrentProcessRunId}}`.

## Covered Inputs

- GPTPro launch placeholder finding.
- REQ-001, REQ-002, REQ-013, REQ-016, REQ-020.
- Incident evidence showing script refs under `artifacts/process-runs/{CurrentProcessRunId}/scripts/...`.
- Existing tests that currently assert unresolved placeholder output.

## Prerequisites

- Bundle prepared-stage validation passes.
- Current source references are refreshed if launch or Workbench process code changed.
- No production implementation from later subbundles is assumed.

## Exact Source References

- `bundle://codex/01-launch-variable-placeholder-resolution.md`
- `bundle://evidence/incident-facts.json`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/DotNetProcessLaunchVariableContributorTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`

## Deliverables

- `ILaunchVariableTemplateResolver` or equivalent cohesive service with explicit success/failure result.
- Placeholder support for `{Key}`, `${Key}`, and `{{Key}}`.
- Bounded recursive resolution with cycle detection.
- Tool-critical key classification for script refs, execution plans, side-effect manifests, product completion requirements, runtime tool requirements, subprocess evidence, managed artifact roots, and artifact refs.
- Central integration after launch variable contributors run and before agent prompt/tool execution.
- Tests updated so final launch context contains resolved script refs or explicit unresolved-placeholder diagnostics.

## Dependency Impact

- SB02 uses resolved values when aggregate diagnostics cite paths and receipts.
- SB04 depends on this to keep rework packets actionable.
- SB07 and SB11 depend on resolved .NET setup script refs.
- SB09 depends on unresolved placeholders becoming template/launch validation failures.

## Validation Depth

- Critical foundation with unit, integration, and negative tests.
- Semantic proof must show the resolver fixes the incident class and does not hide unresolved variables.

## Implementation Steps

1. Inventory all launch variable producers and consumers in the source references.
2. Define a strongly typed resolution result containing resolved variables, unresolved placeholders, cycles, and iteration count.
3. Implement token parsing for the three placeholder syntaxes without ad hoc string cascades scattered through callers.
4. Add bounded passes and cycle detection; unresolved tool-critical values fail predictably.
5. Define tool-critical key matching with constants or typed predicates, not duplicated magic strings.
6. Integrate resolver into `ProcessLaunchApplicationService` after all contributors complete.
7. Keep `ProjectStructureProcessLaunchVariableContributor` focused on producing the .NET setup variables; do not move generic resolver behavior into Workbench.
8. Update the existing unit/integration tests that expect unresolved script refs.
9. Add negative tests for unknown placeholder, cycle, non-tool-critical unresolved text, and tool-critical unresolved text.
10. Ensure diagnostics include the variable key and masked value/path context.

## Do Not Do

- Do not replace placeholders opportunistically only in the .NET solution setup path.
- Do not silently leave unresolved tool-critical values in place.
- Do not resolve by repeatedly calling `string.Replace` in unrelated services.
- Do not change template prose as the only fix.

## Acceptance Checklist

- [x] `DotNetCreateProjectScriptRef` resolves for the current process run.
- [x] `DotNetAddTestProjectScriptRef` resolves for the current process run.
- [x] Tool-critical unresolved values block dispatch with explicit diagnostics.
- [x] Cycles fail with a bounded, testable diagnostic.
- [x] Existing placeholder-expectation tests are intentionally updated.
- [x] No new dependency cycle is introduced.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- Failing-first test for unresolved `{CurrentProcessRunId}` in script refs.
- Passing resolver and launch integration tests.
- Source assertions for resolver placement and Workbench boundary.
- Changed-file hashes and anti-stub audit.

## Browser Validation Logging

- `N/A`; no browser surface is changed.

## Progression Gate

- SB02, SB04, SB07, SB08, and SB09 may start only after no tool-critical launch variable reaches execution unresolved.

## C# Architecture Impact

Adds a small deterministic service and possibly shared result records.

## Boundary Ownership

Application launch orchestration invokes the resolver; Workbench contributes variables; shared contracts are added only if runtime/rework packet code also consumes them.

## Dependency Direction

The resolver must not make `Processes.Runtime` reference Workbench or `Modules.Processes`.

## Pattern Decision

Use PSR-001: a small deterministic service with explicit result type and bounded resolution.

## Testability Contract

Resolver tests must run without database, MAF, process runtime, or file system setup.

## Partial Class Policy

No adapter partial changes are expected in this subbundle.

## Architecture Proof Required

- Placement rationale for resolver contract and implementation.
- Dependency check if any shared project references change.

## Suggested Agent Prompt

```text
Execute SB01 only. Implement deterministic launch-variable resolution and fail unresolved tool-critical placeholders before agent dispatch. Update the existing placeholder tests and add negative cycle/unresolved tests. Do not change production templates as the primary fix.
```

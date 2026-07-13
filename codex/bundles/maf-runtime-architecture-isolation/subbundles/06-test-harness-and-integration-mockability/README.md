# 06-test-harness-and-integration-mockability

## Status

- `Ready`

## Objective

Turn the extracted runtime seams into a practical testing base. Add or consolidate fake builders, mock providers, mock runtime tool providers, fake context contributors, fake workspace/MCP services, and diagnostics/metrics collectors so integration tests can target behavior without private reflection or unnecessary full-runtime setup.

## Covered Inputs

- M004, M009, M010
- R008, R009, R010, R012

## Prerequisites

- SB03 capability/provider composer extraction.
- SB04 provider/session/finalizer extraction.
- SB05 feature-driver extraction.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeProviderHealthTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs`

## Deliverables

- Test harness utilities for runtime build requests, fake providers, fake runtime tool providers, fake workspace services, fake MCP clients, fake context contributors, diagnostics sinks, and metrics sinks.
- Direct tests replacing reflection-heavy coverage for moved behavior.
- Integration tests that compose `MafAgentRuntime` with mocks/fakes at the new seams.
- Reflection-reduction report listing removed, reduced, and intentionally retained reflection tests.

## Dependency Impact

- SB07 depends on this harness to prove architecture closure and behavior parity.
- Future agent-specific bundles depend on these fakes to test scenarios without live providers or brittle private reflection.

## Validation Depth

- `Critical testability foundation`

## Implementation Steps

1. Inventory tests still using `BindingFlags.NonPublic`, `GetMethod`, `GetNestedType`, or repeated full-runtime setup for moved behavior.
2. Create shared test harness builders around SB02-SB05 contracts.
3. Migrate moved behavior tests to direct collaborator tests.
4. Add integration tests that swap in fake provider/session/tool/context/workspace/MCP dependencies.
5. Keep full-runtime smoke tests for public behavior.
6. Write reflection-reduction report and update proof.

## Scope Exceptions

- Reflection tests for behavior not moved by SB03-SB05 may remain with explicit justification.
- This subbundle does not change production behavior except where minor testability hooks are required by the extracted contracts.

## Do Not Do

- Do not add test-only branches to production runtime flow.
- Do not make collaborators public only for tests if `InternalsVisibleTo` or internal contracts are the local pattern.
- Do not mock what should be covered by direct pure unit tests.
- Do not delete full-runtime integration smoke coverage.

## Acceptance Checklist

- [ ] Test harness supports fake providers, runtime tool providers, context contributors, workspace/MCP services, diagnostics, and metrics.
- [ ] Moved behavior has direct tests.
- [ ] Reflection usage for moved behavior is removed or justified.
- [ ] Public runtime integration smoke tests remain.
- [ ] Execution report lists remaining testability gaps.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- `## Production Behavior Artifact Matrix` for any production test seam, diagnostic sink, or metrics sink introduced.
- Test transcripts for direct tests and integration tests with fakes.
- Reflection-reduction report.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- N/A unless UI-visible diagnostics are added.

## Progression Gate

- SB07 may start only after moved behavior is directly testable and integration tests can mock the extracted seams.

## Suggested Agent Prompt

```text
Implement SB06 only. Build the MAF runtime test harness around the extracted collaborators, migrate moved behavior away from private reflection tests, and prove integration mockability without adding test-only production branches.
```

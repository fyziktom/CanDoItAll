# SB07 Test Harness And Architecture Guards

## Status

- `Ready`

## Objective

Migrate tests to direct collaborators and add architecture guards that prevent regression into new `MafAgentRuntime` partial/nested builders, broad manager classes, or reflection-only tests.

## Covered Inputs

- N004, N006, N007
- MAF2-R011, MAF2-R012, MAF2-R014

## Prerequisites

- SB04 closure proof.
- SB05 closure proof.
- SB06 closure proof.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/CapabilityMigrationCleanupGuardTests.cs`

## Deliverables

- Tests migrated from `MafAgentRuntime` static/private behavior to extracted collaborators.
- Architecture guard tests that scan production source for forbidden nested builder/class patterns.
- Guard allowlist for any small nested exception/control-flow types that remain, with explicit justification.
- Test harness fakes for capability builders, workspace drivers, MCP drivers, provider services, and execution coordinators where needed.

## Dependency Impact

- SB08 closure depends on these guards.
- Without this phase, future work can accidentally recreate the original partial-class antipattern.

## Validation Depth

- Critical closure.
- Requires Semantic Adequacy Gate proof and anti-stub audit.

## Implementation Steps

1. Add guard tests for forbidden `MafAgentRuntime` nested builders/classes.
2. Add guard tests for constructors accepting `MafAgentRuntime owner`.
3. Add guard tests to prevent new broad `MafRuntimeManager`/catch-all service classes.
4. Migrate existing tests to extracted collaborators.
5. Keep only orchestration tests constructing `MafAgentRuntime`.
6. Capture source-scan transcripts matching guard intent.

## Scope Exceptions

- Full-suite unrelated failures remain documented, not fixed here unless caused by this refactor.

## Do Not Do

- Do not replace direct tests with reflection.
- Do not hard-code implementation-specific line counts as the only proof.
- Do not permit new runtime partial files without a narrow, temporary exception.

## Acceptance Checklist

- Guard tests fail if a new private nested `*Builder` is added under `MafAgentRuntime`.
- Guard tests fail if builders accept `MafAgentRuntime owner`.
- Most behavior tests target extracted services directly.
- Runtime construction is reserved for public adapter/orchestration tests.

## Proof Required

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- Focused unit test transcript.
- Source scan transcript.
- Anti-stub audit.

## Browser Validation Logging

- N/A: backend test and source-guard work.

## Progression Gate

- SB08 may start only after architecture guards are green and direct collaborator tests cover extracted behavior.

## Suggested Agent Prompt

```text
Implement SB07 only. Add architecture guards and migrate tests to the extracted collaborators. Do not weaken tests to fit the refactor; make the seams testable.
```

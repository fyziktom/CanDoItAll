# SB06 End To End Proof And Architecture Closure

## Status

- Status: `Completed`
- Criticality: `Critical closure`
- Depends on: SB04, SB05

## Objective

Prove the complete system: common MAF is domain-neutral, process scope can add instructions and suppress/require tools, skills, MCPs, and providers, and a management-only process step suppresses development capabilities without editing the agent default profile.

## Covered Inputs

- All normalized requirements.
- Final architecture validation for phased MAF-first and process-second refactoring.

## Prerequisites

- SB01 through SB05 complete.
- Proof manifests for SB01 through SB05 exist.
- No known blocking test failures from earlier phases.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf`
- `repo://src/Processes`

| Source | Required attention |
| --- | --- |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs` | Unit policy regression and new scope tests. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs` | Scoped instruction and assignment prompt proof. |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs` | End-to-end process launch/assignment proof. |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs` | Seeded development behavior ownership proof. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf` | Final text scan for domain leaks. |
| `repo://src/Processes` | Final dependency scan for process-to-MAF boundaries. |

## Scope

- Run targeted unit tests.
- Run targeted integration tests.
- Run build.
- Run source text scan for domain terms in common MAF.
- Run dependency scan or CodeAnalytics snapshot.
- Review context manifest proof for suppressed and required capabilities.
- Update `reviews/01-execution-report.md` and `reviews/csharp-architecture-gate.md`.

## C# Architecture Impact

This phase does not add new architecture. It validates that implementation matched the planned boundaries and did not create cosmetic separation only.

## Boundary Ownership

Closure must prove:

- Common MAF is generic.
- Process contracts are runtime-neutral.
- AgentFramework integration is the translation boundary.
- Development behavior is domain-owned.

## Dependency Direction

No forbidden references or cycles are allowed at closure.

## Dependency Impact

- Expected impact is verification-only unless closure exposes a defect requiring a repair subbundle.
- Downstream dependency is final bundle closure.

## Pattern Decision

Use artifact-backed proof and semantic invariants. Do not close based on build-only success.

## Testability Contract

Required scenarios:

- Management-only process step suppresses a development skill.
- Software-delivery step can require development image-analysis capability.
- Missing required capability blocks governed execution.
- Denied MCP server/tool is absent from attached MCP tools.
- Denied runtime provider/tool is absent from attached runtime provider tools.
- Common image analysis prompts remain generic.

## Validation Depth

- Unit, integration, build, text-scan, dependency-scan, and architecture-gate validation are all required.
- Browser validation is conditional on UI-visible changes only.

## Partial Class Policy

Review touched partial classes. Closure is blocked if behavior was merely moved into new partial files without focused collaborators or testability improvements.

## Implementation Steps

1. Run targeted unit tests.
2. Run targeted integration tests.
3. Run `dotnet build CanDoItAll.slnx`.
4. Run source text scans and dependency scans.
5. Inspect proof manifests and semantic invariants for SB01-SB05.
6. Complete `reviews/01-execution-report.md`.
7. Complete `reviews/csharp-architecture-gate.md`.

## Do Not Do

- Do not close with only prompt text tests.
- Do not ignore unrelated build failures without documenting ownership and approval.
- Do not accept provider suppression without context manifest or effective descriptor proof.
- Do not accept common MAF domain terms without classifying them as tests, docs, or acceptable generic names.

## Acceptance Checklist

- All requirements in `bundle://traceability/01-requirement-traceability.md` are closed.
- Targeted tests pass.
- Build passes or unrelated failure is documented with evidence.
- Text scans pass.
- Dependency scans pass.
- Architecture gate approves closure.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- Final test outputs.
- Final dependency scan output.
- Final text scan output.
- Updated `reviews/01-execution-report.md`.
- Updated `reviews/csharp-architecture-gate.md`.

## Browser Validation Logging

- N/A unless implementation adds UI-visible diagnostics or authoring fields.
- If UI changes are added, capture browser screenshots and console logs.

## Progression Gate

- Bundle can close only when SB06 proof and architecture gate are complete.

## Suggested Agent Prompt

```text
Execute SB06 only. Validate the whole MAF/process capability scope isolation bundle. Run tests, build, scans, dependency checks, and architecture gate. Close only with artifact-backed proof.
```

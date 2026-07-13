# SB10 - Capability-Aware Assignment And Preflight

## Status

- `Completed`
- Critical foundation: no

## Objective

Ensure process steps with deterministic tool plans or required runtime tools are assigned only to agents/executors with the required capabilities, and extend preflight so tool capability, scope, and composed invocation contract are checked before execution.

## Covered Inputs

- GPTPro template/agent combination analysis.
- REQ-011, REQ-015, REQ-016, REQ-017, REQ-018, REQ-020.
- Template capability and agent instruction sources.

## Prerequisites

- SB07 tool-plan guard complete.
- SB08 execution class schema complete.
- SB09 high-risk template migration available or in progress with compatible metadata.

## Exact Source References

- `bundle://analysis/04-template-agent-combination-analysis.md`
- `repo://Templates/Capabilities/tools.json`
- `repo://Templates/Capabilities/skills/instructions/dotnet-app-delivery.md`
- `repo://Templates/Capabilities/skills/instructions/blazor-ssr-delivery.md`
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/instructions.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeToolPreflightService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessCapabilityScopeContractTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchExecutorResolverTests.cs`

## Deliverables

- Capability metadata that maps execution classes and required tool plans to eligible agent/executor capabilities.
- Assignment validation that prevents deterministic tool-plan steps from going to generic agents without required tool capability.
- Preflight result that explains missing capability, denied scope, invalid args, invalid path, or missing manifest.
- Tests for assignment repair and capability mismatch.
- Updated template validation or process launch validation using typed capability metadata.

## Dependency Impact

- SB11 runtime-owned executor depends on clear deterministic versus agent-owned execution assignment.
- SB12 final validation must prove capability mismatch cannot reproduce the incident class.

## Validation Depth

- Unit tests for capability matching, assignment repair, and exact preflight denial.
- Semantic proof must show generic `.NET Application Developer` assignment cannot bypass deterministic tool-plan requirements.

## Implementation Steps

1. Inventory current capability metadata and agent instructions for .NET and Blazor delivery.
2. Define typed capability requirements for execution classes and tool plans.
3. Extend assignment/launch validation to compare required capabilities with agent/executor capabilities.
4. Extend preflight diagnostics to distinguish unavailable tool, unauthorized scope, invalid args, invalid path, and missing side-effect manifest.
5. Add assignment repair or explicit blocked diagnostics when no eligible executor exists.
6. Add tests where deterministic setup is assigned to an agent lacking `workspace_pwsh_run_script`.
7. Add tests where tool exists but scope/path/args are invalid.
8. Add tests where Blazor and screenshot templates require the correct tool set.
9. Update template audit proof with capability coverage.

## Do Not Do

- Do not infer capability from prose instructions.
- Do not let a generic agent receive deterministic runtime-owned work without capability proof.
- Do not collapse all preflight denials into a generic missing-tool message.
- Do not add broad fallback assignment.

## Acceptance Checklist

- [x] Execution class maps to required capability metadata.
- [x] Deterministic tool-plan steps reject incompatible agents.
- [x] Preflight diagnostics distinguish tool, scope, args, path, and manifest failures.
- [x] Assignment repair is explicit and logged.
- [x] Tests cover .NET setup, Blazor, and screenshot/writeback representative cases.

## Closure Proof

- `proof/SB10/manifest.md`
- `proof/SB10/semantic-invariants.md`
- `proof/SB10/transcripts/01-targeted-unit-tests.txt`: 36 targeted preflight/assignment tests passed.
- `proof/SB10/transcripts/02-adapter-preflight-tests.txt`: 3 adjacent adapter preflight tests passed.
- `proof/SB10/transcripts/03-modules-processes-build.txt`: `CanDoItAll.Modules.Processes` build passed with 0 warnings and 0 errors.
- `proof/SB10/transcripts/04-processes-application-build.txt`: `CanDoItAll.Processes.Application` build passed with 0 warnings and 0 errors.
- `proof/SB10/transcripts/05-source-assertions.txt`: SB10-CAP-001 through SB10-CAP-006 source assertions.
- `proof/SB10/transcripts/06-anti-stub-audit.txt`: no placeholder implementation markers found in changed SB10 files.
- CodeAnalytics snapshot `snap-20260708203629-184e6305` reported no scoped dependency cycles.

## Proof Required

- `proof/SB10/manifest.md`
- `proof/SB10/semantic-invariants.md`
- Capability mismatch failing-first tests.
- Passing assignment/preflight tests.
- Source assertions for capability metadata and assignment decisions.
- Anti-stub audit proving assignment does not read prose to infer tools.

## Browser Validation Logging

- `N/A`; no browser surface is changed.

## Progression Gate

- SB11 may proceed only after deterministic .NET setup cannot be assigned to an incapable agent/executor.

## C# Architecture Impact

Extends capability contracts and process launch/preflight logic.

## Boundary Ownership

Capability contracts belong in process contracts or capability metadata; assignment/preflight behavior belongs in runtime/application integration.

## Dependency Direction

Capability checks must not require runtime to parse agent instruction markdown.

## Pattern Decision

Use typed capability records and explicit matching service; avoid inheritance-heavy agent specialization.

## Testability Contract

Capability matching tests use small fixtures and do not need live agents.

## Partial Class Policy

No adapter partial changes expected unless preflight integration is currently adapter-owned.

## Architecture Proof Required

- Contract placement rationale.
- Negative test for prose-only capability inference.

## Suggested Agent Prompt

```text
Execute SB10 only. Add capability-aware assignment and exact preflight diagnostics using typed execution/tool metadata. Prove deterministic tool-plan work cannot be assigned to an incapable generic agent.
```

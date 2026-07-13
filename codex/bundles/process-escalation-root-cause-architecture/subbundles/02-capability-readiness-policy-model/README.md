# Capability Readiness Policy Model

## Status

- `Ready`

## Objective

- Define and enforce a typed process-step readiness contract for runtime tools, MCP tools, skills, suppressions, allowed operations, instruction fragments, and required receipts.

## Success Criteria

- Launch/matching and dispatch can report missing, denied, suppressed, or incompatible capabilities before the agent starts work.
- A step can suppress a globally available agent skill/tool without changing the agent's main settings.
- Readiness diagnostics distinguish process-scope denial from agent configuration, MCP availability, tool policy, and missing step contract.

## Covered Inputs

- R03 Capability Readiness Contract.
- R04 HR Matching And Preflight Surfacing.
- R06 Domain Isolation.
- User concern that agents may globally have development/project skills but a process step should do only management work.

## Prerequisites

- SB01 completed or at least its diagnostic model is available for readiness failures.
- Current `ProcessCapabilityScope` behavior is characterized before changing it.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeCapabilityScopeModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`

## Deliverables

- Step readiness contract model with required runtime tools, MCP tools, skills, suppressions, allowed operations, instruction fragments, and receipt gates.
- Readiness resolver that combines process definition, assignment, process scope, agent capabilities, MAF capability catalog, and driver contributions.
- Launch/matching diagnostics for hard blockers and warnings.
- Dispatch preflight guard that prevents starting impossible steps with actionable diagnostics.
- MAF context bridge that applies suppressions and includes only allowed scoped tools/skills/MCPs.

## Dependency Impact

- SB03 uses readiness diagnostics to decide whether manager recovery is possible.
- SB04 uses readiness contracts to isolate .NET delivery tools and proof requirements.
- SB05 uses readiness declarations to simplify templates.
- SB06 uses readiness proof to validate management-only suppression and browser-tool availability.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Characterize current `ProcessCapabilityScope` normalization and required receipt behavior.
2. Define the readiness contract with strongly typed identifiers instead of magic string maps where possible.
3. Implement a resolver with fakeable catalogs for tools, MCPs, skills, and process operations.
4. Add readiness diagnostics for missing, denied, suppressed, incompatible, optional, and unknown capabilities.
5. Surface readiness diagnostics in launch preview and HR matching output.
6. Apply readiness results to dispatch and MAF context assembly.
7. Add tests for management-only step suppression with a globally development-capable agent.

## Scope Exceptions

- Do not refactor .NET templates yet except to add minimal fixtures for readiness tests.
- Do not implement driver recovery decisions in this subbundle.

## Do Not Do

- Do not mutate global agent settings to satisfy step-level suppression.
- Do not rely on prompt text as the only capability limiter.
- Do not add hardcoded Playwright or .NET checks to generic readiness; represent them as capability identifiers supplied by domain steps.
- Do not create a heavy per-step service graph.

## Acceptance Checklist

- A missing runtime tool is reported before dispatch.
- A missing MCP tool is reported before dispatch.
- A denied browser/tool access policy is reported before dispatch.
- A globally available skill can be suppressed for a management-only step.
- A UI/browser proof step can explicitly require browser capability without forcing it on non-UI steps.
- Tests cover both launch preview and dispatch preflight.

## Proof Required

- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter Capability`
- `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter Process`
- Process launch preview sample showing a missing MCP/tool readiness blocker.
- Process launch preview sample showing a development skill suppressed for a management-only step.

## Browser Validation Logging

- N/A for UI rendering, but SB02 must record launch preview/API evidence paths in the execution report.

## Progression Gate

- SB03 may start only when readiness failures are typed, projected, and enforced before dispatch.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Create a typed readiness contract and resolver for process step capabilities. Prove missing tools, MCPs, skills, and suppressions with fake catalogs and integration tests. Keep the model generic and strongly typed, bridge it to MAF context assembly, update launch/matching diagnostics, and stop if any capability rule is enforced only by prompt wording.
```

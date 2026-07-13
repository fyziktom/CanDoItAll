# SB05 Development Tool Package Migration

## Status

- Status: `Completed`
- Criticality: `Critical ownership foundation`
- Depends on: SB01, SB02, SB04

## Objective

Move development-specific UI screenshot/image analysis behavior into a development-owned capability, tool provider, or project that can be required or suppressed by process scope without contaminating common MAF workspace tools.

## Covered Inputs

- Development-specific image analysis can exist, but it must have its own project or domain owner.
- Different processes may analyze images for non-development reasons.
- REQ-MAF-002, REQ-MAF-009, REQ-MAF-012.
- NFR-001, NFR-004.

## Prerequisites

- SB01 generic common prompt behavior complete.
- SB02 scoped policy can require/suppress development capability.
- SB04 process handoff can attach scoped instructions.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://Templates/Processes/processes/software-delivery`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`

| Source | Required attention |
| --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs` | Must not regain development prompts. |
| `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs` | Candidate contract for a development runtime tool provider. |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs` | Existing module-owned runtime provider pattern. |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` | Existing runtime provider pattern with domain ownership. |
| `repo://Templates/Processes/processes/software-delivery` | Candidate process-owned scoped instruction owner. |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs` | Existing seeded agent/process instruction assertions. |

## Scope

- Choose a development owner for UI screenshot image-analysis behavior.
- Candidate project: `src/MAF/Tools/CanDoItAll.AgentFramework.Tools.Development`.
- Alternative: a module-owned runtime tool provider registered by `CanDoItAll.Modules.AgentFramework`.
- Move or reintroduce UI screenshot prompt behavior only in that owner.
- Register the capability so process scope can require or suppress it.
- Ensure common MAF has no dependency on the development owner.

## C# Architecture Impact

This phase isolates domain behavior into a plugin/provider boundary. It should not add domain-specific branching to common workspace tools.

## Boundary Ownership

- Development tools own UI screenshot and software-delivery analysis prompts.
- Common MAF owns generic image file access and provider invocation.
- Process templates decide when development behavior is in scope.

## Dependency Direction

- Development project may reference AgentFramework tooling/capability abstractions.
- Application composition root may register both common MAF and development tools.
- Common MAF must not reference the development project.

## Dependency Impact

- Expected impact is a new or existing development-owned provider/capability plus composition-root registration.
- Common MAF should show only negative dependency impact: removal of domain ownership from the common wrapper.

## Pattern Decision

Use provider/tool isolation. Prefer a runtime tool provider or catalog skill over prompt switches. A process-scoped instruction fragment is acceptable when no new tool behavior is required.

## Testability Contract

- Tests proving development prompt is unavailable from common MAF defaults.
- Tests proving development capability can be required for a software-delivery step.
- Tests proving development capability can be suppressed for management-only steps.
- Dependency scan proving no common MAF reference to the development owner.

## Validation Depth

- Unit tests are required for any new provider/capability.
- Integration or seed tests are required for software-delivery ownership.
- Text and dependency scans are mandatory.

## Partial Class Policy

No new partials for the development tool package unless an existing file pattern requires generated split files. Prefer top-level provider and contract types.

## Implementation Steps

1. Choose the domain owner and document the choice in proof.
2. Implement or seed the development image-analysis capability.
3. Register it from an appropriate composition root/module.
4. Update software-delivery process scope to require or attach it where needed.
5. Add suppression tests for management-only steps.
6. Capture proof in `proof/SB05/`.

## Do Not Do

- Do not add `if softwareDelivery` logic to common MAF.
- Do not make common MAF reference the development tools project.
- Do not rely only on agent default instructions for the development behavior.
- Do not remove valid software-delivery process templates.

## Acceptance Checklist

- Development image analysis has a clear owner outside common MAF.
- Common MAF remains domain-neutral.
- Process scope can require/suppress the development capability.
- Tests and dependency scan prove ownership boundaries.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- Production Behavior Artifact Matrix for new project/provider/capability registrations.
- Text scan for common MAF domain terms.
- Dependency scan proof.

## Browser Validation Logging

- Only required if the development tool package changes browser-visible process behavior.

## Progression Gate

- SB06 may start when common MAF is clean and development behavior is owned and scope-controllable.

## Suggested Agent Prompt

```text
Execute SB05 only. Move development-specific UI screenshot/image analysis behavior into a development-owned capability or process-scoped instruction owner. Keep common MAF generic and prove process scope can require or suppress the new owner.
```

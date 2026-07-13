# SB13 Domain Policy Driver Isolation

## Status

- `Completed`

## Objective

Remove .NET/software-delivery receipt matching, step-key branching, and recovery guidance from generic adapter completion code by introducing explicit, composable policy contributions at the module/driver boundary.

## Covered Inputs

- `bundle://inputs/03-architecture-refactor-request.md`
- `bundle://07-domain-boundary-rules.md`

## Prerequisites

- SB12 closure gate passes and exposes stable policy seams.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/Drivers/DotNet/DotNetToolReceiptPolicyContribution.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessManagedArtifactEvidence.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/Drivers/DotNet/DotNetSolutionSetupRuntimeExecutor.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/Drivers/DotNet/DotNetSolutionSetupToolPlanGuard.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/Drivers/DotNet/DotNetWorkspaceCommandReceiptLifecycleFactExtractor.cs`

## Deliverables

- Strongly typed policy contribution contract for tool receipt semantics and recovery advice where extension is real.
- Generic built-in policy plus isolated .NET/software-delivery contribution registered at composition.

## Dependency Impact

- Prefer local module composition; any project-reference change requires bundle repair and dependency audit first.

## Validation Depth

- Critical architecture boundary with positive .NET matching, negative unrelated-tool matching, forbidden-token scan, and composition smoke.

## Implementation Steps

1. Inventory domain branches in extracted completion services.
2. Define the smallest contribution contract required by multiple policies.
3. Move .NET/step-specific behavior to the domain contribution.
4. Inject an immutable catalog into generic completion services.
5. Delete compatibility branches and tighten architecture tests.

## C# Architecture Impact

Creates an extension seam for domain tool semantics without contaminating generic runtime or dispatcher code.

## Boundary Ownership

Generic code owns policy orchestration; domain contributors own tool families, template selectors, and domain guidance.

## Dependency Direction

Generic services depend on the contribution contract, never on the .NET implementation.

## Pattern Decision

Strategy/catalog contribution because multiple generic/domain policy implementations are composed. Rejected: hardcoded switch, static helper referenced from generic code, or service locator.

## Testability Contract

Policy catalog and each contribution are tested without adapter construction, workspace, filesystem, provider, or host.

## Partial Class Policy

No partial classes.

## Architecture Proof Required

Forbidden-token scan, direct contribution tests, composition smoke, and refreshed dependency evidence.

## Do Not Do

- Do not move domain strings into a generic constants class.
- Do not silently fall back when no contribution matches.

## Acceptance Checklist

- Generic code contains no named .NET/software-delivery branches.
- Domain contributor preserves required behavior.
- Unrelated app/tool families are unaffected.

## Proof Required

- `bundle://proof/SB13/manifest.md`
- `bundle://proof/SB13/semantic-invariants.md`
- Failing-first/passing transcripts, hashes, source assertions, anti-stub audit, and refreshed CodeAnalytics proof.

## Browser Validation Logging

- N/A; backend architecture phase.

## Progression Gate

- SB14 may start only after generic-boundary and testability gates pass.

## Suggested Agent Prompt

Isolate domain-specific receipt and recovery semantics behind a composable driver policy and prove generic behavior remains application-neutral.

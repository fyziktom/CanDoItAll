# Tool Availability Profiles

## Status

- `Completed`

## Objective

Ensure software-development, QA, architecture, security, and business-analysis agents receive the tools they need through typed role/tool profiles while keeping least-privilege defaults.

## Covered Inputs

- `NOTE-06`
- `REQ-09`

## Prerequisites

- Default model migration is complete or queued.
- Current workspace tool access behavior is understood.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentWorkspaceToolAccessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Capabilities\CapabilityProofService.Rules.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\MafAgentRuntimeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs`

## Deliverables

- Typed tool profile ids or settings for software development, QA/review, architecture, security, and business analysis.
- Seed updates so role-specific agents get appropriate workspace/file/build/test/run/storage/process/project-structure capabilities.
- Tests proving dev/QA agents have required tools and non-dev agents do not receive mutation tools unintentionally.
- Documentation in execution prompts or tool descriptions if new constraints are introduced.

## Dependency Impact

- Process integration depends on the right agents having build/test/read/write/browser-adjacent tools during real delivery.

## Validation Depth

- Role/tool policy regression.

## Implementation Steps

1. Inventory current seeded agents and their workspace tool access settings.
2. Add or reuse typed profile settings instead of stringly role checks.
3. Apply profiles to seeds and process role assignment paths.
4. Add tests around `CreateConfiguredWorkspaceTools` or integration snapshots.
5. Verify approval requirements remain in place for mutation/external tools.

## Scope Exceptions

- Do not auto-grant write/build/run to all agents.
- Do not make browser tools mandatory for non-UI workflows.

## Do Not Do

- Do not bypass `AgentPermissionsPolicy.CanUseTools`.
- Do not make external target aliases global.
- Do not silently auto-approve external calls.

## Acceptance Checklist

- Software development agents can read/write/build/test/run within configured boundaries.
- QA/review agents can inspect artifacts and run validation without unnecessary mutation access unless explicitly assigned.
- Business analysis agents can read relevant files/artifacts and produce documents without developer mutation defaults.
- Tool profile behavior is strongly typed and test-covered.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter AgentFrameworkWorkspaceSeedIntegrationTests --no-restore -m:1`
- Targeted Maf runtime capability test proving expected tools attach for a dev/QA agent.
- Negative test proving a non-write agent does not receive write/build/run tools.

## Browser Validation Logging

- N/A unless agent editor UI exposes tool profiles.

## Progression Gate

- Process integration may continue once role-specific tool availability and least-privilege denial are proven.

## Suggested Agent Prompt

```text
Implement subbundle 06 only: add or repair typed tool profiles for dev, QA, architecture, security, and business roles. Preserve least privilege and prove both positive and negative tool attachment.
```

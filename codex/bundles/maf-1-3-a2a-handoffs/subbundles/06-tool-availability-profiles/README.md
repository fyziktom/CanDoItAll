# Tool Availability Profiles

## Status

- `Completed`
- Reopened and repaired `2026-05-03` after live run `cf086486-2424-487b-bd29-bfc3c111f307` showed configured tool exposure and runtime enforcement could diverge.

## Objective

Ensure software-development, QA, architecture, security, and business-analysis agents receive the tools they need through typed role/tool profiles while keeping least-privilege defaults.

## Covered Inputs

- `NOTE-06`
- `NOTE-10`
- `REQ-09`
- `REQ-13`

## Prerequisites

- Default model migration is complete or queued.
- Current workspace tool access behavior is understood.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentWorkspaceToolAccessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workspace\MafAgentRuntime.WorkspaceRuntimePlugin.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Capabilities\CapabilityProofService.Rules.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\MafAgentRuntimeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs`
- `C:\repositories\CanDoItAll\codex\bundles\maf-1-3-a2a-handoffs\inputs\03-live-process-tool-profile-regression.md`

## Deliverables

- Typed tool profile ids or settings for software development, QA/review, architecture, security, and business analysis.
- Seed updates so role-specific agents get appropriate workspace/file/build/test/run/storage/process/project-structure capabilities.
- Tests proving dev/QA agents have required tools and non-dev agents do not receive mutation tools unintentionally.
- Tests proving trusted governed process workspace-tool profile overrides affect both configured workspace tools and catalog `workspace-plugin` tools.
- Documentation in execution prompts or tool descriptions if new constraints are introduced.

## Dependency Impact

- Process integration depends on the right agents having build/test/read/write/browser-adjacent tools during real delivery.
- Runtime enforcement depends on the attached tool surface being filtered from the same effective workspace access profile.

## Validation Depth

- Role/tool policy regression.

## Implementation Steps

1. Inventory current seeded agents and their workspace tool access settings.
2. Add or reuse typed profile settings instead of stringly role checks.
3. Apply profiles to seeds and process role assignment paths.
4. Add tests around `CreateConfiguredWorkspaceTools` or integration snapshots.
5. Verify approval requirements remain in place for mutation/external tools.
6. Regression repair: pass effective workspace access into MAF tool construction and filter catalog `workspace-plugin` functions by that effective access.
7. Make host-denied scaffold/validation exceptions identify the effective workspace profile and the required profile repair.

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
- A governed process implementation run with persisted read-only agent settings can still attach software-development scaffold/build/test/run tools when trusted process metadata selects that profile.
- A read-only agent with catalog `workspace-plugin` no longer sees scaffold/build/test/run tools the runtime will deny.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter AgentFrameworkWorkspaceSeedIntegrationTests --no-restore -m:1`
- Targeted Maf runtime capability test proving expected tools attach for a dev/QA agent.
- Targeted Maf runtime capability test proving process-scoped software-development override attaches scaffold/build/test/run tools.
- Targeted Maf runtime capability test proving `workspace-plugin` is filtered by effective access.
- Negative test proving a non-write agent does not receive write/build/run tools.

## Proof Captured

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~MafAgentRuntimeTests" --no-restore -m:1`: passed; 40 tests.
- `dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore -m:1`: passed with existing NU1902 and NU1904 warnings.

## Browser Validation Logging

- N/A unless agent editor UI exposes tool profiles.

## Progression Gate

- Process integration may continue once role-specific tool availability and least-privilege denial are proven.

## Suggested Agent Prompt

```text
Implement subbundle 06 only: add or repair typed tool profiles for dev, QA, architecture, security, and business roles. Preserve least privilege and prove both positive and negative tool attachment.
```

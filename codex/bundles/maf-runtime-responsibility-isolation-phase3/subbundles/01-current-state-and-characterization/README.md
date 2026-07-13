# SB01 Current-State And Characterization

## Status

- `Ready`

## Objective

Establish an implementation baseline that maps every remaining MAF runtime responsibility to a target owner and adds or identifies characterization tests before risky code movement starts.

## Success Criteria

- Current-state inventory is updated from fresh source scans.
- Characterization tests are listed or added for every high-risk responsibility.
- CodeAnalytics evidence is recorded.
- No production behavior is refactored in this subbundle except test-only characterization additions.

## Covered Inputs

- R01, R02, R10, R12.
- Raw note: remaining isolation is incomplete and root causes must be found before more implementation.

## Prerequisites

- Prepared bundle exists.
- Repository builds enough for focused MAF tests, or current blockers are recorded.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.CatalogDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.RuntimeToolDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.RuntimeToolProviders.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`

## Deliverables

- Updated responsibility inventory.
- Fresh CodeAnalytics snapshot and focused symbol evidence.
- Characterization test list and any missing tests needed before SB02-SB06.
- Baseline line/member counts for hotspots.
- Baseline focused test timing.

## Dependency Impact

- All later subbundles depend on this inventory. If SB01 is incomplete, later source assertions and unit tests can miss behavior left in the old classes.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Run scoped CodeAnalytics for `CanDoItAll.AgentFramework.Maf`.
2. Refresh local scans for large files, partial classes, direct runtime construction, and `IServiceProvider` use.
3. Review current MAF unit/integration tests and mark which behavior is already characterized.
4. Add characterization tests only where existing behavior is not safely covered and movement would be risky.
5. Update `inventories/01-scope-inventory.md`, architecture files, and `reviews/01-execution-report.md`.

## Scope Exceptions

- Do not extract production behavior yet.
- Do not solve downstream hotspots such as `McpCapabilityBuilder` unless characterization requires a test reference.

## Do Not Do

- Do not implement the final architecture.
- Do not delete old runtime code.
- Do not add partial classes.

## C# Architecture Impact

Establishes the responsibility map and baseline metrics for all later architecture changes.

## Boundary Ownership

No new production boundary is created unless a characterization test requires a test fixture or fake.

## Dependency Direction

Record current dependency direction only. Do not change project references.

## Pattern Decision

No new production pattern is implemented. Pattern records in `architecture/03-csharp-pattern-selection-records.md` are validated against source evidence.

## Testability Contract

Characterization tests may use current broad seams. All later extracted-owner tests must be narrower.

## Partial Class Policy

Record existing partials. Do not add partials.

## Architecture Proof Required

- CodeAnalytics snapshot id.
- `rg` scan for partial classes and direct construction.
- Test list with purpose.
- Baseline focused test transcript.

## Acceptance Checklist

- [ ] Responsibility inventory maps each hotspot responsibility to a target owner.
- [ ] Missing characterization tests are explicit.
- [ ] CodeAnalytics snapshot and source scans are recorded.
- [ ] SB02-SB06 entry risks are clear.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- transcript for CodeAnalytics evidence or recorded MCP snapshot id.
- transcript for focused tests or explicit blocker.

## Browser Validation Logging

- N/A. Backend architecture and tests only.

## Progression Gate

- SB02, SB04, and SB05 may start only after the responsibility inventory and characterization plan are complete.

## Suggested Agent Prompt

```text
Execute SB01 only. Refresh the MAF runtime architecture baseline, update the responsibility inventory, add characterization tests only where needed before movement, and stop before production refactoring.
```

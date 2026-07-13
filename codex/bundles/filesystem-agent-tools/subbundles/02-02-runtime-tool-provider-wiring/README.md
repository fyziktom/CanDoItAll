# 02-runtime-tool-provider-wiring

## Status

- `Complete`

## Objective

Wire the extracted filesystem plugin into agent-visible tool registration, policy classification, and capability templates.

## Covered Inputs

- Make filesystem functions easier to find as shared/common tools.
- Expose missing file-service operations as agent tools.

## Prerequisites

- SB01 completed.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ToolCapabilityBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ToolCapabilityBuilder.ConfiguredWorkspace.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `repo://Templates/Capabilities/tools.json`
- `repo://Templates/Capabilities/manifest.json`

## Deliverables

- New tool names for directory listing, hash, zip, and unzip.
- Tool descriptions that clearly state file/folder support.
- Registry classifications and approval defaults.
- Capability template rows and seed version bump.

## Dependency Impact

- Unlocks final tests and runtime proof in SB03.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add tool constants.
2. Add registry metadata.
3. Wire builder/configured workspace tool set to filesystem plugin.
4. Add capability templates.
5. Update tests that enumerate expected capability keys.

## Scope Exceptions

Do not migrate module runtime providers in this phase.

## Do Not Do

- Do not duplicate tool names through `IAgentRuntimeToolProvider`.
- Do not make archive mutations read-only.

## Acceptance Checklist

- [x] New tool names are known runtime tools.
- [x] Mutation tools require approval by default.
- [x] Templates include the new tools.
- [x] Existing filesystem tools still attach.

## Proof Required

- Focused unit/template tests.

## Browser Validation Logging

- N/A.

## Progression Gate

- Passed. SB03 validation completed after catalog/template tests passed.

## Suggested Agent Prompt

```text
Implement SB02 only. Wire the extracted filesystem plugin into existing capability policy and templates.
```

## C# Architecture Impact

Thin composition wiring; no behavior reintroduction into broad runtime classes.

## Boundary Ownership

Tool builder owns construction, filesystem plugin owns file operations.

## Dependency Direction

No project references added.

## Pattern Decision

Catalog constants plus explicit composition.

## Testability Contract

Policy/template tests catch shallow registration.

## Partial Class Policy

No partial class allowed.

## Architecture Proof Required

Tool registration source assertions and approval classification tests.

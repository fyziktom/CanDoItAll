# 02 Workspace Tool Wiring

## Status

- `Completed`

## Objective

Wire the converter into the existing `workspace_convert_document` tool without expanding Maf runtime responsibilities.

## Deliverables

- `WorkspaceArtifactToolService` uses `IWorkspaceDocumentMarkdownConverter`.
- DI registrations in hosting and module composition.
- Fallback resolver updated.
- Tool description updated from Python MarkItDown wording to ManagedCode.MarkItDown.
- Unit tests with fake converter.

## Covered Inputs

- R003
- R004
- R005
- R006
- R007

## Prerequisites

- `01-managedcode-document-converter` passed.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceArtifactToolService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeDependencyResolver.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ToolCapabilityBuilder.ConfiguredWorkspace.cs`

## Dependency Impact

- Runtime composition receives the converter through DI.
- No new `MafAgentRuntime` methods or partial files are allowed.

## Validation Depth

- Critical runtime contract.

## Implementation Steps

1. Change artifact service constructor to accept the converter.
2. Convert success/failure result into existing workspace result and receipt.
3. Register converter in all composition roots.
4. Update fallback resolver.
5. Update tool descriptions.
6. Add fake-converter and DI tests.

## Do Not Do

- Do not leave `workspace_convert_document` using Python.
- Do not silently fallback to Python when ManagedCode conversion fails.

## Acceptance Checklist

- `workspace_convert_document` still returns source path, output path, preview, truncation flag, diagnostics, and receipt.
- Image rejection behavior is unchanged.
- Core does not reference Tools.Documents.

## Proof Required

- Focused artifact-service test transcript.
- DI validation transcript.
- Dependency scan transcript.

## Browser Validation Logging

- Not applicable for this subbundle.

## Progression Gate

- Continue only after runtime wiring tests and dependency-direction checks pass.

## Suggested Agent Prompt

```text
Wire the ManagedCode converter into workspace_convert_document without expanding Maf runtime responsibilities. Preserve tool contracts and receipts, add fake-converter tests, and validate DI.
```

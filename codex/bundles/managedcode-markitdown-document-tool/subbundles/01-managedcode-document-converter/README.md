# 01 ManagedCode Document Converter

## Status

- `Completed`

## Objective

Create a reusable C# document-to-markdown converter in `CanDoItAll.Tools.Documents`.

## Deliverables

- Core converter contract.
- ManagedCode.MarkItDown package reference.
- `ManagedCodeMarkItDownDocumentMarkdownConverter`.
- Unit tests for success and explicit failure.

## Covered Inputs

- R001
- R002
- R003

## Prerequisites

- Prepared bundle accepted by validator.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools`
- `repo://src/MAF/Tools/CanDoItAll.Tools.Documents`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`

## Dependency Impact

- Tools.Documents gains a package dependency and project reference to Core for contracts.
- Core must not gain a dependency on Tools.Documents.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add Core request/result/interface contracts.
2. Add the package reference.
3. Implement the converter with cancellation and explicit exception mapping.
4. Add direct converter tests.

## Do Not Do

- Do not expose ManagedCode types through Core contracts.
- Do not use Python or shell commands.

## Acceptance Checklist

- Converter writes markdown to the requested output path.
- Missing source files fail explicitly.
- Unsupported/conversion exceptions return clear diagnostics.

## Proof Required

- Focused converter test transcript.
- Tools.Documents build transcript.

## Browser Validation Logging

- Not applicable for this subbundle.

## Progression Gate

- Continue only after the concrete converter compiles and direct tests pass.

## Suggested Agent Prompt

```text
Implement only the ManagedCode document converter contract and implementation. Keep the third-party package isolated to Tools.Documents, add direct tests, and stop before runtime wiring.
```

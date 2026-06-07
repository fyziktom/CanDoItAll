# SB017 Proof Manifest

## Scope
- Subbundle: `SB017 - Subprocess mapping diagnostics`
- Objective: add typed diagnostics for mapping ambiguity, missing mappings, legacy mapping, and latest eligible artifact selection.

## Changed Sources
- `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessSubprocessArtifactSourceResolver.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessArtifactSourceResolver.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Proof
- Focused subprocess mapping diagnostic test: `bundle://proof/SB017/transcripts/subprocess-mapping-diagnostics-test.txt`
- Critical gate integration proof: `bundle://proof/SB018/transcripts/process-dispatch-artifact-subprocess-diagnostics-integration-tests.txt`
- Source assertions: `bundle://proof/SB018/transcripts/source-assertions.txt`

## Result
- Legacy string diagnostics are preserved.
- Typed diagnostics identify ambiguous mappings and latest eligible mapped artifact selection.
- Module adapter exposes typed diagnostics without adding projection writes to Core.

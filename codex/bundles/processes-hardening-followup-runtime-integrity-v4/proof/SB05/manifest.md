# SB05 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` introduces `IProcessArtifactContentReader` and `WorkspaceProcessArtifactContentReader` for storage-backed validation reads through the configured workspace root.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` resolves relative managed storage paths under the workspace root, rejects paths outside the workspace, reports missing files, and rejects files larger than `MaxProcessArtifactValidationContentBytes`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` validates JSON from stored bytes, validates readable text for YAML/Markdown, validates image content type and signatures, and requires stored content for file-backed evidence/runtime-proof artifacts.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` adds SB05 invariants for malformed relative JSON, missing relative content, and oversized relative content through the production reader.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Storage-backed artifact content read result | `WorkspaceProcessArtifactContentReader` from `ValidateRequiredCompletionArtifactsAsync` | Finalizer format validation and artifact validation diagnostic persistence | Per finalizer validation; no new durable state | SB05 tests reject malformed, missing, and oversized relative managed artifacts |
| Managed artifact text decoding | `TryDecodeManagedArtifactTextContent` | JSON/YAML/Markdown validation | Per artifact candidate read | Binary or invalid UTF-8 content is not silently treated as valid text |
| Managed artifact content diagnostics | `TryReadManagedArtifactContent` | `CreateArtifactValidationResult` and existing diagnostic persistence | Stored in the validation result payload when finalizer persists unsatisfied artifact diagnostics | Missing relative content returns a readable `could not be loaded` diagnostic |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB05/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB05/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB05/transcripts/changed-file-hashes.txt`

## Validation

Passed:

- `dotnet build tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~SB05_INV_001" --no-restore --no-build -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ArtifactContractValidation" --no-restore --no-build -v minimal`

Known unrelated warning noise during build: existing MSB3277 EntityFrameworkCore.Relational 10.0.0/10.0.4 conflicts.

## Blockers

None.

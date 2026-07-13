# SB01 Proof Manifest

## Changed Files

- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeArchitectureBaselineTests.cs`
- SHA-256 `C19EC8D15D05F03262A1C4C94BE76590AE8B164C0EC1E7DA1EBE4989C0673D7C` for `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeArchitectureBaselineTests.cs`

## Behavior Moved Out Of Adapter

SB01 established the exact adapter partial inventory and source assertions that later subbundles used as deletion gates.

## Tests Added Or Updated

- Test name: `ProcessRuntimeArchitectureBaselineTests`

## Test Transcript

- Passing transcript: `bundle://proof/SB01/transcripts/passing.txt`
- Failing-first: N/A process/non-production exemption; SB01 is a characterization gate and did not move production behavior.

## Build Transcript

- Managed build proof: `bundle://proof/SB01/transcripts/passing.txt`

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260709182007-390484e5`
- Dependency result: `cycles: []`

## Source Assertions

- Production search for old symbols returned no hits: `IDotNetSolutionSetupRuntimeExecutor`, `TryExecuteRuntimeOwnedDotNetSetupAsync`, `dotNetSolutionSetupRuntimeExecutor`, `IsDotNetRuntimeLifecycleTool`.
- Receipt writer search for `workspace_dotnet_run` and `workspace_dotnet_stop` returned no hits.

## Partial-Class Policy Proof

- Adapter partial file count: 20.
- No new adapter partial file was added.

## Domain-Boundary Source Assertion

- Generic process runtime/application hits are limited to typed repair route identifiers and are not .NET/Tetris/Calculator policy.

## Semantic Invariant Contract

- `bundle://proof/SB01/semantic-invariants.md`

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/passing.txt`

## Risks Left Open

- Adapter cluster remains large; this bundle blocks growth and removes targeted duplicated behavior but does not claim full adapter deletion.

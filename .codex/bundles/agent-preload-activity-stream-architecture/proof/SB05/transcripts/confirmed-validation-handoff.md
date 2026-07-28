# SB05 Confirmed Validation Handoff

## Provenance

- Class: `Confirmed handoff`
- Working directory: `C:\repositories\CanDoItAll`
- Run date: `2026-07-27`
- Original command lines: not retained
- Original raw console output: not retained
- Command: confirmed parent validation workflow handoff; original command lines were
  not retained
- ExitCode: 0
- Exit-code provenance: normalized successful outcome for the confirmed build/test
  results below; it is not reconstructed raw shell metadata
- Evidence status: results below were explicitly confirmed by the parent validation
  workflow; this artifact does not reconstruct them as raw transcripts

## Invariant correlation

The confirmed results below are cited by these SB05 invariant contracts:

- `SB05-PERF-001`
- `SB05-PERF-002`
- `SB05-EF-003`
- `SB05-STORE-004`
- `SB05-ARCH-005`

## Confirmed results

| Validation | Result | Source classes / artifact |
| --- | --- | --- |
| Full serial solution build | 0 errors, 166 warnings | `CanDoItAll.slnx` |
| Final startup matrix | 5/5 repetitions; four scenarios per repetition | `AgentFrameworkExecutionRunTrackingIntegrationTests.SendMessageAsync_records_current_startup_baseline`; `bundle://proof/SB05/startup-raw.md` |
| Generic new-run WAL crash matrix | 6/6 | `FileSandboxWorkspaceGenericNewRunCommitRecoveryIntegrationTests` |
| Combined generic/chat/update WAL and regressions | 33/33 | `FileSandboxWorkspaceGenericNewRunCommitRecoveryIntegrationTests`, `FileSandboxWorkspaceChatRunCommitRecoveryIntegrationTests`, `FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests` |
| Process snapshot/redaction group | 18/18 | focused process/provenance/redaction tests in the unit project |
| Activity admission/profile group | 11/11 | `CurrentProfileAgentExecutionActivityAdmissionTests` (5) plus `AgentExecutionActivityDependencyInjectionTests` (6) |
| Storage scaling/usage group | 10/10 | `FileSandboxWorkspaceAdmissionReadScalingIntegrationTests` (6 cases) plus `FileSandboxWorkspaceUsageProjectionIntegrationTests` (4) |
| Provider warm/synthetic/changed query proof | 1 / 0 / 3 SQL commands | provider snapshot focused validation |

## Suggested reproduction commands

These commands are reproducible suggestions. They are not asserted to be the
original executed commands.

```powershell
dotnet build CanDoItAll.slnx -m:1 -nodeReuse:false

dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --no-restore -m:1 --filter "FullyQualifiedName~FileSandboxWorkspaceGenericNewRunCommitRecoveryIntegrationTests|FullyQualifiedName~FileSandboxWorkspaceChatRunCommitRecoveryIntegrationTests|FullyQualifiedName~FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests"

dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --no-restore -m:1 --filter "FullyQualifiedName~FileSandboxWorkspaceAdmissionReadScalingIntegrationTests|FullyQualifiedName~FileSandboxWorkspaceUsageProjectionIntegrationTests"

dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build --no-restore -m:1 --filter "FullyQualifiedName~CurrentProfileAgentExecutionActivityAdmissionTests|FullyQualifiedName~AgentExecutionActivityDependencyInjectionTests"
```

## Interpretation

The counts are valid confirmed evidence for the A5 decision. They are not a substitute
for a retained TRX or raw console log when a later external verifier requires one.

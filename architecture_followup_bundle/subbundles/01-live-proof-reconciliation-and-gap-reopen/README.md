# Live proof reconciliation and gap reopen

## Purpose

Reconcile the previous closure claim with the actual checked-in artifacts, rerun missing proof, and reopen the live gap log from evidence instead of assumption.

## Required deliverables
- A written proof-gap memo comparing the claimed execution report with the currently checked-in `.trx` and browser artifacts.
- Fresh `.trx` artifacts that actually include the Process integration surface claimed for closure.
- An updated execution report section that lists the real proof and the still-open gaps.
- No production-code architecture changes beyond proof capture and any test scaffolding needed to prove the current state.

## Repository touchpoints
- `architecture_hardening_bundle/reviews/01-execution-report.md`
- `.codex-test-results/integration/integration.trx`
- `.codex-test-results/components/components.trx`
- `.codex-test-results/mcp-processes/mcp-processes.trx`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessImportMetadataIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`
- `tests/CanDoItAll.Mcp.Processes.Tests`

## Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj -v:minimal`

## Review questions
1. Do the checked-in proof artifacts actually show that the claimed Process suites ran?
2. Were missing proofs rerun and captured before reopening architecture work?
3. Is the reopened gap log based on artifacts rather than prior bundle prose?

## Corrective trigger

If the proof is still inconsistent, stop immediately. Do not reopen architecture work on top of untrusted evidence; create a corrective subbundle from the generic template and repair the proof record first.

## Corrective template

- `subbundles/_corrective-template`

## Detailed execution notes

- Compare `architecture_hardening_bundle/reviews/01-execution-report.md` with the live `.codex-test-results` contents.
- Explicitly record any mismatch between claimed commands and emitted `.trx` contents.
- Treat the current repository as the source of truth, not the previous execution report.
- If the previous report references proof that is not present in artifacts, rerun it and store the new artifacts before continuing.

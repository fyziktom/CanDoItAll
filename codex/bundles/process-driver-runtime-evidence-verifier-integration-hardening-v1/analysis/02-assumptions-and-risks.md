# Assumptions And Risks

## Working Assumptions
- The active checkout is on `maf-processes-refactor`; execution must verify against the current live source before trusting previous status rows.
- Prior bundle artifacts referenced by the architect are not present under `repo://codex/bundles/` in this checkout; SB001-SB003 must treat that as a re-entry risk and use current source, tests, scans, and fresh proof as the authority.
- No production runtime host, registry, selector, DI registration, manager command, scheduler hook, workflow hook, file/network read, storage write, or process mutation is approved in this bundle.

## Critical Path Risks
- A generic driver runtime could be introduced prematurely through innocent-looking registry, selector, host, provider, DI, manager command or scheduler/workflow hook names.
- Splitting the verifier could accidentally change diagnostic categories, severity, hash policy, redaction or audit fact behavior.
- A process adapter could silently start reading files, workspace paths or storage instead of accepting supplied payloads.
- A runtime evidence verifier could drift into process state validation with side effects rather than immutable descriptor consistency checks.
- Codex crash recovery may leave stale proof rows or missing changed-file hashes unless actively guarded.

## Validation Risks
- Build-only proof is insufficient. Critical gates must include semantic positive tests, adversarial negative tests, source scans, anti-stub audit and artifact-backed manifests.
- Shared transcripts are acceptable only for noncritical closure; production signal changes need subbundle-specific proof.
- Tests that only assert non-empty diagnostics or row counts are too weak.

## Reopen Triggers
- Any new `IServiceCollection`, `AddScoped`, `AddSingleton`, registry, selector, manager command, runtime host or process-driver DI token appears in production source.
- Any Core project references driver abstractions or modules.
- Any driver package references Modules, Infrastructure, AgentFramework, EF, UI, storage, workspace, or external connector packages.
- Any adapter uses `File.`, `Directory.`, `HttpClient`, shell/process execution, Graph/Office APIs, storage/workspace writes, claims/transitions/finalizer/retry mutation.
- Any diagnostic/audit output leaks raw secret/token/password/connection string/email content.
- Any subbundle status is complete without proof manifest for a critical gate.

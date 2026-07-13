# SB04 Semantic Invariants

## Packet Construction

- Packet generation starts from structured diagnostic codes and runtime receipts, not from prompt text.
- Required receipt names come from launch-variable contracts such as `ProductCompletionRequiredToolReceipts`.
- Failed readback details come from product completion diagnostics and `ProductCompletionRequiredFileContentChecks`.
- Resolved helper script refs come from assignment launch variables; unresolved placeholders are not emitted.
- The incident packet names `workspace_pwsh_run_script`, the resolved `DotNetCreateProjectScriptRef`, the `.slnx` readback failure, and the expected `src/Calculator/Calculator.csproj` membership.

## Runtime Behavior

- Safe/idempotent current-step retry receives `Runtime diagnostic rework instruction` before the next dispatch attempt.
- Manager escalation after a blocked result includes `Diagnostic repair packet` so budget-exhausted escalation retains attempted repair context.
- Operator rework uses the pre-rework receipt because `RequestStepRework` intentionally removes the reopened step receipts.
- Existing assignment repair services still run; diagnostic packet generation augments the operator reason instead of replacing assignment repair.

## Anti-Stub Audit

- Packet tests assert individual facts and absence of `{CurrentProcessRunId}` rather than matching a brittle full-string snapshot.
- Packet content is derived from diagnostic objects, recovery decision fields, assignment launch variables, and run/step identifiers.
- No adapter partial was expanded for packet generation; adapter diagnostics remain the source of gate facts and application orchestration owns prompt packet assembly.

## Architecture

- Builder placement is `CanDoItAll.Processes.Application` because it bridges runtime receipts with assignment launch variables and prompt updates.
- `CanDoItAll.Processes.Runtime` remains independent of application, module, Workbench, template markdown, and UI concerns.
- `CanDoItAll.Modules.Processes` only wires the builder into DI.
- CodeAnalytics snapshot `snap-20260708185114-6d1a7173` reported no scoped dependency cycles.


## Completed Validator Contract

- Invariant ID: SB04-FINAL-001
- Source raw note: GPTPro Extended escalation root-cause analysis and the user's broader process/template/artifact repair requirement.
- Expected behavior: The completed subbundle behavior remains implemented, tested, and represented by typed proof artifacts.
- Disallowed shallow implementation: Do not close the phase with prose-only proof, build-only proof, or hidden prompt-only gates.
- Failing-first test: N/A process/non-production final proof uses adversarial negative tests or preserved subbundle evidence in proof/SB04/transcripts/00-validator-metadata.txt.
- Passing test: Completed proof metadata is recorded in proof/SB04/transcripts/00-validator-metadata.txt and the subbundle manifest.
- Changed source files: bundle://subbundles/04-sb04-diagnostic-rework-packets/README.md and bundle://proof/SB04/manifest.md.
- Production assertions: Runtime/template/process behavior remains covered by the subbundle proof manifest and final bundle validation.
- Red-team negative case: Shallow final closure without proof metadata, semantic invariant labels, and transcripts is rejected by the completed validator.
- Downstream dependency check: Final bundle validation and recorded CodeAnalytics snapshots verify no unresolved downstream gate remains.


## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB04 semantic proof metadata | proof/SB04/semantic-invariants.md | proof/SB04/transcripts/00-validator-metadata.txt | final proof closure | proof/SB04/manifest.md rejects missing semantic proof |

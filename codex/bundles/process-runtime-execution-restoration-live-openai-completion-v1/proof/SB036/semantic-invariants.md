# SB036 Semantic Invariants

## Status
Completed.

## Invariant SB034_INV_001
- Invariant ID: `SB034_INV_001`
- Source raw note: manager diagnostics must be visible without turning drivers into runtime execution hooks.
- Expected behavior: Manager diagnostics are projected from supplied read-only verification evidence, require manager identity for attached modes, expose diagnostics or envelope by explicit mode, and keep all mutation flags false.
- Disallowed shallow implementation: report-only proof, anonymous manager attachment, diagnostics that can mutate process/transition/finalizer state, or evidence-envelope attachment in diagnostics mode.
- Passing tests: `Process_manager_readonly_projection_SB031_INV_001_projects_supplied_observations_as_diagnostics_without_mutation`, `Process_manager_readonly_projection_SB032_INV_001_attaches_evidence_envelope_only_when_requested`, and `Process_manager_readonly_projection_SB033_INV_001_rejects_unnamed_attached_manager_request`.

## Invariant SB035_INV_001
- Invariant ID: `SB035_INV_001`
- Source raw note: no-mutation audit/redaction/evidence envelope tests must prove read-only diagnostics and sensitive payload handling.
- Expected behavior: Transcript and runtime evidence adapters accept read-only operations, deny mutation/untrusted lanes, produce audit facts, redact sensitive data, and return evidence references/hashes without process mutation.
- Disallowed shallow implementation: accepting mutation operations, invoking verifier work after preflight denial, leaking secrets into diagnostics/audit facts, or treating envelope projection as process state mutation.
- Passing tests: `ProcessTranscriptVerificationReadOnlyAdapterTests`, `ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests`, `ProcessDomainEvidenceReadOnlyAdapterTests`, and `RuntimeEvidenceSourceIntegrationTests` in the SB036 transcript.

## Invariant SB036_INV_001
- Invariant ID: `SB036_INV_001`
- Source raw note: Gate L must preserve manager diagnostics without mutation and without introducing a runtime driver host.
- Expected behavior: The strict driver-consumer allowlist names only approved read-only driver consumer files, the final test slice passes, no source references the active bundle path, and no forbidden runtime host/driver mutation surface appears in scoped source.
- Disallowed shallow implementation: broad driver registration, selector/registry/manager command, runtime host, execution-capable driver hook, or unapproved process-module driver consumer file.
- Failing-first/negative proof: `bundle://proof/SB036/red-team/mutating-manager-diagnostic-proof-rejected.md`
- Passing test: `bundle://proof/SB036/transcripts/manager-diagnostics-no-mutation-tests.txt`
- Source assertions: `bundle://proof/SB036/transcripts/source-assertions.txt`

## Shallow-Pass Trap
A fake closure could prove only that a diagnostic type exists. SB036 rejects that by requiring no-mutation flags, explicit projection modes, manager identity validation, denied mutation lanes, redaction, evidence hashes, strict allowlisting, and clean forbidden-surface scans.

## Semantic Positive Proof
- `bundle://proof/SB034/manager-visible-readonly-diagnostic-projection-proof.md`
- `bundle://proof/SB035/no-mutation-redaction-evidence-envelope-proof.md`
- `bundle://proof/SB036/transcripts/manager-diagnostics-no-mutation-tests.txt`
- `bundle://proof/SB036/transcripts/source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB036/red-team/mutating-manager-diagnostic-proof-rejected.md`

## Anti-Stub Audit
- `bundle://proof/SB036/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB036/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No active bundle paths or forbidden runtime driver host surfaces were found in scoped source/tests.

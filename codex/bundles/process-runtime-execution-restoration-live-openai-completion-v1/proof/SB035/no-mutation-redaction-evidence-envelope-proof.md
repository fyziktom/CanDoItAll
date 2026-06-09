# SB035 No-Mutation Redaction Evidence Envelope Proof

## Status
Completed.

## Objective
Prove no-mutation audit facts, redaction, and evidence-envelope behavior for manager diagnostics.

## Source-Backed Proof
- Transcript read-only adapter tests: `repo://tests/CanDoItAll.Tests.Integration/ProcessTranscriptVerificationReadOnlyAdapterTests.cs`
- Runtime evidence read-only adapter tests: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs`
- Manager projection tests: `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`
- Runtime evidence source tests: `repo://tests/CanDoItAll.Tests.Integration/RuntimeEvidenceSourceIntegrationTests.cs`
- Gate transcript: `bundle://proof/SB036/transcripts/manager-diagnostics-no-mutation-tests.txt`
- TRX: `bundle://proof/SB036/SB036-manager-diagnostics-no-mutation.trx`

## Behavior Proven
- Transcript verification accepts only read-only verification operations and denies mutation or untrusted evidence before verifier invocation.
- Transcript diagnostics and audit facts redact secret token and owner email payloads.
- Unsupported non-.NET/Rust transcript lanes are denied with `NoMutationPerformed`.
- Runtime evidence consistency observations use `ManagerReadonly`, `RuntimeFactsRead`, stable audit output hashes, and denied mutation/untrusted source paths.
- Manager evidence-envelope projection attaches envelope data only when requested and keeps all mutation flags false.
- Runtime evidence source snapshots redact sensitive journal/run/workflow payloads and mark restricted hash policy where raw sensitive payloads are involved.

## Guard Tightening
The broad Gate L run initially exposed a stale strict allowlist in `Process_runtime_evidence_readonly_adapter_SB030_INV_003_keeps_driver_references_allowlisted_and_unregistered`. The test now explicitly includes:
- `ProcessManagerReadOnlyVerificationProjection.cs`
- `ProcessReadOnlyVerificationBatchModels.cs`

That keeps the guard strict: unapproved driver-consuming files still fail the test, while the current typed read-only projection/model surface is named and audited.

## Closure
SB035 is closed by the passing 30-test integration slice and clean source scans captured under `bundle://proof/SB036/transcripts`.

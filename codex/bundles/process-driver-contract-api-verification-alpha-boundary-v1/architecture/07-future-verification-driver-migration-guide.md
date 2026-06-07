# Future Verification Driver Migration Guide

## Approved Starting Point
- Start from `CanDoItAll.Processes.Drivers.Abstractions` contracts only.
- Use `ProcessDriverVerificationRequest` and `ProcessDriverVerificationResponse` as verification-only data exchange shapes.
- Use `ProcessDriverEvidenceReference` and `ProcessDriverTranscriptReference` to point at existing artifacts and transcripts.
- Use `ProcessDriverAuditFact` and `ProcessDriverRedactionDescriptor` to describe audit/redaction outcomes.

## Migration Steps
- Add tests first for permission mode, capability scope, denied operation, audit fact, evidence reference, and no-mutation response behavior.
- Keep any `.NET/Rust transcript verifier` rehearsal in tests until a future bundle approves production implementation.
- Keep Office and business-analysis lanes read-only; use references to existing evidence, not Graph calls or business-record mutation.
- Preserve dependency direction: Core exposes deterministic descriptors, while driver contracts describe references to those descriptors without Core depending on driver abstractions.

## Stop Conditions
- Stop if implementation needs command execution, package restore, shell access, workspace writes, storage writes, process mutation, claim mutation, transition mutation, finalizer application, retry scheduling, Office/Graph calls, provider repair, or UI changes.
- Stop if a proposed type looks like a registry, runtime, selector, provider, host, manager command, service registration, or connector implementation.
- Stop if proof is only a filled table, non-empty string, or status count without a negative case and a semantic positive case.

# SB030 Red-Team Negative Proof

## Rejected Shallow Pass
A shallow pass would:
- load `/processes` without selecting a durable run,
- assert static page text only,
- skip API readback,
- skip typed recovery attributes,
- skip artifact record persistence, or
- claim screenshots without proving blocked-state semantics.

## Why It Is Rejected
The accepted proof must demonstrate a real process run with persisted blocked state and a durable artifact record. It must prove both public API readback and browser-visible rendering at large desktop.

## Required Positive Counter-Evidence
The SB030 Playwright test rejects the shallow pass by asserting:
- API run status `Blocked`,
- API health recommended action `RecoverArtifactsOnly`,
- API step block reason `ArtifactContractUnsatisfied`,
- UI recovery diagnostics `data-block-reason-code="ArtifactContractUnsatisfied"`,
- UI recovery options containing `RecoverArtifactsOnly`,
- Evidence ledger containing the satisfied artifact obligation and durable artifact record id.

Proof: `bundle://proof/SB030/transcripts/run-detail-recovery-ui-test.txt`.

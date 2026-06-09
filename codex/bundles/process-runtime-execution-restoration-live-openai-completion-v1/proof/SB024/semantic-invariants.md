# SB024 Semantic Invariants

## Status
Completed.

## Invariant SB024_INV_001
- Invariant ID: `SB024_INV_001`
- Source raw note: process runtime restoration must not be limited to the software/.NET path.
- Expected behavior: A non-software business-analysis process imports, publishes, starts, completes non-blocked business steps, skips the blocked correction branch, records typed business artifacts, and reads back managed business-plan content.
- Disallowed shallow implementation: Reusing software mock-process proof, proving only template projection, or asserting run completion without business artifacts and readback.
- Failing-first test: `bundle://proof/SB024/red-team/software-scenario-not-business-proof.txt` rejects software scenario reuse.
- Passing test: Two focused integration tests passed in `bundle://proof/SB024/transcripts/business-analysis-process-tests.txt`.
- Changed source files: No production source changed in SB024. Current source/test hashes are captured in `bundle://proof/SB024/manifest.md`.
- Production assertions: `bundle://proof/SB024/transcripts/business-analysis-process-source-assertions.txt`
- Red-team negative case: `bundle://proof/SB024/red-team/software-scenario-not-business-proof.txt`
- Downstream dependency check: SB025-SB027 trigger-origin work may start after both software and non-software process scenarios are proven.

## Shallow-Pass Trap
A fake Gate H closure could claim non-software coverage from the completed .NET mock process. SB024 rejects that by requiring the business-plan template, business roles, no software operations, business-specific artifacts, and managed business-plan readback.

## Semantic Positive Proof
- `bundle://proof/SB024/transcripts/business-analysis-process-tests.txt`
- `bundle://proof/SB024/transcripts/business-analysis-process-source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB024/red-team/software-scenario-not-business-proof.txt`

## Anti-Stub Audit
- `bundle://proof/SB024/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Matches are documentation and negative test assertions, not an execution-capable process-driver runtime host, process-driver registry, selector, or production `NotImplemented` path.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Business run | Business-plan template import/publish/start | Runtime readback | Completes non-blocked steps and skips blocked correction branch | Software proof reuse is rejected |
| Business artifacts | `RecordArtifactAsync` during business step completion | Run detail and managed storage | Six typed business artifacts persist with role handoffs | Tests assert exact business artifact titles/kinds |
| Business plan content | Workspace managed artifact | Business plan artifact readback | Contains validation label and handoff evidence summary | Generic run completion without readback is rejected |

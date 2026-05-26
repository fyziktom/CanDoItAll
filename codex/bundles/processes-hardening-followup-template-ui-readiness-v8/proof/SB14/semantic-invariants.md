# SB14 Semantic Invariants

- Invariant ID: SB14-INV-001
- Expected behavior: the required baseline scenarios exercise typed template contracts, branch selection, artifact creation, blocked-state recovery metadata, and reusable seed runtime behavior through production template loading and process seeding paths.
- Disallowed shallow implementation: docs-only baseline notes, hardcoded Tetris runtime behavior, scenario records that do not load through `ProcessTemplatePackScenarios`, recovery metadata that bypasses `ProcessBlockStateClassifier`, or tests that assert only raw JSON text without projected template contracts.
- Required proof: failing-first/adversarial proof, passing production-path tests, source assertions, anti-stub audit, and changed-file hashes.

## Production Behavior Artifact Matrix

| Invariant surface | Required behavior | Negative case protected | Proof |
| --- | --- | --- | --- |
| Typed contract scenario exercises | Each required baseline has `ContractExercises` that match projected step operation contracts. | A template can drift to a different target scope or allowed operation while the baseline still appears documented. | `bundle://proof/SB14/transcripts/passing.txt` |
| Recovery scenario exercises | Each required baseline has `RecoveryExercises` validated through typed block cause classification. | Prose-only exception text can claim recovery behavior without producing typed recovery options. | `bundle://proof/SB14/transcripts/passing.txt` |
| Runtime blocked transitions | Seeded blocked transitions carry `BlockCause` only on the blocked transition request. | A seeded blocked state can lose its ownership cause and degrade recovery routing. | `bundle://proof/SB14/transcripts/source-assertions.txt` |
| Runtime artifact expectation matching | Seeded artifacts with expectation ids are matched by expectation id before title/kind fallback. | A same-title artifact on a different step can satisfy the wrong required expectation. | `bundle://proof/SB14/transcripts/passing.txt` |
| Generic template pack behavior | Release, architecture, customer, incident, business, and Blazor/Tetris coverage lives in template/scenario data. | Tetris-specific behavior leaks into generic process runtime code. | `bundle://proof/SB14/transcripts/source-assertions.txt`; `bundle://proof/SB14/transcripts/anti-stub-audit.txt` |

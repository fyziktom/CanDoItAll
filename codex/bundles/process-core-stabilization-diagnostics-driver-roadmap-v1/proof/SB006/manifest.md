# SB006 Proof Manifest

## Scope
- Subbundle: `SB006 - Gate B - Core API stability proof`
- Invariant IDs: `SB006-INV-001`
- Changed files:
  - `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
  - `bundle://architecture/04-core-public-api-inventory.md`

## Changed-File Hashes
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
  - SHA-256: `AB11E782B75B090E0875D0CCAF2283698BBC74BA62EFD43803F32A955ACC08E5`
- `bundle://architecture/04-core-public-api-inventory.md`
  - SHA-256: `13B0478616C828BEE5CE251E625E0AEF9AEF8203D4EA0748A8A4C7983FACF155`
- Hash transcript: `bundle://proof/SB006/transcripts/changed-file-hashes.txt`

## Command Transcripts
- Passing build: `bundle://proof/SB006/transcripts/build.txt`
- Passing API guard and architecture tests: `bundle://proof/SB006/transcripts/architecture-api-guard-tests-rerun.txt`
- Source assertions: `bundle://proof/SB006/transcripts/source-assertions.txt`
- Core forbidden-token scan: `bundle://proof/SB006/transcripts/core-forbidden-token-scan.txt`
- Anti-stub audit: `bundle://proof/SB006/transcripts/anti-stub-audit.txt`

## Source Assertions
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` includes `Process_core_public_api_surface_is_explicitly_guarded`.
- The guard reflects over `CanDoItAll.Processes.Core` and snapshots public types, enum values, constructors, properties, and public methods while excluding generated record boilerplate.
- `bundle://architecture/04-core-public-api-inventory.md` records the human-readable Core public API owner classification.

## Failing-First And Passing Proof
- Failing-first: N/A for process/non-production proof because SB004-SB006 add docs/test guardrails and do not change production behavior.
- Adversarial negative proof: an unapproved public Core API addition now fails `Process_core_public_api_surface_is_explicitly_guarded`.
- Passing proof: `bundle://proof/SB006/transcripts/architecture-api-guard-tests-rerun.txt` exits zero with 85 focused architecture tests passing.

## Anti-Stub Audit
- `bundle://proof/SB006/transcripts/anti-stub-audit.txt` proves SB004-SB006 changed no Process Core production source and introduced no production stub path.

## Semantic Contract
- Semantic invariants: `bundle://proof/SB006/semantic-invariants.md`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| N/A | No production signal, persisted state, durable record, or domain event is introduced. | N/A | N/A | `bundle://proof/SB006/transcripts/anti-stub-audit.txt` |

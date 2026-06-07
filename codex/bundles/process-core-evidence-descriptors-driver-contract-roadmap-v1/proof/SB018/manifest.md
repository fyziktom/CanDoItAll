# SB018 Proof Manifest

## Status
- Completed.

## Scope
- Gate F closure for explicit Core consumer boundaries.
- Confirms direct Core consumers match the ownership map, global usings do not hide Core imports, side-effect files remain clean, and no production driver/UI/stub drift occurred.

## Semantic Invariants
- `bundle://proof/SB018/semantic-invariants.md`.
- Invariant IDs: SB018-CONSUMER-MAP-001, SB018-ADAPTER-CONFINEMENT-001, SB018-DRIVER-001, SB018-UI-001, SB018-STUB-001.

## Failing-First Evidence
- `bundle://proof/SB018/transcripts/failing-first-consumer-boundary-gap.txt` records the consumer-boundary gap with `ExitCode: 1`.

## Passing Evidence
- Build: `bundle://proof/SB018/transcripts/consumer-boundary-build.txt`.
- Architecture tests: `bundle://proof/SB018/transcripts/consumer-boundary-architecture-tests.txt`.
- Exact consumer scan: `bundle://proof/SB018/transcripts/explicit-core-consumer-list.txt`.
- Source assertions: `bundle://proof/SB018/transcripts/source-assertions.txt`.
- Semantic closure: `bundle://proof/SB018/transcripts/semantic-closure.txt`.

## Scan Evidence
- Global using scan: `bundle://proof/SB018/transcripts/global-using-core-scan.txt`.
- Side-effect Core reference scan: `bundle://proof/SB018/transcripts/side-effect-core-reference-scan.txt`.
- Production process-driver token scan: `bundle://proof/SB018/transcripts/production-driver-token-scan.txt`.
- UI/media drift scan: `bundle://proof/SB018/transcripts/ui-media-drift-scan.txt`.
- Anti-stub audit: `bundle://proof/SB018/transcripts/anti-stub-audit.txt`.

## Hashes
- Hash index: `bundle://proof/SB018/transcripts/changed-file-hashes.txt`.
- SHA-256 `71E60955C752E4786A195BC022108DCFB13C4DC9105F7A7EDF5F60BEC059DD8B` for `bundle://architecture/06-core-adapter-ownership-map.md`.
- SHA-256 `0C73C7FFD5F15281F04C0C8011AEFCF2AE5D14BEFF6BD04C97EC6CB68C7827EF` for `repo://codex/bundles/process-core-stabilization-diagnostics-driver-roadmap-v1/architecture/05-core-consumer-allowed-call-site-map.md`.
- SHA-256 `496A478F3DB29FF4B6BEBB50762205091A9AEC082A859BAD70D2C79FF0D8B9F6` for `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.

## Result
- SB018 passed. Core consumers remain explicit, dependency-clean, adapter-owned, and driver-free.

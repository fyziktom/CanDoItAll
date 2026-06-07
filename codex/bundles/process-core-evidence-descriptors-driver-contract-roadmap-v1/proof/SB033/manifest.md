# SB033 Proof Manifest

## Status
- Completed.

## Scope
- Gate K closure for Core readiness decision.

## Semantic Invariants
- `bundle://proof/SB033/semantic-invariants.md`.
- Invariant IDs: SB033-SCORECARD-001, SB033-NEXT-FAMILY-001, SB033-DRIVER-SOURCE-001, SB033-UI-001, SB033-STUB-001.

## Failing-First Evidence
- `bundle://proof/SB033/transcripts/failing-first-core-readiness-gap.txt` records the pre-gate gap with `ExitCode: 1`.

## Passing Evidence
- Build: `bundle://proof/SB033/transcripts/core-readiness-build.txt`.
- Architecture tests: `bundle://proof/SB033/transcripts/core-readiness-architecture-tests.txt`.
- Source assertions: `bundle://proof/SB033/transcripts/source-assertions.txt`.
- Semantic closure: `bundle://proof/SB033/transcripts/semantic-closure.txt`.

## Scan Evidence
- Production process-driver token scan: `bundle://proof/SB033/transcripts/production-driver-token-scan.txt`.
- Readonly/proposal document scan: `bundle://proof/SB033/transcripts/driver-readonly-doc-scan.txt`.
- UI/media drift scan: `bundle://proof/SB033/transcripts/ui-media-drift-scan.txt`.
- Anti-stub audit: `bundle://proof/SB033/transcripts/anti-stub-audit.txt`.

## Result
- SB033 passed. Core readiness is documented, no broad runtime extraction is approved, and proof matrix is complete for downstream broad smoke.

# SB033 Semantic Invariants

## SB033-SCORECARD-001
- Execution, finalizer, diagnostics, projection, adapter ownership, and public API stability must be scored with remaining side-effect blockers still module-owned.

## SB033-NEXT-FAMILY-001
- The next-pure-family decision must not approve broad dispatcher, runtime, storage, workspace, AgentFramework, finalizer, transition, claim, or retry extraction.

## SB033-DRIVER-SOURCE-001
- Production source must remain free of process-driver runtime APIs.

## SB033-UI-001
- This roadmap slice must not change UI or media files.

## SB033-STUB-001
- Changed files must not add TODO, stub, placeholder, fake, dummy, or NotImplemented markers.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB033/transcripts/failing-first-core-readiness-gap.txt`.
- Passing build proof: `bundle://proof/SB033/transcripts/core-readiness-build.txt`.
- Passing architecture proof: `bundle://proof/SB033/transcripts/core-readiness-architecture-tests.txt`.
- Source and scan proof: `bundle://proof/SB033/transcripts/source-assertions.txt`, `bundle://proof/SB033/transcripts/production-driver-token-scan.txt`, `bundle://proof/SB033/transcripts/driver-readonly-doc-scan.txt`, `bundle://proof/SB033/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB033/transcripts/anti-stub-audit.txt`.

# SB030 Semantic Invariants

## SB030-DECISION-001
- Driver implementation must default to no unless permission enforcement, audit persistence, sandbox policy, runtime ownership, and executable negative tests exist.

## SB030-PREREQUISITE-001
- Alpha candidate selection must be deferred when prerequisites are unmet.

## SB030-DRIVER-SOURCE-001
- Production source must remain free of process-driver runtime APIs.

## SB030-UI-001
- This roadmap slice must not change UI or media files.

## SB030-STUB-001
- Changed files must not add TODO, stub, placeholder, fake, dummy, or NotImplemented markers.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB030/transcripts/failing-first-driver-implementation-decision-gap.txt`.
- Passing build proof: `bundle://proof/SB030/transcripts/driver-implementation-decision-build.txt`.
- Passing architecture proof: `bundle://proof/SB030/transcripts/driver-implementation-decision-architecture-tests.txt`.
- Source and scan proof: `bundle://proof/SB030/transcripts/source-assertions.txt`, `bundle://proof/SB030/transcripts/production-driver-token-scan.txt`, `bundle://proof/SB030/transcripts/driver-readonly-doc-scan.txt`, `bundle://proof/SB030/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB030/transcripts/anti-stub-audit.txt`.

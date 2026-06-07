# SB017 Proof Manifest

## Status
- Completed.

## Scope
- Reject direct Core usage in side-effect dispatch files outside explicit adapters and route boundary files.

## Evidence
- Architecture tests: `bundle://proof/SB018/transcripts/consumer-boundary-architecture-tests.txt`.
- Side-effect Core reference scan: `bundle://proof/SB018/transcripts/side-effect-core-reference-scan.txt`.
- Exact consumer scan: `bundle://proof/SB018/transcripts/explicit-core-consumer-list.txt`.
- Source assertions: `bundle://proof/SB018/transcripts/source-assertions.txt`.

## Result
- SB017 passed. Side-effect dispatch files outside the exact allow-list do not import `CanDoItAll.Processes.Core` directly.

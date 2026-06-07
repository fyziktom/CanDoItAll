# SB018 Semantic Invariants

## SB018-CONSUMER-MAP-001
- Every dispatch file that directly references `CanDoItAll.Processes.Core` must appear in `architecture/06-core-adapter-ownership-map.md`.
- The actual filesystem scan must exactly match the allowed file list.
- Shallow-pass trap: updating prose without comparing it to actual files would allow hidden Core consumers to drift in.

## SB018-ADAPTER-CONFINEMENT-001
- Side-effect dispatch files outside the exact allow-list must not import Core directly.
- Global usings must not hide Core imports from file-level scans.
- Shallow-pass trap: adding a global Core using or direct Core import in an orchestration/file/storage component would bypass adapter confinement while tests still compiled.

## SB018-DRIVER-001
- This phase must not introduce production process-driver registry, pack, selector, DI, or manager command APIs.

## SB018-UI-001
- This runtime/Core/service slice must not change UI, mobile, CSS, JavaScript, TypeScript, or media files.

## SB018-STUB-001
- Changed files must not add TODO, stub, or NotImplemented placeholders.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB018/transcripts/failing-first-consumer-boundary-gap.txt`.
- Passing build proof: `bundle://proof/SB018/transcripts/consumer-boundary-build.txt`.
- Passing architecture proof: `bundle://proof/SB018/transcripts/consumer-boundary-architecture-tests.txt`.
- Consumer map proof: `bundle://proof/SB018/transcripts/explicit-core-consumer-list.txt`.
- Source and scan proof: `bundle://proof/SB018/transcripts/source-assertions.txt`, `bundle://proof/SB018/transcripts/global-using-core-scan.txt`, `bundle://proof/SB018/transcripts/side-effect-core-reference-scan.txt`, `bundle://proof/SB018/transcripts/production-driver-token-scan.txt`, `bundle://proof/SB018/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB018/transcripts/anti-stub-audit.txt`.

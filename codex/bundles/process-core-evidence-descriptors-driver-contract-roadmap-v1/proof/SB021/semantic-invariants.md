# SB021 Semantic Invariants

## SB021-API-SNAPSHOT-001
- The Core public API surface must be generated from the built `CanDoItAll.Processes.Core` assembly and remain covered by the architecture/API guard test.
- Shallow-pass trap: editing the embedded snapshot without a regenerated transcript would hide public API drift from reviewers.

## SB021-OWNER-CLASSIFICATION-001
- Every public Core family added through this bundle must be owner-classified in `architecture/07-public-api-owner-classification.md`.
- Denied public API families must stay explicit so future process-driver proposals do not get mistaken for shipped contracts.

## SB021-CORE-HYGIENE-001
- `CanDoItAll.Processes.Core` must remain package-free and reference only `CanDoItAll.Processes.Contracts`.
- Core namespaces must remain limited to `Artifacts`, `Diagnostics`, `Execution`, `Finalization`, `Routing`, and `Subprocess`.
- Core must not expose broad helper/service/manager/registry/driver API families.

## SB021-DRIVER-001
- This phase must not introduce production process-driver registry, pack, selector, DI, runtime, or manager command APIs.

## SB021-UI-001
- This runtime/Core/service slice must not change UI, mobile, CSS, JavaScript, TypeScript, or media files.

## SB021-STUB-001
- Changed files must not add TODO, stub, placeholder, fake, dummy, or NotImplemented markers.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB021/transcripts/failing-first-api-stability-gap.txt`.
- Passing build proof: `bundle://proof/SB021/transcripts/api-stability-build.txt`.
- Passing architecture/API proof: `bundle://proof/SB021/transcripts/api-stability-architecture-tests.txt`.
- API transcript proof: `bundle://proof/SB021/transcripts/current-core-public-api-surface-api-stability.txt`.
- Source and scan proof: `bundle://proof/SB021/transcripts/source-assertions.txt`, `bundle://proof/SB021/transcripts/core-project-reference-scan.txt`, `bundle://proof/SB021/transcripts/core-namespace-package-hygiene-scan.txt`, `bundle://proof/SB021/transcripts/forbidden-core-source-scan.txt`, `bundle://proof/SB021/transcripts/production-driver-token-scan.txt`, `bundle://proof/SB021/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB021/transcripts/anti-stub-audit.txt`.

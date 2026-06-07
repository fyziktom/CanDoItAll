# SB027 Proof Manifest

## Status
- Completed.

## Scope
- Gate I closure proving domain schemas are read-only and no production driver APIs were introduced.

## Semantic Invariants
- `bundle://proof/SB027/semantic-invariants.md`.
- Invariant IDs: SB027-SCHEMA-READONLY-001, SB027-DOMAIN-DENIAL-001, SB027-DRIVER-SOURCE-001, SB027-UI-001, SB027-STUB-001.

## Failing-First Evidence
- `bundle://proof/SB027/transcripts/failing-first-domain-schema-gap.txt` records the pre-gate gap with `ExitCode: 1`.

## Passing Evidence
- Build: `bundle://proof/SB027/transcripts/domain-schemas-build.txt`.
- Architecture tests: `bundle://proof/SB027/transcripts/domain-schemas-architecture-tests.txt`.
- Source assertions: `bundle://proof/SB027/transcripts/source-assertions.txt`.
- Semantic closure: `bundle://proof/SB027/transcripts/semantic-closure.txt`.

## Scan Evidence
- Production process-driver token scan: `bundle://proof/SB027/transcripts/production-driver-token-scan.txt`.
- Readonly/proposal document scan: `bundle://proof/SB027/transcripts/driver-readonly-doc-scan.txt`.
- UI/media drift scan: `bundle://proof/SB027/transcripts/ui-media-drift-scan.txt`.
- Anti-stub audit: `bundle://proof/SB027/transcripts/anti-stub-audit.txt`.

## Result
- SB027 passed. Domain schemas are read-only and side-effect denial is guarded.

# SB027 Semantic Invariants

## SB027-SCHEMA-READONLY-001
- .NET/Rust, Office, business-analysis, and runtime verification schemas must describe existing evidence only.

## SB027-DOMAIN-DENIAL-001
- Schemas must deny command execution, Graph/Office calls, document mutation, workspace/storage writes, CRM/project/business-record mutation, process mutation, and runtime hooks.

## SB027-DRIVER-SOURCE-001
- Production source must remain free of process-driver runtime APIs.

## SB027-UI-001
- This roadmap slice must not change UI or media files.

## SB027-STUB-001
- Changed files must not add TODO, stub, placeholder, fake, dummy, or NotImplemented markers.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB027/transcripts/failing-first-domain-schema-gap.txt`.
- Passing build proof: `bundle://proof/SB027/transcripts/domain-schemas-build.txt`.
- Passing architecture proof: `bundle://proof/SB027/transcripts/domain-schemas-architecture-tests.txt`.
- Source and scan proof: `bundle://proof/SB027/transcripts/source-assertions.txt`, `bundle://proof/SB027/transcripts/production-driver-token-scan.txt`, `bundle://proof/SB027/transcripts/driver-readonly-doc-scan.txt`, `bundle://proof/SB027/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB027/transcripts/anti-stub-audit.txt`.

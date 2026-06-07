# SB024 Semantic Invariants

## SB024-PROPOSAL-ONLY-001
- Driver permission/audit material must remain proposal documentation and architecture-test proof only.
- No production driver pack, registry, selector, runtime, provider, manager command, or dependency-injection integration may be added.

## SB024-NEGATIVE-SCENARIOS-001
- Negative scenarios must deny process mutation, runtime hooks, registry, dependency-injection integration, manager commands, shell, Graph, storage writes, and incomplete audit facts.

## SB024-DRIVER-SOURCE-001
- Production source under Processes Core, Contracts, and Modules.Processes must not contain process-driver runtime tokens.

## SB024-UI-001
- This runtime/Core/service roadmap slice must not change UI or media files.

## SB024-STUB-001
- Changed files must not add TODO, stub, placeholder, fake, dummy, or NotImplemented markers.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB024/transcripts/failing-first-driver-proposal-gap.txt`.
- Passing build proof: `bundle://proof/SB024/transcripts/driver-proposal-build.txt`.
- Passing architecture proof: `bundle://proof/SB024/transcripts/driver-proposal-architecture-tests.txt`.
- Source and scan proof: `bundle://proof/SB024/transcripts/source-assertions.txt`, `bundle://proof/SB024/transcripts/production-driver-token-scan.txt`, `bundle://proof/SB024/transcripts/driver-readonly-doc-scan.txt`, `bundle://proof/SB024/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB024/transcripts/anti-stub-audit.txt`.

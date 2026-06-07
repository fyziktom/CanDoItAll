# SB006 Semantic Invariants

## SB006-EXEC-001
- Core execution descriptors must represent deterministic run, attempt, and carried-proof facts only.
- Dispatcher behavior must remain driven by the same post-attempt facts: completion status, completion reason, missing tools, critical tool failures, selected branch outcome, and carried proof.
- Shallow-pass trap: adding record types without routing dispatcher outcome construction through the adapter would leave the descriptor contract unexercised.

## SB006-BOUNDARY-001
- Core must not depend on module, infrastructure, AgentFramework execution, EF, workspace/storage, filesystem, logging, or dispatcher side-effect APIs.
- Dispatch side-effect files must not import `CanDoItAll.Processes.Core` directly; only explicit adapter files may bridge to Core.
- Shallow-pass trap: importing Core into broad dispatcher files would pass compile but weaken the adapter boundary.

## SB006-DRIVER-001
- This phase must not introduce production process-driver registry, pack, selector, DI, or manager command APIs.
- Driver schema and roadmap work remains proposal/read-only until later phases.

## SB006-UI-001
- This runtime/Core/service slice must not change UI, mobile, CSS, JavaScript, TypeScript, or media files.

## SB006-STUB-001
- Changed production C# files must not add TODO, stub, or NotImplemented placeholders.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB006/transcripts/failing-first-execution-descriptor-gap.txt`.
- Passing build proof: `bundle://proof/SB006/transcripts/execution-descriptor-build-after-snapshot-prep.txt`.
- Passing architecture proof: `bundle://proof/SB006/transcripts/execution-descriptor-architecture-tests-after-fix.txt`.
- Passing behavior proof: `bundle://proof/SB006/transcripts/execution-descriptor-focused-integration-tests.txt`.
- Source and scan proof: `bundle://proof/SB006/transcripts/source-assertions.txt`, `bundle://proof/SB006/transcripts/core-execution-forbidden-token-scan.txt`, `bundle://proof/SB006/transcripts/adapter-confinement-scan.txt`, `bundle://proof/SB006/transcripts/production-process-driver-token-scan.txt`, `bundle://proof/SB006/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB006/transcripts/anti-stub-audit.txt`.

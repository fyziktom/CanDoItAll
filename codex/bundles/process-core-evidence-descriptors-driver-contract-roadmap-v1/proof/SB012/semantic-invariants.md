# SB012 Semantic Invariants

## SB012-DIAGNOSTIC-001
- Core diagnostic descriptors must represent immutable retry, no-progress, and provider repair facts only.
- Current retry/provider decisions must remain module-owned and behaviorally unchanged.
- Shallow-pass trap: adding diagnostic records without comparing them to computed retry/provider decisions would leave descriptor parity unproved.

## SB012-BOUNDARY-001
- Core must not depend on AgentFramework execution detail/tool receipt objects, module retry fact records, provider repair coordinators, infrastructure, EF, workspace/storage, filesystem, logging, or dispatcher side-effect APIs.
- Dispatch side-effect files must not import `CanDoItAll.Processes.Core` directly; only explicit adapter files may bridge to Core.

## SB012-DRIVER-001
- This phase must not introduce production process-driver registry, pack, selector, DI, or manager command APIs.

## SB012-UI-001
- This runtime/Core/service slice must not change UI, mobile, CSS, JavaScript, TypeScript, or media files.

## SB012-STUB-001
- Changed production C# files must not add TODO, stub, or NotImplemented placeholders.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB012/transcripts/failing-first-diagnostic-descriptor-gap.txt`.
- Passing build proof: `bundle://proof/SB012/transcripts/diagnostic-descriptor-build-before-snapshot.txt`.
- Passing architecture proof: `bundle://proof/SB012/transcripts/diagnostic-descriptor-architecture-tests.txt`.
- Passing behavior proof: `bundle://proof/SB012/transcripts/diagnostic-descriptor-focused-integration-tests.txt`.
- Source and scan proof: `bundle://proof/SB012/transcripts/source-assertions.txt`, `bundle://proof/SB012/transcripts/core-diagnostics-forbidden-token-scan.txt`, `bundle://proof/SB012/transcripts/adapter-confinement-scan.txt`, `bundle://proof/SB012/transcripts/production-process-driver-token-scan.txt`, `bundle://proof/SB012/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB012/transcripts/anti-stub-audit.txt`.

# SB009 Semantic Invariants

## SB009-FINALIZER-001
- Core finalizer descriptors must represent deterministic intent/result facts only.
- Finalizer no-result behavior must remain no-apply, and non-null finalizer results must still apply through the module-owned transition delegate.
- Shallow-pass trap: adding finalizer record types without using the adapter in `ProcessDispatchFinalizerAdapter` would leave parity unproved.

## SB009-BOUNDARY-001
- Core must not depend on module finalizer context/result types, module block-cause enums, infrastructure, AgentFramework execution, EF, workspace/storage, filesystem, logging, or dispatcher side-effect APIs.
- Dispatch side-effect files must not import `CanDoItAll.Processes.Core` directly; only explicit adapter files may bridge to Core.

## SB009-DRIVER-001
- This phase must not introduce production process-driver registry, pack, selector, DI, or manager command APIs.

## SB009-UI-001
- This runtime/Core/service slice must not change UI, mobile, CSS, JavaScript, TypeScript, or media files.

## SB009-STUB-001
- Changed production C# files must not add TODO, stub, or NotImplemented placeholders.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB009/transcripts/failing-first-finalizer-descriptor-gap.txt`.
- Passing build proof: `bundle://proof/SB009/transcripts/finalizer-descriptor-build-before-snapshot.txt`.
- Passing architecture proof: `bundle://proof/SB009/transcripts/finalizer-descriptor-architecture-tests.txt`.
- Passing behavior proof: `bundle://proof/SB009/transcripts/finalizer-descriptor-focused-integration-tests.txt`.
- Source and scan proof: `bundle://proof/SB009/transcripts/source-assertions.txt`, `bundle://proof/SB009/transcripts/core-finalizer-forbidden-token-scan.txt`, `bundle://proof/SB009/transcripts/adapter-confinement-scan.txt`, `bundle://proof/SB009/transcripts/production-process-driver-token-scan.txt`, `bundle://proof/SB009/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB009/transcripts/anti-stub-audit.txt`.

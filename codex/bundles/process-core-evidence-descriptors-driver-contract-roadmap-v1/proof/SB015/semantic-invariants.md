# SB015 Semantic Invariants

## SB015-PROJECTION-001
- Core projection evidence descriptors must represent immutable source order facts only.
- The actual projection coordinator sequence must remain module-owned and must be checked through the adapter.
- Shallow-pass trap: adding source-order records without comparing them to the current coordinator order would leave projection precedence unproved.

## SB015-LINEAGE-001
- Core lineage descriptors must represent immutable lineage facts only.
- Lineage JSON serialization, projection identity hash computation, artifact persistence, and manager-recovery key construction must remain module-owned.
- Shallow-pass trap: adding lineage records without exercising source adapter lineage parity would miss identity and recovery drift.

## SB015-PROVIDER-BROWSER-001
- Core provider-native browser descriptors may classify normalized browser tool evidence and satisfaction eligibility only.
- Browser output discovery, path matching, safe path resolution, file existence checks, and file length checks must remain module-owned.
- Shallow-pass trap: adding browser descriptors without preserving provider-native path and declared-output tests would silently change required browser proof handling.

## SB015-BOUNDARY-001
- Core must not depend on AgentFramework execution detail/tool receipt objects, module lineage JSON objects, projection coordinators, infrastructure, EF, workspace/storage, filesystem, logging, or dispatcher side-effect APIs.
- Dispatch side-effect files must not import `CanDoItAll.Processes.Core` directly; only explicit adapter files may bridge to Core.

## SB015-DRIVER-001
- This phase must not introduce production process-driver registry, pack, selector, DI, or manager command APIs.

## SB015-UI-001
- This runtime/Core/service slice must not change UI, mobile, CSS, JavaScript, TypeScript, or media files.

## SB015-STUB-001
- Changed files must not add TODO, stub, or NotImplemented placeholders.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB015/transcripts/failing-first-projection-validation-descriptor-gap.txt`.
- Passing build proof: `bundle://proof/SB015/transcripts/projection-validation-descriptor-build.txt`.
- Passing architecture proof: `bundle://proof/SB015/transcripts/projection-validation-architecture-tests.txt`.
- Passing behavior proof: `bundle://proof/SB015/transcripts/projection-validation-focused-integration-tests.txt`.
- Source and scan proof: `bundle://proof/SB015/transcripts/source-assertions.txt`, `bundle://proof/SB015/transcripts/forbidden-core-source-scan.txt`, `bundle://proof/SB015/transcripts/dispatch-core-reference-scan.txt`, `bundle://proof/SB015/transcripts/ui-media-drift-scan.txt`, `bundle://proof/SB015/transcripts/anti-stub-audit.txt`.

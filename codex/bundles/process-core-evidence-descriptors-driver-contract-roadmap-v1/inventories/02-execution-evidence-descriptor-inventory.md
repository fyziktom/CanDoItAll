# Execution Evidence Descriptor Inventory

## Scope
- Covers SB004 execution outcome facts used by `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`.
- Separates deterministic descriptor fields that may live in `Processes.Core` from runtime/application facts that must remain module-owned.
- Feeds SB005 implementation and SB006 parity proof.

## Pure Core Descriptor Fields
- Run identity and timing: execution run id, state, outcome, terminal/active flags, pending approval presence, created/started/completed timestamps.
- Attempt outcome: attempt number, completion status, completion reason, missing required tool names/counts, unresolved critical tool failure count, selected branch outcome id.
- Carried proof flags: concrete implementation proof, runnable application proof, and concrete product mutation.
- Derived classifications: execution run observation kind, terminal-state classification, missing-tool presence, and unresolved-critical-failure presence.

## Module-Owned Runtime Fields
- AgentFramework execution calls, chat sessions, provider calls, and response text.
- Retry loop orchestration, current attempt mutation, final outcome assignment, and process step transition application.
- Tool receipt analysis, artifact projection/writeback, storage/workspace resolution, EF/database state, and logging.
- Process driver proposal/registry/selector concepts, which remain out of production scope for this bundle.

## Adapter Ownership
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionEvidenceDescriptorAdapter.cs` is the execution descriptor bridge.
- The adapter converts module-owned post-attempt facts into Core descriptors.
- Dispatch side-effect files consume the adapter, not `CanDoItAll.Processes.Core` directly.

## Validation
- Source assertions: `bundle://proof/SB006/transcripts/source-assertions.txt`.
- Boundary scan: `bundle://proof/SB006/transcripts/adapter-confinement-scan.txt`.
- Gate semantics: `bundle://proof/SB006/semantic-invariants.md`.

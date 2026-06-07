# Retry Provider Diagnostics Inventory

## Scope
- Covers SB010 retry, missing-tool, critical tool failure, no-progress, provider failure, and provider repair facts.
- Separates immutable diagnostic descriptors from provider health calls, assigned-agent repair, retry persistence, and recovery packet creation.
- Feeds SB011 implementation and SB012 parity proof.

## Pure Core Descriptor Fields
- Retry facts: retry decision, attempt number, maximum attempts, retry reasons, retry reason summary, missing required tools, failed tool names, unresolved critical failure count, build/test failure flags, provider/interruption/finalizer failure flags, and primary diagnostic failure kind.
- No-progress facts: signal presence, fingerprint, execution run id, tool signature, artifact validation fingerprint, mutation delta, and proof delta.
- Provider repair facts: recoverable provider failure presence, repair outcome presence, failure summary, failed/fallback provider names, fallback model, and affected agent count.

## Module-Owned Runtime Fields
- Provider health probing, provider fallback selection, assigned-agent repair, and provider API calls.
- Retry/no-progress journal persistence, recovery ledger persistence, recovery packet creation, and recovery prompt rendering.
- AgentFramework execution detail and tool receipt objects.
- Process driver proposal/registry/selector concepts, which remain out of production scope for this bundle.

## Adapter Ownership
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRetryDiagnosticDescriptorAdapter.cs` is the retry/provider diagnostics bridge.
- The adapter maps module retry facts, no-progress signal data, and provider repair outcome data into Core descriptors.
- `ProcessRunAutomationDispatchService.Execution.cs` still computes retry/provider decisions and only uses descriptors as consistency evidence.

## Validation
- Source assertions: `bundle://proof/SB012/transcripts/source-assertions.txt`.
- Behavioral proof: `bundle://proof/SB012/transcripts/diagnostic-descriptor-focused-integration-tests.txt`.
- Boundary scan: `bundle://proof/SB012/transcripts/adapter-confinement-scan.txt`.
- Gate semantics: `bundle://proof/SB012/semantic-invariants.md`.

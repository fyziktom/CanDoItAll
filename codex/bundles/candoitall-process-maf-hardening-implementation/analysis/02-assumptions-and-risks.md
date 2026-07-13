# Assumptions And Risks

## Assumptions

- The blocked 5032 instance uses the current process/template architecture closely enough that the general repair remains valid even if exact run ids differ.
- `ExecutionRunQuery.ProcessStepId` support in AgentFramework can be used by the process observation reader with limited API changes.
- Typed `SubprocessContract` metadata can be added alongside existing legacy fields and then consumed by runtime and validators without breaking old templates.
- Parent bridge synthesis can write managed artifacts through existing process artifact infrastructure or a focused module integration service without moving project-structure-specific behavior into generic runtime core.
- `CanDoItAll.Processes.Runtime` should keep orchestration state-machine semantics; module integration should own external process launch, project-structure tool integration, and managed artifact file I/O where those are infrastructure concerns.
- Some existing process docs will remain explanatory, but all hard gates must become machine-readable.

## Critical Path Risks

- If SB01 misses a subprocess parent or terminal child branch, later template hardening will falsely pass while another process can still reproduce the same loop.
- If SB02 and SB03 do not establish exact diagnostics and structured summaries first, later bridge/preflight blockers may remain opaque in operator action and rework.
- If SB04 places typed subprocess contracts in the wrong project, dependency direction can force runtime/core to reference template implementation or module integration incorrectly.
- If SB05 only gathers generic child step refs instead of validating accepted/no-go mappings, it recreates GPTPro F04 under a new service name.
- If SB06 continues using synthetic slot hashes, downstream steps may appear connected while managed artifact content is stale, missing, or not parent-owned.
- If SB07 checks only agent metadata and not the composed runtime provider for the governed context, missing tool loops remain.
- If SB08 edits only `prepare-solution-skeleton`, the software-delivery and nested .NET slice parents remain vulnerable.
- If SB09 relies on live LLM runs, proof will be slow, flaky, and unable to catch shallow implementations deterministically.

## Validation Risks

- Some tests may need fixture builders for process run state, child run terminal state, assignment artifacts, and AgentFramework execution records. Plan these as test infrastructure, not production fallback.
- The currently blocked 5032 instance may not be recoverable in an automated test environment. The bundle requires a recovery playbook plus deterministic local regression tests, not live-instance-only proof.
- UI/browser proof is not primary, but operator action text is user-visible through projections. If implementation changes Blazor rendering or action views, Playwright proof must be added by the executing subbundle.
- CodeAnalytics dependency results must be refreshed after any project-reference or large-class extraction changes.
- Existing tests may not isolate process runtime/application/module integration well. Subbundles must add narrow tests around extracted services so closure does not depend on full app host construction.

## Reopen Triggers

- A new or existing subprocess parent is found outside the nine enumerated parents.
- Any template prose references an accepted, repaired, skipped, no-go, required receipt, or completion path that lacks typed metadata after SB08.
- A parent bridge completion can pass with child folder existence only, without accepted child artifact proof.
- Operator action still says only `No AgentFramework result summary was found` while runtime receipt diagnostics exist.
- A required tool absence launches an agent before being reported as a deterministic preflight diagnostic.
- Artifact ledger events are created from a result that finalization downgraded to `NeedsManager`.
- Produced artifact identity changes across identical managed content without an intentional content change.
- Unit tests for extracted services still instantiate `ProcessRuntimeProjectionQueryService`, `AgentFrameworkProcessExecutionAdapter`, or other large owner classes as the only way to exercise the new behavior.
- A new partial file becomes the final boundary for bridge, descriptors, preflight, template validation, or blocked-packet behavior.

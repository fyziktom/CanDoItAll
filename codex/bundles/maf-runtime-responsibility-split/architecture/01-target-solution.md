# Target Solution

## Responsibility Boundaries

| Target area | Proposed owner | Scope | Not owned |
| --- | --- | --- | --- |
| Runtime orchestration | `MafAgentRuntime` | Public `RunAsync`, approval response entrypoint, high-level collaborator coordination, dependency construction from DI. | Finalizer repair internals, session construction internals, hashing, argument formatting, model policy, manifest building. |
| Stable text/content hashing | `CanDoItAll.SharedKernel.Common` or `CanDoItAll.SharedKernel.Text` | General stable SHA-256 helpers with explicit full-hash and short-display variants if both are needed. | MAF-specific argument formatting or process-plan-specific canonicalization. |
| MAF argument formatting | `MafToolInvocationArgumentFormatter` or equivalent internal MAF helper | Display-safe argument summaries, truncation, JSON/object formatting, MAF diagnostic signatures. | Whole-project content hashing policy. |
| Session building | `MafAgentSessionBuilder` or `MafRuntimeSessionBuilder` | Restore/create `AgentSession`, create prompt messages, build run options, structured response format, streaming snapshot helpers, provider history decisions. | Finalizer outcome validation or provider usage accounting. |
| Model parameters | `MafModelParametersBuilder` | `ChatOptions`, temperature omission/retry rules, reasoning effort mapping, unsupported transport diagnostics, runtime model resolution. | Provider execution, retry loops not caused by model parameter compatibility. |
| Context manifest | `MafContextManifestBuilder` | `AgentRuntimeContextAssemblyManifest` creation, source records, token estimates, schema char estimates. | Capability attachment policy decisions. |
| Finalizer driver or strategy | `MafFinalizerDriver`, `RequiredFinalizerDriver`, or strategy set | Required finalizer repair, JSON repair, streamed capture, sequence validation, process artifact recovery, finalizer response building, finalizer usage observations. | General run orchestration and unrelated provider failure formatting. |
| Runtime cleanup checkpoint | SB07 implementation | Remove dead partial methods, wire collaborators, enforce file-size thresholds, ensure no responsibility moved into another catch-all. | Broad capability system refactor already covered by other bundles. |

## Design Rules

- Prefer concrete internal collaborators with constructor-injected dependencies. Add interfaces only for test seams that cannot be covered otherwise.
- Preserve existing exception types and diagnostic messages unless a subbundle explicitly updates tests and documents the change.
- Keep reusable hashing helpers free of MAF dependencies.
- Keep MAF helpers in the MAF project if they depend on `ToolCallContent`, `AgentResponse`, MAF runtime options, or MAF diagnostics.
- Use records or options objects for builder input when method argument lists grow or multiple methods need the same state.
- Avoid a "RuntimeHelpers" dumping ground. Each helper class must have one named responsibility and direct tests.

## Behavioral Invariants

- Required finalizers remain authoritative over assistant prose when required mode is active.
- Required finalizer validation still fails for missing, malformed, repeated, or post-validation tool calls.
- Provider failures after a valid required finalizer still persist the governed result and preserve failure diagnostics.
- Runtime session serialization still strips request-scoped attachment data and respects finalizer serialization timeout.
- Provider-managed conversation restoration and framework-managed history decisions remain unchanged.
- Context manifest included/excluded sources and totals remain stable for existing tests.
- Tool invocation signatures and repeated-tool diagnostics remain stable except where intentionally improved and tested.

## UI Validation Scope

- Refactor is backend-heavy, but UI proof is still required because agent chat, workflow, capability setup, and process shells display runtime state and diagnostics.
- Browser validation should use existing Playwright fixture infrastructure.
- First pass should use a large desktop viewport or headed maximized browser. Narrower viewport pass is required if any implementation touches UI layout or CSS. If no UI files change, narrower pass can be documented as not applicable after a large-screen smoke pass.

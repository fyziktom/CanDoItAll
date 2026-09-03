# Root Cause And Current State

## Incident conclusion

This is a combined model-input and application-handling defect. The model supplied a wrong signature; the application neither provided enough correction feedback nor represented the failed effect honestly. The node was not stored. An automatic refresh cannot display a node that was never created.

## Durable timeline

| Local time on 2026-09-03 (UTC−04:00) | Evidence |
|---|---|
| 11:58:55–12:00:13 | Prior run wrote architecture_overview.md, then invoked asset_create using project_id, parents, and request. The receipt failed, but the assistant claimed it had registered the asset. |
| 12:05:10 | User: “I do not see the new node. try it again”. Direct provider Local Ollama, model gemma4-12b-256k. |
| 12:05:52 | workspace_create_directory succeeded for docs/architecture. |
| 12:06:04 | workspace_write_file succeeded for docs/architecture/architecture_overview.md, 1,638 characters. |
| 12:06:10 | asset_create received project_id, parentNodeKey, property, source_type, source_type_detail, source_value, note. Required projectId and nested request were absent. Persisted result: Error: Function failed. Persisted receipt: Failed: Tool invocation failed. |
| 12:10:41 | Final assistant text promised a future corrected registration. No later function call or readback exists. |
| Capture | Run was Completed (5), Succeeded (0), three tool calls. Canonical graph still had five nodes; Main had no child. |
| 12:28:14 onward | Identified Web process 38720 stopped; subsequent verification confirmed no listener on 5032. |

The run attached 58 module runtime tools, including 55 Project Structure tools, plus workspace/capability tools. Tool availability was not the blocker. Permissions allowed non-task structure and workspace writing; the run log says effective auto-approval was active. The stored request preference remained false: that field alone is not an effective-policy report.

## Confirmed findings

**F01 — Argument-binding errors lose actionable feedback.** The asset factory at ProjectStructureAgentRuntimeToolProvider.cs:424 accepts `(Guid projectId, ProjectStructureAgentAssetCreateInput request, int? estimatedMinutes, CancellationToken)`. A probe using the actual DTO, unchanged signature, existing Release SDK assemblies, and a no-op delegate reproduced `ArgumentException: ...missing a value for the required parameter 'projectId'` with zero delegate invocations. The corrected nested payload invoked the delegate once. MafRuntimeAgentFactory.cs:668 maps only IAgentToolFailure; its generic catch at :687 wraps other exceptions in MafToolInvocationBoundaryException. The saved SDK tool result is the unhelpful generic string. Do not fix this by globally exposing detailed exception messages.

**F02 — Ordinary runs equate returned prose with success despite failed tools.** AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:601 and :1451 validate structured output, then choose Completed when there are no pending approvals and no invalid portable schema. They do not assess unresolved failed mutations in ToolInvocationTraces. Both reported attempts reached Succeeded. Existing required-finalizer logic is stronger for governed structured runs; interactive runs do not inherit that guarantee.

**F03 — Next-turn replay preserves a false claim and drops the failure that contradicts it.** The second run's serialized history begins with four prior user/assistant text messages and has no previous function call/result. ExecutionRuns.cs:685 clears session compatibility; ChatSessionRuntimeCompatibilityAdapter.CreateRuntimeSession (:75) builds from the current run; a new run has no prior serialized state. MafRuntimeSessionBuilder.CreatePromptInputMessages (:263) falls back to transcript text. This ownership split can be intentional, but it lacks a provider-neutral projection of prior relevant tool outcomes. Do not restore old provider sessions or approval state wholesale.

**F04 — Public receipt DTOs conceal outcome.** AgentApiResponseContracts.ToToolReceipts (:377) omits ExitSummary and has no typed success/error/commit field. The safe HTTP output shows an invocation without its failure. Raw ExitSummary and RequestSummary cannot simply be exposed: they may contain sensitive text. Persist and publish bounded typed outcome and explicitly safe diagnostics.

**F05 — Unknown result shapes default to success.** MafRuntimeToolInvocationResultClassifier.IsSuccessful (:33) returns true if its reflection/text heuristics cannot resolve a status. The diagnostic probe returned true for `Error: Function failed.`. This was not the immediate cause of the reported failure trace—the thrown exception already set that trace to failure—but it is a confirmed adjacent classification defect. Null/unknown and ambiguous envelopes must not prove a mutation succeeded. Preserve supported read-result semantics with explicit adapters.

## Transport findings and limits

Direct: MafProviderAgentFactory.CreateOllamaAgent (:362) → OllamaSharp native /api/chat → tool-result normalization handler. Shared: SharedProviderRuntimeProfileMaterializer (:132) deliberately exposes an OpenAI-compatible profile → MafProviderAgentFactory OpenAI branch → SharedProviderRuntimeHttpClientSelector → shared source /api/shared-providers/openai/v1/chat/completions → SharedProviderOllamaRelayAdapter → Ollama /v1/chat/completions. SDK details differ below the local tool loop.

The probe captured both SDKs with fake HTTP handlers. Both preserve projectId, request, nested properties, enum symbols, parentNodeKey, and sourceWorkspacePath. Native adds additionalProperties=false. No schema-stripping cause was found. The probe is not a complete source relay test or a live-model conformance test. SharedProviderRelayRequestPolicy also validates bounded tool messages and call identifiers; verify those constraints through the real relay in SB04/SB06. Do not add shared-provider branches to agent business behavior.

## File, asset, and refresh findings

The workspace file and managed project asset are separate durable effects. File creation does not register a graph node. The expected managed path is derived by ProjectStructureAgentService.CreateAssetAsync, with authorization, parent validation, managed storage, and metadata; neither the model nor a retry handler should invent it. The current runtime guidance already tells the agent to use request.sourceWorkspacePath and read back metadata/content. Prompt-only guidance did not close the incident.

ProjectStructurePage.razor:60 connects RefreshRequested; ProjectStructureAgentChatContextProvider subscribes at :160; AgentChatExecutionOrchestrator.PublishCompletionAsync (:305) publishes through AgentChatExecutionNotificationHub; ProjectStructurePage.AgentWindows.cs:22 reloads the matching project. This run offers no evidence that the callback itself failed.

**F06 — Success-only refresh is insufficient for partial effects.** AgentChatContextInvocationFactory.CreateCompletionNotification (:139) rejects failed/cancelled runs. Once honest completion is implemented, a committed node followed by a later failure still needs refresh. Publish effects-aware, scoped invalidation and reread canonical state; never infer commit from prose.

**F07 — Post-commit telemetry can obscure a committed mutation.** ProjectStructureToolBuilder.ExecuteAsync (:2531) awaits action before analyticsService.RecordAsync. A later analytics exception propagates through the same error path and can hide an already committed asset. This is a source-confirmed failure window, not observed in this run (binding failed first). Any new automatic correction must distinguish NotExecuted, Committed, and Unknown; never retry an unknown side effect automatically. Test analytics failure after commit and cancellation at the boundary.

## Architecture assessment

CodeAnalytics snapshot snap-20260903162319-aa914253 loaded six projects / 420 documents, with four DI collector diagnostics and no blocking workspace error. It reports no project cycle within that selected scope, plus existing Workbench module and ProjectPackageService/ProjectPackageStorageImporter type cycles. This is not whole-solution cycle proof. Full project references are recorded separately from the scope-filtered snapshot.

MafAgentRuntime is already a 146-line composition facade. Do not re-centralize behavior there. Responsibility hotspots are MafRuntimeAgentFactory (898 lines), MafStreamingTurnExecutor (1,307), the heuristic classifier (568), and ProjectStructureAgentRuntimeToolProvider (3,439 lines; a nested builder with 127 members and 18 constructor parameters). Their sizes support targeted extraction when touching responsibilities, not a general rewrite.

No production fixes, model calls, real mutation replay, browser regression run, or full test-suite run occurred during preparation.

## MAF 1.20 addendum

The follow-up assessment is in [03-maf-1-20-assessment.md](03-maf-1-20-assessment.md). MAF 1.20 plus MEAI 10.9 still throws the same missing-projectId ArgumentException for the captured malformed shape. The update is now SB00, an SDK/dependency baseline before the application-owned repair; it does not supersede F01–F07.

# Audited baseline and architecture

## Provenance and drift

Repository: `fyziktom/CanDoItAll`; branch: `development`; inspected HEAD: `ffa83cf903c305a7490a674f41a0d08498156db6`; tree: `9659ba21653ee3a46a6fb444e887b689454da078`. The commit, dated 2026-09-23T16:41:11Z, merges `postgresql-update` into development. This is a read-only review, not a validation run. Source identifiers below resolve through [SOURCE_INDEX.md](reference/SOURCE_INDEX.md).

GitHub code search was indexed on the default branch, not necessarily development. It was used to locate files; substantive findings were checked by fetching the development SHA. Test names found in tree listings are **existing locators**, not evidence that they cover or pass the new scenarios.

Before editing, compare actual HEAD and package/reference layout with this audit. Do not resurrect retired paths or automatically migrate backwards if the current branch is already newer. Resolve and explain such drift while retaining the requested compatibility and reliability outcomes.

## Current package/build facts

| Owner | Audited value | Required treatment |
|---|---|---|
| `src/MAF/MicrosoftAgentFramework.Packages.props` [R04] | Stable 1.20.0; preview 1.20.0-preview.260831.1 | Stable 1.22.0; used A2A preview 1.22.0-preview.260918.1 |
| `Directory.Build.props` [R05] | Extensions.AI 10.9.0; general Extensions 10.0.11 | Reconcile with target floors, including AI 10.10.0 and relevant general packages 10.0.12 |
| MAF adapter `.csproj` [R06] | OpenAI 2.12.0; OpenTelemetry.Api 1.15.3 | Target metadata requires at least OpenAI 2.13.0 and OpenTelemetry.Api 1.18.0 in the relevant graph |
| Same project | ModelContextProtocol 1.1.0; OllamaSharp 5.4.25 | Inspect compatibility; no blind unrelated bump |
| `global.json` [R07] | SDK 10.0.302, latestPatch, no prerelease | Honor resolved SDK and net10.0; no unrelated retarget |
| `Directory.Build.props` | ASP.NET Components 10.0.10 | Do not assume every package shares the same version family; change only with graph justification |
| CI [R03] | Sibling source graph; FileTools commit 498b36825bd5a5222429972af120b04becf4b3f6 | Re-resolve from current CI and record exact sibling revisions |
| Testing/CI [R02/R03] | Isolated PostgreSQL 18 | Verify server major and provenance; do not substitute InMemory or SQLite for required persistence proof |

NuGet floors are package metadata, not a completed restore. Keep the smallest coherent supported graph, inspect `project.assets.json` and evaluated references, and preserve existing version ownership instead of scattering literal pins. Record why each additional dependency changes. Test source and package paths where the current project supports both; do not mix them within one claimed validation checkpoint.

## Current boundaries

`CanDoItAll.slnx` contains production projects only. Tests live in dedicated solutions under `tests/Solutions`. `src/App/CanDoItAll.Web` hosts Blazor/API; `src/App/CanDoItAll.Composition` composes modules; `src/Modules` owns product behavior; `src/Processes` owns provider-neutral process contracts/runtime; `src/MAF` contains provider/framework adapters; foundation projects own shared infrastructure.

The MAF adapter README [R08] describes **four narrow runtime ports**, not the deleted broad `IAgentRuntime`: execution, continuation, provider diagnostics and provider model administration. `MafAgentRuntime` is a composition root; streaming lives in `Runtime/Execution/MafStreamingTurnExecutor.cs`. Locate current implementations through CodeAnalytics rather than putting execution logic back in the composition root.

MAF must not reference product `CanDoItAll.Modules.*` or the `Workflows.MafAdapter` project. First-party tools arrive through module-owned `IAgentRuntimeToolProvider` registrations. Process execution uses the Processes-to-Agents hosting bridge. Direct `processes_*` runtime tools are not an established current surface; do not invent their existence to repair a prompt. Use registered tools and the existing HTTP control plane.

## Main inspected execution paths

| Concern | Reviewed entry point | Important observation |
|---|---|---|
| Approval DTO/cache | `Runtime/MafApprovalContinuationDriver.cs` [R09] | Cache equality compares IDs only; native state remains a separate authority |
| Native restoration | `Runtime/MafRuntimeSessionBuilder.cs` [R10] | Calls `DeserializeSessionAsync`; continuation with missing/incompatible state fails closed |
| Versioned state | `MafRuntimeStateAdapter.cs`, `MafRuntimeStateCompatibilityPolicy.cs` [R11/R12] | Runtime assembly version captured; existing package test accepts equal majors and unknown versions |
| Durable protocol | `Runtime/Admission/MafToolRunContext.cs` [R13] | Replays admitted segments, SDK checkpoints and immutable proposal bindings; dynamic context tools register here |
| MCP | `Runtime/Capabilities/McpCapabilityBuilder.cs` [R14] | Application-owned client factory plus hosted/local branches; this is not proof of declarative MCP handler usage |
| A2A | `Runtime/Capabilities/A2ARemoteAgentToolFactory.cs` [R15] | Creates HTTP client, resolves card and projects remote skills into tools |
| Workflow | `Workflows.MafAdapter/MafWorkflowCompiler.cs` [R16] | Imperative `WorkflowBuilder`, explicit end-node outputs, typed routing and native external requests |
| Process agent step | `AgentFrameworkProcessStepExecutor.cs` [R17] | Dispatch claim, preflight, admission, typed finalizer, subprocess reuse, completion evidence |
| Recovery | `ProcessAgentExecutionRecoveryPolicy.cs`, `ProcessRuntimeFailureClassifier.cs` [R18/R19] | String classification plus conservative durable no-side-effect attestation |
| Manager | `ProcessManagerControlLoop.Recovery.cs` [R20] | Policy and loop-budget decisions; manager involvement alone does not mean human escalation |
| Chat shell | `FloatingAgentChatHost.razor` [R21] | Compatibility wrapper renders `ConversationShellHost`; do not put business state into the wrapper |

Paths in the table are shortened where unambiguous; full pinned links are in the source index. The audit did not review every file or every provider. Codex must close the remaining applicability and caller-chain checks using the current checkout.

## UI and infrastructure scope

Preserve conversation-shell contribution/state ownership, module UI seams, current SSR behavior and API security deployment modes. No new authentication or full tenancy model is part of this upgrade. Shared non-WebGL UI belongs in Components; use the current Components MCP/shared instructions when changes there are genuinely needed. Do not add duplicate UI components or new Radzen dependencies.

Keep the newly merged PostgreSQL 18 infrastructure. Generic Memory remains disabled by default; Cognitive Memory, Qdrant and SQLite are not prerequisites of a basic agent/process smoke test. Optional integrations need their own applicability and prerequisite evidence, not activation by default.

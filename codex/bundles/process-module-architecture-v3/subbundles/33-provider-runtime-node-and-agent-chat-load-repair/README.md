# SB33 Provider Failure, Runtime Node, And Agent Chat Load Repair

## Status

- Completed on 2026-06-20

## Objective

Repair the post-SB32 runtime handoff and operator experience issues observed on the running development instance: provider quota or credit failures must be clear, .NET runtime project-structure nodes must be launchable from typed metadata even when previous writes placed command details in notes, and contextual agent chat must stop blocking the UI on full execution document loads.

## Covered Inputs

- User follow-up on 2026-06-20: `bundle://inputs/post-sb32-provider-runtime-chat-followup-20260620.md`.
- SB31 runtime launch/readiness proof: `bundle://proof/SB31-project-structure-launch-staffing-readiness-and-runtime-sequence-repair/manifest.md`.
- SB32 staffing and Live Processes repair proof: `bundle://proof/SB32-live-processes-staffing-ui-and-active-agent-repair/manifest.md`.

## Prerequisites

- SB31 and SB32 are completed and trusted.
- The running development instance reproduces provider failure clarity, runtime-node launchability, or contextual chat latency symptoms.
- Focused unit, integration, and web build validation can run against active source.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ChatBootstrap.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceChatProjectionStore.cs`
- `repo://src/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`
- `repo://Templates/Processes/processes/dotnet-runtime-command-writeback/definition.json`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://tests/CanDoItAll.Tests.Unit`
- `repo://tests/CanDoItAll.Tests.Integration`

## Deliverables

- Redacted provider quota/billing failure display normalization.
- Runtime-node launch resolution from typed metadata and safe legacy note-only evidence.
- Runtime command templates requiring launcher-compatible receipts.
- Split-store contextual chat session list/create/rename/bootstrap paths.
- Immediate contextual chat shell open with busy state.

## Implementation Steps

- Add provider failure display normalization for OpenAI-family billing, credit, and quota exhaustion, preserving actionable provider detail without leaking secrets.
- Persist normalized failure messages into execution run summaries, execution logs, and user-facing run exceptions.
- Repair .NET runtime launch resolution so a dotnet runtime node with typed metadata resolves like PowerShell/script runtime nodes, and safely recovers the project path/protocol from legacy note-only command evidence when possible.
- Tighten runtime command templates so JSON source definitions require launcher-compatible metadata receipts, not note-only command writeback.
- Move chat session list, create, rename, and latest-run bootstrap paths onto the existing split chat projection/session stores instead of loading or rewriting the full execution document.
- Open the contextual agent chat shell immediately and show busy state while the session/workspace loads.

## Dependency Impact

- Runtime-node launch actions in project structure depend on the repaired metadata resolver and command-template receipt contract.
- Contextual chat UI depends on split chat/session stores without forcing full execution document reads.
- Later e2e validation must restart the app on the repaired build before measuring after-deploy chat latency or runtime-node double-click behavior.

## Validation Depth

- Focused unit validation for provider failure normalization, runtime-node launch plan resolution, and contextual chat shell behavior.
- Focused integration validation for split chat/session storage paths.
- Web build validation for Blazor/UI compilation.
- API or browser validation to record the live bottleneck/root cause and any restart-validation gap.

## Do Not Do

- Do not hide provider failures behind generic runtime errors.
- Do not silently accept non-launchable runtime node writes as completed runtime proof.
- Do not reintroduce full execution document reads into the chat open path.
- Do not fake run actions for nodes when no safe launch plan can be resolved.
- Do not change unrelated Process runtime, staffing, or Live Processes behavior.

## Acceptance Checklist

- [x] OpenAI-family insufficient quota, credit, or billing errors show a clear user-facing provider message.
- [x] Runtime failure logs and run summaries use the normalized provider message.
- [x] Existing note-only `.NET runtime` command evidence can produce a launcher plan when it contains a concrete project root and `dotnet run --project` command.
- [x] Future runtime command writeback JSON requires typed launcher-compatible metadata receipts.
- [x] Project-structure node action capabilities expose `runtime:open` and `runtime:admin` for a resolvable `.NET runtime` node.
- [x] `ListChatSessionsAsync`, `GetOrCreateChatSessionAsync`, and `RenameChatSessionAsync` avoid full execution document load/update when split chat stores are available.
- [x] Contextual agent chat opens the window immediately with busy state instead of waiting for backend session creation.
- [x] Focused unit/integration/build validation passes.
- [x] Browser or API proof records the before/after latency root cause and remaining validation gaps.

## Proof Required

- `proof/SB33-provider-runtime-node-and-agent-chat-load-repair/manifest.md`
- `proof/SB33-provider-runtime-node-and-agent-chat-load-repair/semantic-invariants.md`
- `proof/SB33-provider-runtime-node-and-agent-chat-load-repair/changed-file-hashes.txt`
- Focused test/build transcripts under `proof/SB33-provider-runtime-node-and-agent-chat-load-repair/transcripts/`.
- Runtime/API/browser validation artifacts under `proof/SB33-provider-runtime-node-and-agent-chat-load-repair/`.

## Browser Validation Logging

- Browser proof is optional when API evidence proves the latency root cause and web build/unit coverage proves the UI behavior.
- If browser proof is captured, record route, viewport, chat open action, visible busy state, screenshot, console/network summary, and pass/fail result.

## Progression Gate

- The chat performance repair is safe only if it uses the existing split storage projections as the source of truth and preserves legacy full-document behavior for stores that do not implement the faster chat session interfaces.

## Closure Result

Completed with focused proof under `bundle://proof/SB33-provider-runtime-node-and-agent-chat-load-repair/`. The live 5032 instance was not restarted, so API proof records the before-deploy bottleneck and runtime-node metadata gap; code validation proves the repaired paths through focused unit, integration, and web build transcripts.

## Suggested Agent Prompt

Execute SB33 from `codex/bundles/process-module-architecture-v3/subbundles/33-provider-runtime-node-and-agent-chat-load-repair`. Preserve provider error redaction, runtime-node launch safety, and split chat-store semantics while repairing provider failure messages, .NET runtime node launchability, and contextual chat load latency. Validate with focused unit/integration/web-build checks and API or browser proof that records live latency/root-cause evidence and restart gaps.

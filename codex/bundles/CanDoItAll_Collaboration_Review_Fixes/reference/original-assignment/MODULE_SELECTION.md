# Module selection and remaining work

## Decision

**Select Collaboration for this assignment.** Extract the whole existing `/collaboration` workspace rather than a cosmetic sub-panel. Test Lab is the strongest next candidate after Collaboration, subject to a fresh check when that assignment starts. Processes and Workbench belong in the final large-module group; their relative ordering should follow actual remaining dependency cuts, not a fixed file-count ranking.

Review: `development`, `7db3543ab437376baeca55089cb331fbe1b30483`, 2026-09-28. This equals shared v3's review revision. Current branch metadata was read through the connected GitHub interface. The canonical seams guidance, UI and sandbox directories and selected candidate code were reread; the remaining-module metadata below also uses the supplied shared v3 source register at that identical commit. **This is a selection audit, not source-level certification or executed testing of every module.** Sources are in [SOURCES.md](SOURCES.md) and the unchanged [shared module map](shared/audit/module-map.md).

## Already established seams: do not start over

| Family | Current evidence and interpretation |
|---|---|
| Agents | Existing `src/UI/CanDoItAll.AgentFramework.UI`; canonical guidance identifies the Agents workspace among completed extractions. Keep the established seam. This does not certify every historical AgentFramework-related surface. |
| Prompt Gallery | Existing Prompts contracts/UI library, owner sessions and sandbox; canonical worked example. Do not reopen the whole module for this task. |
| CRM / HR | Existing CrmHr contracts/UI library and sandbox; canonical guidance explicitly covers seven workspaces. Keep those boundaries and use them as precedent. |
| RecordBrowsing and shared conversation rendering | Established reusable families, not additional business modules to re-extract. Inspect their actual closure before reuse. Collaboration currently does not need to adopt the entire conversation shell. |
| Workflows and Simple Chats | Rendering projects also exist under `src/MAF`, outside `src/UI`. Their presence prevents declaring them unextracted merely because no project appears under `src/UI`. Whole-feature completion needs its own coverage check, not a new duplicate UI library. |

The first three rows are supported by the freshly read canonical seams guidance and current UI/sandbox directories (C02/C19/C28). The sandbox directory currently lists AgentFramework, CrmHr and Prompts; Collaboration is not yet present there. The narrower families and MAF project placements retain shared v3 S17/S18/S38/S39 evidence at the same revision.

## Remaining mixed owner/UI modules

These are **remaining candidates**, not ten whole-module implementations claimed to have been exhaustively audited. A backend implementation may correctly remain heavy after its renderer is extracted.

| Candidate | Observed reason it remains relevant | Suggested scheduling treatment |
|---|---|---|
| **Collaboration** | One active workspace page and code-behind, rendering directly coupled to its owner assembly; read/editor contracts and enums live beside EF implementation. | **Now: bounded complete workspace cut.** |
| **TestLab** | One `TestLabPage.razor`, but its actual form includes project selection, responsible-party integration and structured coverage/case/evidence/run editing. | Next small/medium candidate; preserve project admission and committed-save/projection behavior. |
| **SchedulerPlanner** | Mixed Razor owner with Quartz hosting and workflow/agent execution dependencies in project metadata. | Later, bounded planning/editor surface without starting dispatch workers in a sandbox. |
| **Plugins** | Mixed Razor owner with EF/security plus agent/workflow/executor implementation dependencies. | Later; separate catalog/settings/status while preserving permissions and install/execution effects. Few visible pages would not make it automatically low-risk. |
| **Memory** | Many provider-oriented renderers, profile/capability/transport editors and a management page remain within the module tree. | After small modules, likely several coherent surfaces. Preserve provider neutrality; no provider startup in preview. |
| **Workspace** | Owner metadata includes Infrastructure, Projects, Security and provider-history/http/identity-related dependencies. | Inspect settings surfaces individually; credentials/profile/authority behavior increases risk beyond simple layout extraction. |
| **Projects** | Narrow Projects.Contracts already exists, but the module retains broader ownership and UI/integration dependencies. | Later; reuse contracts rather than claiming the whole module is done. Preserve lifecycle/admission/file/assignment consumers. |
| **Resources** | Cross-module references include file integration, agents/Memory, Projects, Security and Workspace. | Later; distinguish resource rendering from bytes, project attachment and access control. |
| **Workbench** | Composition-heavy owner across Projects, Processes, conversations, files, workflows and Memory. | Final large-module group; coherent surfaces rather than a global fake workspace. |
| **Processes** | Multiple existing application/builder/persistence/runtime/projection/driver layers plus product UI and HTTP control plane. | Final large-module group; no Processes redesign or extraction in this assignment. |

Collaboration is freshly inspected through its page, contracts and owner implementation (C03–C12). Test Lab is checked at page-tree and initial form-source level (C17/C27), not a complete module audit. Memory is checked at module-tree level (C18), not a provider lifecycle audit. The other rows use shared v3's documented metadata S26–S35 at the same revision; they are deliberately not assigned numeric effort estimates or a rigid execution sequence.

Security and AgentFramework.ProviderManagement are backend projects in the retained metadata, not missing UI libraries to manufacture. Find their UI consumers in other owners when those surfaces are assigned. Contracts-only projects similarly do not require a sandbox.

## Why Collaboration before Test Lab or Memory

The current Collaboration tree contains one active feature Razor page plus its code-behind, and no separate local component/dialog/asset hierarchy to pull across (C03). Its inspected markup uses the BaseLib family, not project pickers, file browsers, process canvases or provider editors (C04/C11). Its owner already exposes plain requests/read values and an isolated four-entity persistence boundary (C06–C12). That offers a useful complete extraction with limited cross-owner behavior.

This is not a claim that Collaboration is trivial. It includes two forms, selection/query routing, unread state, asynchronous writes and the shared shell badge. Those are precisely enough to make the next extraction meaningful while keeping it smaller than a project/editor or runtime workspace. The source review found concrete lifecycle/post-commit hazards; they are included as narrow related corrections rather than deferred into a second implementation phase.

Test Lab is a good follow-on but already injects `ProjectWriteSelectionQuery` and `IProjectPartyIntegrationBridge` and carries a richer editor (C17). Its directory also exposes committed-save/projection-related types. Memory has a broader provider-management UI family, including HTTP/MCP transport and capability editors (C18). Neither is as isolated a starting point as Collaboration.

## Reassessment policy

After Collaboration, prepare a fresh Test Lab brief rather than automatically executing it. Recheck current source, existing seam coverage, project-write admission, commit/projection behavior and the real component closure. Keep SchedulerPlanner/Plugins and then the broader Memory/Workspace/Projects/Resources work as a flexible middle group. Keep Processes/Workbench at the end unless a narrowly justified prerequisite makes a small earlier surface necessary. No extra work is authorized by this ordering note.

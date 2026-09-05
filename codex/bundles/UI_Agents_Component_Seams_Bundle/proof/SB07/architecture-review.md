# Source and evidence review

This is a separate source/evidence review pass by the implementing agent, not a claim that another agent or human reviewed the change. It re-read production contracts, actual constructors, registered adapters, consumer lifetimes and the negative transcripts, rather than accepting the phase reports. The final verifier separately checks artifact/source identity and gate evidence.

## Findings resolved before final validation

1. Shared editor/catalog contracts initially created a new service-to-Pages namespace cycle. Contracts now use the module root namespace; the refreshed local analytics snapshot removed that introduced cycle. This does not pretend the assembly graph became lighter.
2. First-save TargetChanged can be echoed by a parent. Reloading that acknowledged ID replaced EditContext and lost later edits. The real host-echo test failed, then passed after retaining the current acknowledged session.
3. A stale old-target closed result after Clear could clear catalog selection. Host result handling now consumes the active presentation target. A further adversarial real-save test demonstrated that a check before catalog I/O alone was insufficient. The handler rechecks after awaited reload before publishing selection. Its ordinary-completion control passes too.
4. The adjacent Simple Chat composition test disposed its real database services before nested initialization had completed. Waiting for absence of loading text was insufficient because definitions load before conversation loading begins. The test now observes the normally constructed real conversation gateway's completed query, then checks rendered state. It neither fakes the catalog nor suppresses the child. Source behavior is unchanged.

5. The first real browser save exposed an additional page/catalog echo gap: the returned ID was published as selection before the host marked it already open, so the page parameter echo opened a second editor. A new real-page save/update test reproduced two dialogs. The host now marks the acknowledged identity before invoking SelectedAgentChanged. The corrected 130-case focused gate passes; the initial in-progress stable run was explicitly stopped/invalidated. The fresh final-* broad run supplies current acceptance: 9,597 passed, zero skipped.

## Responsibility and dependency verdict

- AgentsHomePage owns route mapping, presentation and existing chat context composition. AgentsWorkspaceState/AgentWorkspaceSection express semantic selection/view; AgentsWorkspaceQuery composes cohesive shell/usage reads. BoundAgentResourceQuery owns the concrete EF boundary. No new page DbContext access is hidden in a presenter.
- AgentCatalogPanel is controlled rendering, search/tree expansion and typed intent emission. It imports neither feature services nor dialog/chat launchers. AgentCatalogHost owns existing route-request echo, selection/context and dialog/chat effects. AgentCatalogOperations owns repair/load/team persistence. The no-service rendering test plus real operations tests challenge a fake separation.
- AgentDetailsDialog retains section presentation and UI orchestration, including existing control policies. Per-instance AgentEditorSession owns draft/EditContext/target/cancellation/pending reconciliation. AgentEditorReads/AgentEditorAccessQuery own reference/core loading and narrow project/secret metadata. AgentEditorCommands owns real workspace mutations and external-root preparation. AgentEditorDraftPolicy owns independent request capture and pure normalization. There is no interface around the session or pure policy.
- Every added interface is a real I/O boundary used by production and normally constructed in tests. The resource/access adapters intentionally isolate EF and Projects/Security implementation dependencies. There is no service locator, generic repository, injected service bag, new project, new partial file or sibling modification.
- The editor is still large because it renders ten feature sections and retains their UI policies. This phase does not disguise that fact. Further section extraction should be driven by useful scenario/asset ownership, not arbitrary line/member limits. Direct persistence and reference loading have actual new owners now.

AgentEditorDraftPolicy uses explicit typed copying, then serialization only as a structural equality snapshot to detect edits made during I/O. It does not deserialize or address properties through magic strings. Changes to AgentEditorModel's nested mutable state must update the copy and its ownership tests; this is a maintenance responsibility, not a claim of future automatic completeness.

## Analytics and graph limitations

SB05/architecture-audit.json records local snapshot snap-20260905025204-4cc89364, real selected types/dependencies and scope limits. Scoped project-reference output omits unselected projects; generated Razor host and TryAddScoped registrations are not reliably recovered. Actual source and registration/composition tests therefore supply those facts. The final Razor post-await guard is reviewed directly and included in current file hashes; the earlier snapshot is not falsely labeled a later host-body snapshot.

The only reported module/type cycles after contract correction were the existing AgentFramework/Hosting cycle and nested image-provider/builder cycle. They remain follow-up architecture debt. No project references changed. The evaluated 46 direct AgentFramework references and broader existing Models/Core/Voice/Conversations asset graph remain relevant to future physical extraction.

## Preservation, errors and scope

The B01–B30 map is in SB06/coverage-map.md. Real adapters and DB tests prove identity/version/settings/team operations; public delayed boundaries prove stale/reset/disposal behavior. Current full-save capability semantics, Clear-to-create, save staying open, optimistic conflicts and delete result channels remain explicit. Acknowledged refresh/callback failure cannot blindly replay a write. Unknown save outcome retains the draft and requires checking the catalog before reopening.

B12 remains the characterized existing blank editable form after core-load failure; no approval to fix that independent defect arrived. External AvatarPicker can still emit its own global notification after disposal, but cannot publish a stale parent draft. No route/history/DialogService, sandbox host, sibling, provider workspace or unrelated module implementation is included.

Final acceptance depends on actual stable/portability/browser evidence and verifier output in manifest.md. A passing source review alone cannot close those gates. One existing Integration build analyzer warning (xUnit2029 in FileSandboxWorkspacePreparedCommitReadIntegrationTests) is outside changed code; no warning suppression or unrelated cleanup was introduced.

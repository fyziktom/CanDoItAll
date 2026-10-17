# P07 settings UI and admission audit

Source-only review, 2026-10-17. No UI/runtime implementation, build, test, browser, database access or new proof count is claimed. The existing R2 producer-entry refresh and C4 policy/work-accounting cards remain the design authority; this note identifies their smallest existing presentation seams and concrete compatibility gates. It does not activate their proposed contracts.

## Existing surfaces and ownership

| Surface | Existing owner and useful behavior | Minimal future policy entry |
| --- | --- | --- |
| Workspace defaults | `WorkspaceDefaultsController.SaveAsync` records the exact accepted snapshot before local reconciliation, retains later edits, and keeps an unknown receipt distinct from a current read. The strict v1 values contract contains exactly six business fields. | Reuse this surface and namespace/head CAS. Follow C4's versioned v2 policy section and preserve that section during v1 writes under the owner lock. An omitted old field is not consent to clear policy. |
| Agent Runtime section | `AgentEditorCoreSurface` renders the host-owned draft/EditContext; `AgentDetailsDialog` captures the submitted draft and preserves acknowledged identity when refresh fails. Ordinary save returns an ID, not an exact accepted policy snapshot. | Use the existing Runtime section with C4's separate policy-only save and accepted policy/revision. Keep the ordinary draft and its original save basis independent; do not imply two saves are atomic. |
| Process definition and step editors | `ProcessDefinitionDraft` retains a dirty draft's original token; `ProcessStepEditorState` submits its synchronized original token and merges exact accepted fields while keeping later input. Real authoring adapters capture operation/fingerprint and use durable receipts and expected/inherited revision CAS. | Add typed definition defaults and step overrides to the existing governance/step editors, projections and authored documents. Carry them through compilation, publication and immutable prepared launch. Do not implement this only in the projection service's in-memory mode. |
| Workflow Definition inspector and selected-node settings | `WorkflowCanvasSurface.SaveAsync` submits the document's original VersionId. `WorkflowDocumentOwner` retains the exact accepted definition before returning; `WorkflowDocumentAcceptance` advances identity/baseline from that acknowledgement and retains later edits. Unknown saves block another save in that owner. | Add typed definition default/node override beside existing runtime/executor policy, with the same accepted-definition path. Reuse both node inspector and modal paths. Do not put semantic policy in executor-specific JSON. |

Hosts continue to own reads, writes, original expectations, current-view fencing and effect knowledge. Renderers receive typed state/intents; neither a displayed policy nor a disabled button supplies runtime authority. Keep the existing component seams and sandbox/host proof split. No new settings page, generic policy registry or cross-engine coordinator is needed.

## Two concrete writer risks

1. **Agent timestamp rebase is unsuitable for the new policy.** `AgentFrameworkWorkspaceCatalogService.Agents.cs:34–58` checks `ExpectedUpdatedAtUtc` only when supplied. `AgentDefinitionFactory.cs:22–41` rewrites typed metadata starting from submitted ConfigurationJson. After save, `AgentDetailsDialog.razor.cs:612–625` loads a later catalog observation; if there are later local edits, line621 assigns that GET's timestamp to the retained whole draft. This is not the exact acknowledgement of the submitted policy and can rebase over an intervening writer. C4 already chooses the narrow remedy: reserved strict policy metadata, a policy-only command bound to current workspace/Agent and monotonic CatalogDataRevision, exact accepted policy/new expectation, and preservation of that reserved section by legacy whole-Agent/import writers. Raw JSON and an omitted typed value must not become alternate policy writes. This audit does not authorize a whole-Agent refactor.
2. **Workflow native original tokens do not close permissive owner entry points.** Native save passes `document.VersionId` (`WorkflowCanvasSurface.razor.cs:849`) and records Accepted before callbacks. However `PersistentWorkflowStores.cs:226–230` performs the version conflict check only when `ExpectedVersionId` is present. Adding a required field to the native editor alone cannot prevent an older replacement/import/API writer from removing new policy. The planned compatibility rule must close governed existing-definition replacement at the actual owner and preserve legacy serialized meaning. A new saved definition version also cannot repin an already admitted run.

Process's production path is materially different from its optional in-memory projection implementation: `ProcessAuthoringWorkspace.cs:74–79` permits a missing token only at revision zero, compares the full original observation otherwise, and passes expected/inherited revisions to the store. `EfProcessAuthoringStore.cs:141–143` compares both under the owner transaction. Extend those real contracts and their returned receipts; do not copy the weaker optional check in the projection-only fallback.

## Supported distinctions and serialization

Source inspection found no `DecisionDataPolicy`, `ReadBasisMode`, `PublicationFreshness` or `OnBasisChange` use in the current Agent/Process/Workflow presentation surfaces. SharedKernel supplies validated values, codec and pure resolution; the dormant Workbench task-plan owner consumes the policy. This is not an implemented end-to-end persisted settings journey.

- **Read basis:** RunSnapshot retains the declared basis for a decision generation; StepSnapshot refreshes only when a new logical step is admitted. Same-step retry, approval delivery and claim rotation retain the original basis. Do not describe this as freezing all workspace data.
- **Publication freshness:** PinnedObservation and RequireCurrent are separate from read mode. Owner-mandatory currentness cannot be weakened by settings. The current task-plan publisher requires RequireCurrent, accepts zero-replan policy, and checks wall-clock decision/basis age (`ProjectTaskPlanPublisher.cs:222–233`). It does not itself provide a producer's durable pause or replan journey.
- **Changed-basis response:** Fail, PauseForReview and Replan need distinct runtime outcomes. The existing Workflow executor editor controls timeout, retries, delay and artifact capture; those retries are not semantic replans. Existing Process governance/exception summaries and Waiting states do not implement these new responses.
- **Defaults:** `DecisionDataPolicy.PlanningDefault` is an explicit factory for StepSnapshot/RequireCurrent/PauseForReview/zero replans and requires work-duration and basis-age arguments. It is not an installed persisted default. Preserve legacy absence/inheritance and original prepared bytes; never load old evidence into today's defaults. The actual admission resolves and saves effective policy/provenance before dependent provider work; later settings edits apply to new decisions.
- **Wire shape:** The current full-policy codec is bounded to 1 KiB, requires schema1 and every known field, rejects unknown/duplicate/missing values and invalid combinations, writes numeric modes and invariant TimeSpan `c` strings. Replan requires a positive count; other responses require zero. Future nullable override/inheritance settings need their explicitly versioned contract rather than treating the full policy's strict missing-field behavior as inheritance.
- **Work and age:** C4's active-work/human-wait ledger is a future versioned contract. Current C3 wall time, basis age and proposed active work are not interchangeable UI labels. Open-crash mutation resume remains blocked unless the trusted settlement/current-admission gate is implemented. Historical receipt review is read-only and cannot satisfy it.

## Before UI implementation

Freeze the owner contracts and compatibility first: Workspace v1/v2 preservation, exact Agent policy acknowledgement, Process authored-policy/prepared-launch identity, and Workflow governed replacement/version pinning. Then extend the existing typed drafts/capture/comparison/acceptance paths, preserving original tokens and later edits. Expose only capabilities supported by the actual producer; unsupported Pause/Replan must fail before model work without silently downgrading.

The run presentation needs saved effective policy and its source, separate captured-facts/current-publication semantics, and distinct paused versus uncertain knowledge. Typed Cancel/Refresh must carry the displayed decision, semantic generation and owner revision. A no-SDK-approval pause cannot reuse approval APIs; existing Process Waiting or Workflow checkpoints are not proof of resumability. Unknown commit, checkpoint, release or activity-finalization knowledge must remain sticky and block replacement generations.

Later proof must cover real settings roundtrip and old-writer preservation, stale original token, accepted reply versus later edits, unknown acknowledgement without automatic retry, exact effective admission, and actual next-step versus same-step/approval/restart behavior. Existing contract tests and this source review do not establish those runtime effects. No prospective count is promoted to authored or passing evidence here.

## Source pins

All paths below are relative to the reviewed managed checkout. These are the relevant decision inputs, not a framework-wide freeze. Hashes identify source inspected for this note; line anchors above are explanatory. Root transfer and execution remain separate.

| Path | SHA-256 |
| --- | --- |
| `codex/bundles/concurrent-mutations/reviews/P07-R2-producer-entry-refresh.md` | `C875104EECDE50E7BA74750B1C6940B383F99C3202C7B68B2809475F58671DB2` |
| `codex/bundles/concurrent-mutations/plan/P04-C4-ordinary-agent-policy-activation.md` | `B7F8CFD4EDB884EFA1EB8D07CE20F56B438F73BC2F0C03F84848E60DCB588445` |
| `codex/bundles/concurrent-mutations/plan/P04-C4-D1-policy-work-accounting-amendment.md` | `712B72AA8A5EB1A68795FE35FD920553A196725ECDF83FB51D1BDD199BFABEC1` |
| `docs/architecture/ui-component-seams.md` | `BF85B3FE4ACC4A521DCF16B85EF252D064A66C136F90554A0399E421409808A7` |
| `src/Foundation/CanDoItAll.SharedKernel/Concurrency/DecisionDataPolicy.cs` | `CDB9525CDFECC18F509B23D1D50125F092F78C14CEEF1E43FAE6BC0D697A763C` |
| `src/Foundation/CanDoItAll.SharedKernel/Concurrency/DecisionDataPolicyCodec.cs` | `87EA936C0FD57B3139E91E7F010B8C33ED89BD9843BAA5D08987E7AB8E36B27C` |
| `src/Modules/CanDoItAll.Modules.Workspace.Contracts/WorkspaceSettingsMutationContracts.cs` | `4462EF8B4309DF00796DE02DDD71BF838E140591D98DDC8677F9CFAB9086BA67` |
| `src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceDefaultsController.cs` | `7738ED8307C6ECF01451A9398398A565A1CCAB661E39C027A88EE530A30F7D13` |
| `src/UI/CanDoItAll.AgentFramework.Editor.UI/AgentEditorCoreSurface.razor` | `55B8D892E2EC9CAB1BD1411644162190BCE4D8DE21E5B692C15104C6B40F9527` |
| `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs` | `C34E3024F32DF3BCF31D1FBB2227C65E76D72781A5D42E89F1D72EBAC090B3BC` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs` | `0E1962D654CFDE94C2218711D9E6E132ABD390B7857E7BAF816FC94374C03F94` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Core/Agents/AgentDefinitionFactory.cs` | `6F7A6847D0DD012EC86EB61005D5E789CA5B6703A65B243E2CCA22E134FA47D2` |
| `src/UI/CanDoItAll.Processes.UI/ProcessDefinitionDraft.cs` | `E29BE36B9557455AC65CC3C0AB52586B26F175ED11BDB2C566E3AED7C1FF9D36` |
| `src/UI/CanDoItAll.Processes.UI/ProcessStepEditorState.cs` | `12072720BADF6CBF8B6A1D1441695D973679AB90B447108F12E7F32A226DDA16` |
| `src/Processes/CanDoItAll.Processes.Application/ProcessAuthoringWorkspace.cs` | `D5C9D93AAB072331109490600ED0F1233624552D30CB32F31A8C96C4957D52A0` |
| `src/Processes/CanDoItAll.Processes.Application/ProcessStepAuthoringAdapter.cs` | `9BFD5144AA0892381540E940D2722E6110D29A50BDB1B82DD11423DF25D21214` |
| `src/Processes/CanDoItAll.Processes.Persistence/EfProcessAuthoringStore.cs` | `D110382BFAD6D7B23961F4635D36C7479FD673AB92DE696755DC4A1F70769BAC` |
| `src/UI/CanDoItAll.AgentFramework.WorkflowAuthoring.UI/WorkflowCanvasSurface.razor.cs` | `B40BF8A5D083CA2003080372435DFDA103DF06E13C4917B785464060B3D30952` |
| `src/UI/CanDoItAll.AgentFramework.WorkflowAuthoring.UI/WorkflowDocumentAcceptance.cs` | `5240F92CB8F09E48211B8DC2F1815D77A486E466028730243238E06FACC7BCB9` |
| `src/UI/CanDoItAll.AgentFramework.WorkflowAuthoring.UI/WorkflowExecutionPolicyEditor.razor` | `A452A0EB84E066FB36C0DE0DB624B4E362037F50B97500E40C1546CF4349AA46` |
| `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Authoring/WorkflowDocumentOwner.cs` | `67306A7CE6A460FAE498754EF6F7E221E77E9F6259CD708B63649573043BF31D` |
| `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` | `A8DBF8E00788AC27EC79D58CEC3456AFEC818E81232BBBEFD7CC4D46B1AB771E` |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/Planning/ProjectTaskPlanPublisher.cs` | `59B1613121A02134D82469DF377AFA744EBAA0FA055E184AC2E80A5E9FBC74C9` |

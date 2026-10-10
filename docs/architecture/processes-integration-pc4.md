# Processes integration PC4

PC4 repairs integration around the existing Processes owners. It does not extract
another renderer or introduce a persistence schema. Focused native and browser proof
is recorded separately from the final broad gate. See the
[validation record](../validation/processes-integration-pc4.md) before relying on a
particular behavior as an executed acceptance result.

## Producer selection

`ProcessesApi.MapLaunchRequestAsync` first constructs the original producer input and
checks its retained intent with the caller identity. It captures current project
authority and executable content only for a new operation. The same canonical
`ProcessExecutableDefinitionClosure` then reaches Workbench variable preparation and
`ProcessLaunchRequest.ResolvedDefinitions`. The original producer fingerprint remains
distinct from the normalized key and the enriched variables.

`ProjectStructureProcessNodeService.BuildProjectScopedLaunchVariablesAsync` accepts
that captured closure. `WithDefinition` marks the driver selection resolved even when
its list is empty. A published empty list consequently cannot request the template's
drivers again. The variables-only overload remains available for existing callers.
The captured content is an internal native input, excluded from request JSON; public
HTTP callers cannot submit trusted executable content or authority.

Known definition/profile wire aliases accept trimming and case normalization. Stored
definition addresses, fingerprints, source IDs and arbitrary variable values remain
exact. Contradictory explicit definition key and ID are rejected. An unknown explicit
identity never selects a default. Omitted identity can select the configured profile
default; an authored selection without an explicit profile keeps its authored bindings.
The XML API descriptions describe these rules, including the deliberate rejection of
contradictory explicit identifiers.

Actual source consumers reviewed include HTTP check/launch, Workbench node start,
Workbench launch-dialog preparation, child preparation, and application compilation.
Direct native compatibility and replay remain separate test families. CodeAnalytics
was unavailable; this is source/caller, serializer, DI and evaluated-reference review,
not a semantic-index completeness claim.

## Retained predecessor recovery

`ProcessRetainedLaunchRecovery` owns historical evidence checks and readback. It uses
the immutable plan's canonical template component identity, never today's template to
explain an old plan. Reliable origin can come from a captured executable closure,
existing runtime project admission, or a matching **accepted** retained preparation.
The preparation must agree on run/root, plan ID/hash, review identity and authority.
Conflicting, missing referenced, unaccepted and malformed authority evidence refuses
recovery. A bare public project ID or caller-supplied launch variable is insufficient.

The four outcomes are intentionally distinct:

| Retained candidate | Behavior |
| --- | --- |
| Proven same database profile/project lifetime | Reuse its run and plan; render its saved review or immutable plan/assignments |
| Proven other origin | Do not return or rebind it |
| No matching candidate | Existing new-launch policy applies |
| Matching candidate with unproven/contradictory origin | Throw `ProcessExistingLaunchReconciliationRequiredException`; refuse automatic replacement |

Origin is checked before filtering failed, blocked or cancelled candidates. Workbench
subprocess reentry now performs retained lookup before new enrichment. Its stopped-child
path uses the same lookup with `StoppedOnly`; blocked/failed children retain their
existing explicit rework outcome. Proven cancelled children retain the prior replacement
policy. The subprocess coordinator reports the typed reconciliation refusal as needing
manager attention instead of converting it into a pending-child fallback.

A retained parent proves the lifetime for any new child capture. The child's optional
runtime admission field still follows its parent's field, preserving native lineage
invariants. Captured child content carries the checked lifetime when the predecessor
parent had no optional admission but other authoritative evidence exists.

The authentic [predecessor fixtures](../../tests/Support/CanDoItAll.Tests.Support/Fixtures/Processes/PrePc3/README.md)
were emitted by retained pre-PC3 binaries whose plan type has no
`ExecutableDefinitions` property. Current native mappers preserve their bytes and typed
plan hashes. Fixture setup can add an explicit historical admission and linkage to
isolated storage. The populated upgrade regression separately seeds those bytes before
the authoring migration, applies the canonical migration chain twice and compares every
plan/state/step/assignment row. It does not claim historical provider execution.

If origin was never recorded, PC4 does not backfill today's lifetime. The refusal names
the retained run. An authorized operator can inspect that original run and its effects
using existing run detail, cancellation and explicit rework controls. Rework acts on
the retained run under existing policy; it is not permission to assign it a successor
project's authority. No force-rebind or replacement override was added. Native full-caller
tests exercise repeated responses and exact child linkage; the browser opens the old run
across a new OS process and proves both reuse/refusal and same-ID project recreation.

The adjacent artifact bridge operates on accepted parent/child run IDs and retained
tree linkage; it neither creates a new run nor captures current project authority.
That linkage alone is not proof of a current project lifetime. Its existing accepted-tree
behavior was retained. Native published-child mapping tests cover the existing bridge;
the authentic predecessor tests separately cover lookup and full Workbench reentry.
No claim is made that the old fixture executed a historical external provider effect.

## Role resource ownership

The authored head owns materialized role resources by **usage key**. Deleting an
unreferenced role removes its usage, that owned resource and role placements/clones
which name the deleted usage. It preserves unrelated resources, source template files,
publication content and old operation receipts.

Add/import allocation checks both existing usages and retained resource keys. Imports
remap local role/step/artifact references and applicable placements together. A child
definition's output references retain their child's identity. New identities do not
silently inherit orphan defaults, and imports do not overwrite another role's material.
All changes still enter the existing revision/receipt transaction and replay contract.

## Rendering and native owners

The current source contains **nine** Razor renderers in `CanDoItAll.Processes.UI`,
excluding `_Imports.razor`. This observed count corrects the handoff's count of eight.

| Renderer | Active consumer / native responsibility |
| --- | --- |
| `ProcessWorkspaceSurface` | `ProcessWorkspaceShell`: reads, authoring commands, launch intent and reconciliation |
| `LiveProcessesSurface` | `LiveProcessesDashboard`: live reads, navigation and hidden-run state |
| `ProcessDefinitionRoleEditorPanel` | Workspace surface; native role adapter owns the commit |
| `ProcessDefinitionStepEditorPanel` | Workspace surface; native step adapter owns the commit |
| `ProcessDefinitionCanvasPanel` | Workspace surface; native canvas adapter owns semantic edits |
| `ProcessTemplateLibraryPanel` | Workspace surface; native import adapter owns allocation and receipts |
| `ProcessManagerChatSurface` | Workspace surface with native `ChatWorkspacePanel` binding; native chat/voice effects |
| `ProcessRunFilesSurface` | `ProcessRunFilesDialog`: authorized listing and file activation |
| `ProcessRunCancellationSurface` | `ProcessRunCancellationAction`: native runtime cancellation |

Routes remain `/processes`, `/projects/{ProjectId}/processes`, `/processes/live` and
`/projects/{ProjectId}/processes/live`. The native host still owns deferred dialogs,
file authorization, manager chat, activity and voice lifetimes. The sandbox consumes
the same renderers. Direct evaluated project references and the passing recursive
assembly/public-signature boundary tests find no forbidden native owner in the renderer
or sandbox closure. `TooltipService` is its only injected UI service.

The **Processes structural UI decoupling dimension is closed** for these reviewed
rendering paths. This is not an exhaustive semantic census of every repository feature,
nor an assertion that all other validation dimensions follow from that cut. No asset
or renderer production source changed in PC4. Existing Fast/Parity asset evidence is
retained on that unchanged graph; fresh native workflow/agent/file/voice and authoring
browser journeys separately exercise the actual consumers.

## Residuals and dependency tuple

Authoring-aware transfer/export remains safely refused where head/publication/receipt
facts would be lost. A portable history/remapping contract and large-catalog database
paging are separate product work. PC4 changes neither guard nor paging behavior.

The local FileTools commit `3a080ecd31068a77c1e1bd639f7a78e21c93db85` and CI pin
`498b36825bd5a5222429972af120b04becf4b3f6` have identical tree
`6bc360281b6ad13ddec5e813f0a00e26b5bc7d6d`. No repin is warranted by that comparison.
The Web entry point and its full dependency graph passed a fresh `ProcIntegrationPC4`
build at that tuple. Focused tests use refreshed `ProcAuthoringPC3` assemblies; actual
predecessor OS restart hosts use the clean `ProcIntegrationPC4` Web output. Release
product/Stable builds and broad execution have their own recorded outcome.
Shared bundle status at the product proof checkpoint was `NO_CHANGE`. The subsequent
operator-directed storage-policy repair is `MINIMAL_REPO_CHANGE`: the existing shared
bundle now requires standard Debug/Release output reuse, serialized builds, useful UI
dotnetwatch iteration and reusable isolated fixtures with last-use cleanup. This changes
execution guidance and its integrity manifest, not product ownership or the tested C# source.

Architecture review retains the existing owners and small internal recovery seam. Final
validation status is recorded alongside this document. No new
partial-class boundary, project reference, DI locator, runtime framework or schema was
introduced. The only added persistence friend access is for the test-support assembly
to ingest authentic predecessor bytes through the existing native mappers.

## C# architecture gate result

Status: **Pass** for the corrected ownership and dependency boundaries at
`ca195c794349d4e4eb653bacb7605ed83df86c4b`; broad product validation remains a separate gate.

| Check | Evidence and conclusion |
| --- | --- |
| Responsibility | Internal retained-launch recovery owns evidence validation/readback; Application and Workbench call it instead of reconstructing origin independently. Role changes remain in the existing authoring transaction. |
| Dependency direction | No production project/reference or DI registration changed. Contracts remain independent of native owners; evaluated renderer/sandbox and recursive assembly tests pass. |
| Partial-class policy | No new partial file or class was added. Existing application/workbench owners received narrow corrections; no forwarding manager or service locator was introduced. |
| Construction | Captured executable content is selected at the authorized native boundary and passed explicitly. No singleton authority or hidden UI persistence was added. |
| Testability | Unit failures before fixes, real PostgreSQL receipt/authority tests, HTTP producer probes and independent browser/OS restart checks assert exact identities and negative cases. |
| Extension seam | All reviewed HTTP, Workbench, child and stopped-child consumers use the same captured selection or retained-origin owner. Public JSON cannot inject that trusted capture. |

The compatible handoff uses staged documents, 36 acceptance groups and 123 inherited rows
rather than the canonical subbundle directory layout. Closure therefore uses the bundle
validator's manual semantic compatibility gate plus the unchanged package verifier;
it does not invent canonical folders or rewrite the historical package. Final closure
must agree with the source-addressed validation record and the frozen campaign outcome.

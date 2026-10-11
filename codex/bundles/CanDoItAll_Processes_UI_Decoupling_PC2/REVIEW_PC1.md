# Review of the pushed PC1 implementation

## Verdict and scope of this review

The implementation at `146067ed133f624878dfe5756c441a43c0f21b4a` is a substantial, correctly directed renderer extraction, not a superficial project split. Keep its architecture. It is **not** sufficient evidence to declare every native Processes feature complete.

The branch is two commits ahead of the previous review baseline `8549e6a18a22638d595bc76ba4240a61ba70351d`. The reviewed product commit is titled `processes phase1`; GitHub reports its signature valid. That is a positive existing signature observation, not independent local verification by this reviewer and not evidence that future checkpoints will be signed. PC2 introduces the operator's requested incremental signing workflow.

This review inspected the comparison, maintained boundary/testing records, selected native and rendering source, tests and historical source counterparts. Full versus range-limited source reads are recorded in SOURCES.json and BASELINE_SOURCES.json. It did not clone/build the product, resolve its current dependency closure, execute C# tests, run PostgreSQL/Playwright, or measure performance. The local review environment did not provide the required .NET checkout/toolchain. CodeAnalytics was not invoked; plugin discovery did not expose the named integration. The executor must use its own configured MCP and actual checkout.

## Preserve these completed changes

| Completed work | Source-level assessment |
| --- | --- |
| Eight renderer families and an independent sandbox | The maintained boundary record covers workspace, live dashboard, canvas, roles, steps, templates, files and cancellation. Processes.UI references reusable rendering packages, Projections and AgentFramework.UI rather than native Processes/EF/runtime implementations. [R01, R17, R24] |
| Explicit native host seam | The workspace host implements `IProcessWorkspaceSession`; the rendering surface consumes it. Native state, routing, admission and execution remain outside the reusable renderer. A long native host is not itself evidence of failed extraction. [R02, R11, R18] |
| Stable definition drafts | `ProcessDefinitionDraft.Observe` retains dirty inputs and detects version conflict. `Accept` reconciles current fields against the submitted values, preserving subsequent edits. Existing host tests exercise tab unmount and edits during a pending save. [R05, R12] |
| Raw role/step input and row identity | Role GUID/allocation and step numeric buffers survive real renderer tab unmount. Step row updates use keys and submissions copy collections. These are meaningful improvements; retain them while fixing the gaps below. [R03, R04, R13] |
| Run-file lifetime | Open, activation and host-action completion paths fence error as well as success. Reset detaches resources before awaited cleanup; Close checks the opening before publishing. [R16] |
| Reused chat seam | Native `ChatWorkspacePanel` can publish typed `ChatWorkspaceBinding` while retaining the previous rendering path; activity can be supplied by a native headless reader. Protect both consumers, not just the Processes one. [R19] |
| Nontrivial native browser coverage | `ProcessNativeBrowserTests` uses the actual Web composition and native owners for accepted launch, workflow completion, read-back, files, manager chat, attachment submission and cancellation, with a controlled loopback inference fixture. The fixture uses the actual WebApplicationFactory, isolated PostgreSQL, native command forwarding and an injected read-fault wrapper. This is stronger than a UI-only fake. It does not test durable authoring saves. [R15, R26] |
| Real closure and renderer tests | The light suite walks loaded assembly references, checks forbidden transitive/unresolved dependencies and public signatures, and renders actual children/canvas/files. Reading these tests does not mean this reviewer executed them. [R13, R14] |

Do not replace this work with another DTO layer, forwarding service, assembly quota, or global state framework. Preserve the current in-process projection adapter; the presence of a separate API control plane is not an instruction to migrate this renderer seam to HTTP. [R06, R22, R23]

## Findings, prioritized

### F1 — High: read generation still retires an in-flight authoring result

**Classification:** source-confirmed remaining defect; inherited mechanism, not newly introduced by PC1. Runtime reproduction is required on the execution checkout.

`ProcessWorkspaceShell.LoadAsync` increments `workspaceGeneration` for ordinary loads. `TryBeginDefinitionEditorOperation` increments the same field and captures a definition key/version. All five editor command families await a native result, then discard it when `IsCurrentDefinitionEditorOperation` fails. A refresh or detail-tab load for the same opening changes that generation, so an accepted command can be discarded along with its receipt and accepted revision. The toolbar and tabs do allow these reads. The same mechanism can let another command replace the active submission/ownership. [R02: LoadAsync, ExecuteDefinition* / ExecuteTemplateImport*, TryBeginDefinitionEditorOperation; R11: toolbar/tabs; B01]

The existing new tests prove later typing during Save and live-operator receipt retention during refresh. They do not exercise an authoring Save with an interleaved read. [R12]

**Fix:** separate read sequencing from mutation ownership. A same-opening read cannot retire the mutation, lose a known accepted receipt, or release the write slot. A genuinely different opening/profile/project lifetime/definition must retire presentation correctly. Capture one immutable submission per admitted command; retain acknowledged outcomes before dependent observation. No blind mutation replay or stale-read rollback.

**Failing-first sequence:** hold a real host's Save callback; issue same-definition Refresh or a tab load; let that read finish; return Accepted for the original Save. Assert one native command, accepted version/receipt accounted for, newer typing preserved, and safe read-only reconciliation. Repeat read completion after acceptance, known rejection, unknown outcome and actual target retirement.

### F2 — High: role Add/Delete no longer adopts the owner's selection

**Classification:** source-confirmed PC1 regression relative to the old role component; runtime reproduction is required.

The native role owner returns the newly created role as `SelectedRoleKey` for Add, and a remaining role (or null) for Delete. `ProcessRoleEditorState.Accept` sets the returned version and calls `SyncSelectedRole` using the **previous local key**, never adopting that result selection. `Observe` then sees the version already synchronized and returns; after Delete, the missing old role causes `SyncSelectedRole` to return without clearing old draft fields. The old component assigned the returned selected key in `OnParametersSet`. [R03; R09: ExecuteAddRole/ExecuteDeleteRole; B02]

This can retain the old editor after Add, or retain stale selection/draft state after Delete. Do not fix it by always overwriting selection: the operator may have selected another role while the command was pending.

**Fix:** reconcile selected identity and raw draft as part of the matching command outcome. Adopt the returned Add/Delete target only when the origin selection is still eligible; clear a deleted draft/submission/errors and handle an empty collection explicitly. If the user moved on, preserve the successor and reconcile catalog data separately. Preserve safe template/workflow selection and version semantics.

**Tests:** Add with the old role dialog open; Delete selected role with survivors; Delete last role; return Add/Delete after user selection changes; repeat through the actual renderer with the native owner's returned projection. Verify both selected key and displayed fields, not a success message alone.

### F3 — High: a selected step can round-trip another step's decision role

**Classification:** source-confirmed inherited defect, copied during extraction; not a new PC1 regression.

`SelectStep` and `SyncSelectedStep` use the locally selected step. `CreateDraft` builds its key from that local selection but takes `DecisionRoleKey` from `StepEditor.SelectedStep?.Basic.DecisionRoleKey`, the original projection selection. With two different steps this can copy or erase another step's decision-role binding. The exact expression exists in the pre-PC1 component. [R04: SelectStep/SyncSelectedStep/CreateDraft; B03]

**Fix:** keep this semantic field with the actual selected step draft and immutable submission, including authoritative acceptance/reload. Audit the rest of the same draft mapping for similar hidden fields. Do not convert null to an arbitrary role, clear metadata to pass a test, or mutate the other step.

**Tests:** two steps with distinct non-null roles, then a null/non-null pair; select the second, edit only its title and Save. Assert exact submitted key and DecisionRoleKey, native owner round-trip where supported, and no change to the first step. Repeat after row reorder and a same-step refresh.

### F4 — High native-feature gap: accepted authoring is not durable read-back

**Classification:** preexisting native ownership issue, explicitly disclosed by the PC1 boundary record. Not a failed renderer move and not evidence that PC1 deleted a previously durable store.

`ProcessWorkspaceProjectionClient` creates a new service scope for reads and definition/role/step/template commands. The shown definition and role services store accepted snapshots in **instance dictionaries**; DI registers these owners Scoped. A command can return Accepted while the next client call gets a fresh owner/template baseline. The definition service blob is identical before and after PC1, as are the client and DI files. [R06–R09; B04; original PC1 source register]

`ProcessDefinitionCatalogProjectionService` also reads template-pack definitions and reports `DraftDefinitionCount: 0`; its catalog is not automatically updated by another service's dictionary. `ProcessLaunchApplicationService` has a template-backed selection/preparation path in the inspected entry code; the complete launch-resolution/partial call graph still needs mapping. A quick singleton dictionary patch does not establish persistence, catalog correctness, profile/lifetime isolation, or executable published-definition semantics. [R10, R25]

**Required PC2 action:** follow AUTHORING_OWNER_PREREQUISITE.md. Produce an actual cross-scope reproducer, current owner/consumer map, and an implementation-ready bounded native-authoring plan. A bounded adapter repair to an already-established authoritative owner may be made if discovered. A new schema and definition/publish/launch authority redesign are a separately scoped implementation, not an undocumented side effect of UI decoupling. Keep the native durability dimension visibly blocked/unresolved until proved.

### F5 — Medium: final pushed-candidate execution cannot be independently reconstructed from the tracked record

**Classification:** evidence limitation, not proof that Codex did not run tests.

The tracked boundary document lists meaningful focused selections, measurements and a raw ledger under ignored `.artifacts/pc1-20261008`, but leaves some final/frozen results there. This review did not retrieve that local ledger. At the reviewed SHA the available commit-status list was empty, check-runs returned zero, and Actions runs filtered by HEAD returned zero. These observations provide no CI-green attestation; they also do not disprove local tests. [R01; remote observations in SOURCES.json]

**Fix:** commit a sanitized exact-source summary with current filters/discovery/execution counts, artifact metadata where available, signed checkpoints, and separate readiness dimensions. Re-run the selections invalidated by PC2. Do not republish historical counts as current proof or keep all meaningful closure information only in ignored output.

## Hypotheses to test before implementing extra changes

| ID | Source observation | Required controlled test |
| --- | --- | --- |
| H1 | Step `MergeRows` keeps a whole local row when any field differs from submission. Raw numeric buffers are restored independently. [R04] | Owner normalizes one field of an existing row while the user edits a different field in that row. Both must survive, with raw invalid text and stable keys. Test actual owner normalization when available; do not invent a defect from irrelevant fields. |
| H2 | Role/step `Discard` does not itself clear a pending `submission`; selection transitions do. [R03, R04] | Hold command completion, invoke only an actually reachable explicit discard/rejection/retirement path, then complete. An old submission must not be reused by a later operation or corrupt a successor. Treat inaccessible sequences as non-applicable with evidence. |
| H3 | Workspace reads capture database generation at entry and guard completion by a workspace counter; interaction with external profile/project-lifetime changes needs proof. [R02] | Hold a read/write while authority changes, including A→B→A with the same public IDs. No old data, receipt, chat/voice state or busy completion may be applied to the successor. Native admission remains mandatory. |

These are not claims of executed failures. Record which reproduce, which are already covered, and which are refuted. Avoid speculative fixes outside the touched boundary.

## Why PC2, not another module

The reviewed records and source support preserving the completed Processes extraction. The next useful work is correctness and trustworthy closure, followed by a semantic residual census. Re-extracting Workbench/Agents or splitting the host by line count would not address the defects above. A newly identified active heavy renderer must be supported by an actual call/dependency path before it becomes another assignment.

The native authoring gap deserves its own deliberate owner design rather than a rushed database rewrite disguised as decoupling. Finishing PC2 can close the structural wave while leaving that separately named functional prerequisite explicit; it cannot certify overall Processes durability or release readiness.

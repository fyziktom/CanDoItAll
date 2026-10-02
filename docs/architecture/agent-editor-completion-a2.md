# Agent editor completion A2

## Scope and source

This record follows the complete sealed `CanDoItAll_Agent_Editor_Completion_A2` package.
Historical bundles, including A1, remain unchanged. Entry application HEAD was
`bf6d15d3b6323a1fb187874afdeb78eb3a55170e` on `components-decoupling`, with a clean tree
`47546b40e8c57b23dd33c2b80ac9c825b27df1fb`. Components remains
`4a858412d2c2a3f6123bf23d8c4584f05b47627d`; FileTools remains
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. Both siblings are unchanged. SDK 10.0.303
and the isolated `AgentEditorA2Proof` configuration use those source dependencies.

Implementation, native/application, desktop, static and development-loop checks are complete.
The single broad Stable checkpoint finished with one stale boundary assertion; its complete
30-case repaired family passes. Original mixed results and targeted follow-ups remain distinct.
This is not an application release or permission to start another module.
No paid model requests, publication, push, merge or PR are authorized by this work.

## S0 causal repair

The inherited Verify handler captured the unsaved draft as though it had been submitted
by Save. Its later-edits comparison returned false when the user stopped typing after
Verify, so read-back replaced the form with persisted configuration. Failing-first tests
demonstrated lost Unicode name/instructions, permissions and EditContext.

`IAgentFrameworkWorkspaceService.VerifyCapabilityAsync` now returns the native
`CapabilityVerificationOutcome` already produced by the proof publisher. Successful
facades previously discarded its receipt. HTTP and curator callers still await the same
operation and keep their existing response shape. C# consumers must rebuild against the
typed result; the full application and test graph has been rebuilt. `AgentEditorVerification` separates
proof read-back from whole-agent Save. It checks agent/capability identity, the editor's
original expected version and the exact native receipt classification before advancing
only that editor's concurrency baseline. It never replaces the draft or EditContext.
Competing configuration stays a conflict. Missing/unconfirmed evidence blocks further
writes; recovery reads do not diagnose again. Cancellation, retirement and A→B→A remain
bound to the original editor.

The extended native round trip also reproduced a template read-back defect: Save committed,
but reconciliation queried a catalog excluding templates. Reconciliation now includes the
saved template, while ordinary tag suggestions still exclude templates. Both template and
ordinary-agent round trips pass. No persistence schema, authority or replay protocol changed.

The final file journey found a second inherited consumer defect after the editor saved Memory's
explicit-directive mode: an approval continuation has no user text, and the Memory planner rejected
its empty query before checking that no directive requested Memory. Four new continuation cases
failed before repair; three empty-query rejection controls already passed. The existing explicit
mode skip now precedes query validation. All 24 invocation/fan-out/result cases pass, including
continued rejection of an actual `/mem:primary` request without a query. No provider is dispatched
for the approval continuation, no grant changes and no runtime schema changes.

## Renderer and owner census

All paths below are relative to `src`. There is one canonical complete draft, session and
EditContext; no per-tab writer or second writable form model.

| Rendering | Actual location | Retained production owner / scenario |
|---|---|---|
| Shell, ten tabs, Identity, Runtime, Images, Voice, footer | `UI/CanDoItAll.AgentFramework.Editor.UI/AgentEditorCoreSurface.razor` | `Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor` composes the session and commands; same renderer in Editor.UiSandbox |
| Project Structure Access, Workspace Tools, Secrets, Process Access, Capabilities | `UI/CanDoItAll.AgentFramework.Editor.UI/AgentEditor*Section.razor`, composed by `AgentEditorAccessSurface.razor` | Host projects safe reference metadata and captures typed intents; `AgentEditorAccessState` shares draft-only permission policy with the sandbox |
| Memory controls and binding rows | `UI/CanDoItAll.AgentFramework.Editor.UI/AgentMemorySection.razor`, `AgentMemoryBindingList.razor` | Existing module `AgentMemorySettingsPanel` reads profiles, checks enabled/healthy/supported/unique-driver eligibility; persistent candidate state survives tab removal |
| External-root entry and selected references | `UI/CanDoItAll.AgentFramework.Editor.UI/AgentExternalRootsField.razor` | Existing module `ExternalWorkspaceRootSelectionField` normalizes native paths, resolves host bindings and exports protected tokens; only safe display metadata reaches the renderer |
| Capability assignment/proof cards | Existing `UI/CanDoItAll.AgentFramework.UI/Capabilities/AgentCapabilityList.razor` | Actual renderer composed in both hosts; native assignment/diagnostic owners unchanged |
| Storage chooser | Existing `UI/CanDoItAll.Workspace.StorageSelection.UI/StorageCatalogSelectionField.razor` and dialog | Actual child in both hosts; Apply edits draft IDs, parent Save persists grants |
| Delete, auto-approval, scripts/environment confirmations | `UI/CanDoItAll.AgentFramework.Editor.UI/Agent*Confirmation.razor` | Compatibility dialog wrappers retain DialogService/Reference and return only to the original editor/revision |
| Selected-reference table and resource picker | `UI/CanDoItAll.AppComponents.RecordBrowsing/SelectedReferenceTable.razor`, `ResourceCardPicker.razor` | Same controls and CSS relocated from broad AppComponents; old public assembly types are forwarded |
| Avatar generation, provider synchronization, capability-definition wizard | Existing module/MAF integrations | Explicit retained integrations; no authoring extraction or external call in the sandbox |

The sandbox uses actual child renderers, BaseLib dialogs and a deterministic Storage metadata
adapter. Its committed fixture snapshots are independent of the live draft. Root resolution,
definition creation and diagnostics are explicitly simulated. It includes missing references,
unavailable Memory, independent metadata failures/retry, manual aliases/order, two editors,
held saves/diagnostics, known commit with failed read-back, unknown publication and large catalogs.

## State and authority

Project permissions remain separate. Select all merges observed IDs without granting all future
projects or discarding missing selections. Removing an explicit project also removes its acquired
lifetime binding; native owners decide any subsequent lifetime acquisition. Secret references
preserve purposes and use only picker metadata, with a rejecting-vault test proving no value read.
Process definition IDs remain visible while their existing picker is unavailable.

Workspace profiles retain explicit roots, protected bindings and Storage IDs. Confirmation
acceptance is fenced by editor origin and permission revision. Root/Memory entry text updates
immediately but belongs to no parent HTML form; Enter does not silently Save or add a permission.
The explicit Add action commits a candidate to the draft.

Saved-agent capability changes still save the complete current draft and say so in the UI.
New-agent assignments remain staged. A wizard-created capability ID is retained before catalog
read-back; catalog retry and assignment retry cannot repeat creation. Admission, optimistic
concurrency, managed deletion and unresolved-write guards stay with native owners.

Native tests cover core-only then A2-only edits, templates, unknown JSON extensions, Favorite,
history/temperature/background flags, project lifetimes, host bindings, Memory metadata and an
untouched second agent. UI-saved Memory bindings dispatch through the real policy/handler to the
shipped deterministic mock provider; disabling it refuses dispatch while preserving the settings.

## Dependency review

Evaluated MSBuild snapshots cover 45 UI/sandbox/application roots and 185 projects before and
after. No protected neighboring root newly acquires Editor.UI, and Projects source hashes agree.
Editor.UI's closure changes from 11 to 12 projects by adding neutral RecordBrowsing. The complete
editor sandbox grows from 12 to 18 by composing the actual capability list and Storage chooser,
including their existing Usage/Charts dependencies. It remains free of Core, EF, Canvas,
Memory.Application, drivers, provider executors and product modules.

StorageSelection.UI narrows from 16 to five projects; its sandbox narrows from 17 to six.
The controls keep their namespaces and forwarded public types. Model/protocol assemblies remain
in place. Pure snapshot copying moved to Editor.UI for use by both presentation hosts; the module
retains the compatible draft-policy entry points and native capture/normalization policy.

## Validation and attempts

Raw local evidence is under `artifacts/agent-editor-a2/20261001-bf6d15d/`; it is ignored and
separate from maintained documentation. Exact selections, current discovery, TRX and elapsed
times are preserved per attempt. Counts below do not add overlapping retries as unique coverage.

| Checkpoint | Result |
|---|---|
| S0 before repair | Clean/dirty tests failed on replaced context; dirty field assertion independently showed the lost pre-Verify Unicode name |
| S0 repaired before extraction | 11/11 component and 17/17 native publication/API/race cases passed; earlier fixture compilation/dispatcher failures remain retained |
| Initial extracted component checkpoint | 184/192 passed; six root selector failures, one obsolete input event and one logger text assertion were repaired |
| Access/native extension | 19/21 passed, exposing the template read-back defect and an assertion that incorrectly included an intentionally removed secret permission; subsequent two native round trips passed |
| Complete editor/related-control selection | 248/248 passed on the repaired source, no skips; includes actual template Verify, metadata-only secrets and read/confirmation lifetimes |
| Independent full editor suite | 25/25 passed, no skips |
| Instrumented native secret selection/save | 1/1 passed, no value resolution |
| Source and independently published desktop | 2/2 passed after a sandbox auto-approval Cancel display defect was repaired; the failed first attempt is retained |
| First final application checkpoint | 15/16 passed; the positive file journey exposed the Memory continuation defect after a successful ten-section editor round trip. Its failed run and tool/approval receipts remain retained |
| Memory continuation repair | Failing-first 4 failures / 3 passing negative controls; repaired invocation, fan-out and result families 24/24 passed |
| Repaired application checkpoint | 16/16 passed, no skips: real files/approval/read-backs/download/deletion, all five hostile proposals, timeout retention, Workflow/TestLab and neighboring hosts |
| Memory context consumers | 37/37 contributor/message-transformation/tool-attachment cases passed on repaired source |
| Final screenshot follow-up | 3/3 passed: positive native file/history-deletion journey and both source/published desktop cases. The earlier Delete image was captured before its overlay appeared; an explicit visibility wait fixes capture |
| Broad Stable checkpoint | 15,968 listed entries expand to 16,023 executed data rows: 16,022 passed, one failed, zero skips across 22 assemblies in 4 h 57 m 22 s. The sole failure is the obsolete StorageSelection graph expectation; its complete repaired 30-case family passes |
| StorageSelection boundary repair | 30/30 passed; exact restored dependency set and runtime allow-list now enforce the measured five-dependency closure, with forbidden, unresolved and cyclic negative controls |
| Portability checkpoint | Final complete scan includes untracked source: 7,945 files and 33,534 findings. All 28 added and 27 stale findings reviewed; final no-write enforcement passed with 15,246 unchanged allowances |
| Reversible watch probes | Web and full sandbox passed all nine probes, restored source hashes and reported no page errors |

The broad Stable decision is to run one frozen checkpoint because the public native verification
result and shared control assembly delivery changed. Builds and all caller/serialization/adapter
owners must agree; isolated browser or leaf tests alone do not close those changes. No repeated
unchanged broad sweep is planned. The root solution initially rejected the isolated configuration
before compiling any project. Local ignored solution mirrors add only that build type and resolve
the original paths: application membership remains 171 projects and Stable remains 22. The original
solutions are unchanged. The broad application and Stable builds use the same configuration and source roots.
The successful broad test build reports 11 existing warnings in unchanged test files.

All 25 native-owner selections match their discovered and executed counts: editor/contracts
27, verification 16, project lifetimes 27, roots/storage 64, secrets 5, CRM 31, the prior Files
operation fence 18 and managed deletion 28. These 216 cases are part of Stable, not additional
unique coverage. Integration passed 3,273 cases; the main unit assembly passed 9,421.
Seven complex-argument theories expand seven listed entries into 62 rows, accounting for the
55-row difference. Their unchanged source factories and all remaining test identities match;
five Unicode display-name differences are explicitly reconciled. There are no missing or
unexpected executions. `stable-discovery-reconciliation.json` retains this accounting without
changing the original discovery receipt. `stable-follow-up-review.json` resolves the sole failed
case through its passing rebuilt family; it does not turn the original run into an all-green run.

The frozen source/test/dependency manifest is `stable-before-source.json`, with aggregate hash
`26e774273653d1bafe6df722685b0843e3e1bde69b9aa544ebc6fe92517d6be0` across 7,843 source/configuration
files in the application and both dependency repositories. Actual test and Web binary hashes are
retained with the final attempts. Early staged attempts retain their own discovery, logs and TRX;
they are not retroactively described as executions of this final checkpoint. The early 25-case
leaf and two-case desktop attempts have no separate frozen whole-source receipt; their current
coverage comes from the frozen Stable and final capture runs, respectively. The initial 24-case
Memory repair has build/TRX lineage and the recorded repair delta; subsequent native/context
checkpoints additionally record pre-execution binary hashes. Work was performed in the existing
checkout without intermediate commits.

The bounded Memory follow-up uses `AgentEditorA2Repair` outputs so the running broad checkpoint's
binaries are never overwritten. Only `AgentMemoryInvocationPlanner` and its regression tests
change executable inputs after that checkpoint; the baseline refresh is validation metadata.
Current source manifest `repair-before-source.json` hashes to
`eb2f89be12c4aef8c261d8c0ed73517d1ab269dd9ff92b2eae1922966d030d15`.
The affected Memory families, complete owning editor selection and native journeys are the
follow-up validation. Unchanged scopes retain the original broad result; it will not be relabeled
as a run of the repaired planner or repeated solely for this bounded branch-order correction.

Desktop source manifest `repair-capture-final-source.json` hashes to
`d1d6b63d5eeafd01d2b496a2c113d5c396df8c2132607055bab8ad62ec2a7b66`.
After the 248/16-case runs, `git diff --check` identified one extra EOF blank line in the Razor
shell, and visual review identified the premature Delete screenshot. `format-invalidation.json`
reconstructs the preceding source hash using only EOF/line endings; `capture-invalidation.json`
records the visibility-wait-only helper change. Web rebuilt without warnings, and the three affected
desktop/native cases passed on these final bytes. No earlier mixed attempt is relabeled all-green.

The frozen Stable run exposed a stale StorageSelection boundary assertion: the sandbox assets
contain five dependencies after moving the neutral controls, while the test still expected sixteen.
The exact allowed set now contains only those five dependencies plus the sandbox itself; runtime
traversal explicitly rejects the removed AppComponents edge and retains indirect unresolved/cyclic
negative controls through RecordBrowsing. The complete 30-case family passes after a fresh build.
This test-only follow-up leaves all product binaries unchanged. `storage-boundary-invalidation.json`
records the single-file delta; current manifest `repair-storage-final-source.json` hashes to
`80fe781388d41c0b9ab2398b97a20b40a7c543248936c4b4fd3c0516b62cb16c`.
The original mixed Stable attempt is retained separately from its repaired follow-up.

## Desktop and development loop

Primary target is 1920×1080 at 100% zoom. The existing wide dialog keeps its compact tabs,
section body scroll owner and stable footer. Small confirmations remain compact. Secondary
counts are compact stats; project/secret lists retain internal scrolling; capability filters
work with the nontrivial catalog. Inspected screenshots cover Identity, all six A2 sections,
100-project and 90-capability catalogs, two editors in adjacent desktop columns, Storage selection,
and all three small confirmations. Labels, fields, acknowledgement and actions remain readable;
the Save footer stays visible and the narrower editor columns retain their own tab scrolling.
The source and independently published sandbox checks also verify fonts, served styles,
one form, no horizontal document overflow and console/circuit failures.
The final production Delete overlay was separately inspected: its original agent identity, full
consequence text, Cancel and Delete controls fit in a compact centered dialog above the retained editor.

Baseline Web hot-reload measurements, three restored samples each: Razor 3.195/2.936/2.678 s;
C# 1.351/1.578/1.583 s; CSS 6.900/15.154/4.812 s. Initial hydration was 2.856 s. Final Web:
Razor 3.732/2.926/2.916 s, C# 1.325/1.884/1.595 s, CSS 6.923/10.962/4.829 s; hydration 2.821 s.
Full sandbox: Razor 2.680/1.857/1.579 s, C# 0.265/0.265/0.265 s, CSS 1.001/2.264/0.717 s;
hydration 0.355 s. C# visibility uses an ordinary input rerender; Razor/CSS update automatically.
All edited bytes were restored and neither host reported page errors. Web watch lists grow from
4,617 to 4,635 files; sandbox lists grow from 534 to 643 as the real children join its graph.
These are measurements of this source graph and host, not a universal whole-Web speedup.

## C# Architecture Gate Result

Status: Pass for the implemented boundary and its completed native/consumer validation.

Presentation moved to the existing cohesive leaf; safe contracts and typed intents cross the
boundary. The host still owns database reads, native root resolution, driver eligibility and
mutations. The real child controls are composed in production and in the sandbox. Neutral controls
were relocated with type forwarders, reducing their other consumer's closure instead of importing
a product module. Existing models, authority protocols and native owners remain in place.

There is no new project, service locator, reflective dispatch, runtime-to-UI edge or partial-class
boundary. Pure behavior has isolated positive and negative renderer tests; real authority and
receipt behavior use native owners. Evaluated before/after graphs and public-signature guards
support the dependency claims. CodeAnalytics was unavailable, as recorded at entry.

## Review, limits and next work

The architecture uses one cohesive presentation boundary, actual controls and native effect
owners. No service locator, runtime-to-UI edge, new durable state or new partial-class boundary
is introduced. Code Analytics/Components/dotnetwatch MCPs were unavailable; exact source,
evaluated MSBuild, CLI tests and browser proof supply the prescribed fallback.

Provider administration remains the next separately scoped AgentFramework family (4/5
integration complexity), followed by capability-definition/team authoring (3–4/5) and a residual
chat/usage audit. Workflow authoring, Workbench and Processes remain later work. Completed
Projects/Workspace/Resources boundaries are consumers of this change, not replacement projects.
The exhausted 40/40 live journal is preserved; scripted-external/native-owner proof does not
claim paid live-model validation. No new paid requests were made.

## Closure and delivery

Final source verification retains hash
`80fe781388d41c0b9ab2398b97a20b40a7c543248936c4b4fd3c0516b62cb16c` across 7,843 files.
All 22 original Stable test assemblies, 722 final Web/browser files and two repaired storage
test binaries still match their recorded hashes; the independent publish comparison matches
all 38 sandbox binaries. All 50 sealed package files and both historical budget artifacts remain
unchanged. Changes remain uncommitted in the existing checkout; no publication or remote action
was performed.

The task-owned PostgreSQL container, relay, network and volume were removed after exact identity
and ownership checks. No task host remains. The two ordinary application processes remain running
with their original start times, and ordinary port 5032 was neither used nor stopped.

The maintained documentation gate, final whitespace check and explicit safe-export scan are
recorded in `closure-review.json`. Shareable summaries, hashes and four visually inspected desktop
images are limited to `artifacts/agent-editor-a2/20261001-bf6d15d/safe/`; raw logs, TRX, private
fixture state and earlier failures remain outside that whitelist. `raw-input-closure.json` maps
the seven operator instructions and sixteen required documents to all 28 outcome groups.
`agent_editor_a2_complete` closes this editor scope only; `application_release_ready` remains false.

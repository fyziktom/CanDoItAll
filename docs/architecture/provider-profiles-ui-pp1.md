# Provider Profiles Core PP1

## Scope and entry

PP1 moves the entire provider catalog and local editor into
`src/UI/CanDoItAll.AgentFramework.Providers.UI`. Connection, Prices, Runtime and Thinking
use the actual renderers in both production and the independent
`src/Sandboxes/CanDoItAll.AgentFramework.Providers.UiSandbox` host. The six tab identities
remain stable. Sharing, request History, source connections and source refresh still run
through their native module owners.

Entry was `46d745ad1a9803f3945523f656b4d1dd26044b85` on `components-decoupling`, clean tree
`4b60efb5a614a8c16ef50ae2f9364d22b908e2ca`. Product inputs did not differ from the package's
reviewed `25ea60327ab572daee2728d6e86589f035942d94`. No A2 repair was needed. The completed
ten-section editor, native verification receipt handling, Memory continuation correction
and all historical bundles remain intact. A2's historical Stable checkpoint remains
16,022/16,023 with a separate passing 30-case StorageSelection repair; this run does not
relabel that result.

SDK 10.0.303 and isolated configuration `ProviderProfilesPP1Proof` use source dependencies:
Components `4a858412d2c2a3f6123bf23d8c4584f05b47627d` and FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. Neither sibling is edited. The Components,
CodeAnalytics and dotnetwatch MCPs were unavailable; source/caller inspection, evaluated
MSBuild, CLI discovery and owned processes provide the evidence instead.

## Renderer and owner census

| Surface | Renderer in Providers.UI | Retained owner / effect |
|---|---|---|
| Catalog, search/count/tree, loading/error/retry, new/select, six-tab shell | `ProviderProfilesSurface`, `ProviderProfileTreeNodeBuilder`, `ProviderProfilePresentation` | `AgentProviderProfilesPanel` owns `ProviderProfilesSession` and `ProviderEditorOperations` |
| Connection: identity, kind/purpose, endpoint/model, safe secret reference, tags | `ProviderProfilesSurface` | Original `ProviderProfileEditorModel` and `EditContext`; metadata-only secret choices projected at the host |
| Runtime: transport, model text, flags, raw JSON, notes | `ProviderProfilesSurface`, `ProviderEditorDraft` | Same whole draft; native administration still determines stored/derived values |
| Prices: full editable rate table, missing models, private policy, add/remove/reset, discovery intent | `ProviderModelPricingEditor`, `ProviderPriceInput` | Existing `ProviderPricingDefaults`; captured native discovery remains in operations/commands |
| Thinking: search/paging/table, automatic/configured controls, local modal, Apply/Cancel | `ProviderThinkingSurface` | Existing `AgentThinkingEffortPolicy` and `ProviderModelThinkingConfiguration`; Apply changes the draft only |
| Form validation and local action footer | `ProviderProfileEditorForm` | Save/Delete/Health/reconcile/verify/retry stay in original native operations |
| Sharing/publication | Typed `SharingSlot` | Actual `SharedProviderManagementPanel`, provider identity and delivery reconciliation |
| Request History | Typed `HistorySlot` | Actual `ProviderRequestHistoryPanel` with `SingleProvider` scope; no eager query |
| Shared connections | Typed `ConnectionsSlot` | Actual `SharedProviderSourcesDialog` and secret metadata, network/authority policies unchanged |
| Source refresh | Typed `SourceRefreshSlot` | Actual `SharedProviderRefreshButton`, exact provider and delivery |

The old pricing/form/state public types retain their namespaces and module type forwarding.
The old Thinking component remains a compatibility host for its source-refresh effect.
The renderer, its children and scoped CSS physically live in the leaf. There is no copied
BaseLib, provider executor, vault, pricing policy or Thinking policy implementation.

## State and mutations

A successful same-target selection is a no-op. Reference refresh preserves the acquired
draft, its context, raw text and target lifetime. Failure of an optional reference refresh
is a warning; initial acquisition failure and a genuinely missing target still block the
editor and support retry. Explicit New and a different target acquire a new lifetime.

`ProviderEditorDraft` associates presentation text, field revisions and JSON validation
with the original EditContext. It does not introduce a second persisted model. Text inputs
update on input, before blur. Numeric inputs retain invalid strings through the original
InputNumber validation contract. The four local tab bodies remain mounted; only the active
footer is rendered. Sharing and History are separate forms, never nested in a provider form.

Save captures the entire request before awaiting. Confirmation reconciles unchanged fields
individually, binds the committed identity/token, and retains later field edits. Price rows
use their original object identities in the live form and the submitted model identifier
for canonical matching, rather than a current row index. Field revisions also distinguish
typing away and back from a field that was never edited. Unknown outcomes retain the existing
exact attempt/candidate verification protocol; known commits remain committed when later
reads fail. No schema, persistence, registry, authority or provider protocol was redesigned.

Thinking captures the model instance, identity, kind, transport, purpose, model catalog,
configuration, discovered capabilities and relevant field revisions. A changed origin
cannot Apply stale modal data. Cancel changes nothing. Unknown JSON extensions and policy
precedence use the existing parser/writer. Invalid JSON remains visible and blocks Save.

Health addresses the saved provider and does not save the form. Model discovery captures
the draft and prepares models/prices without persistence; later typing supersedes its
result. Retained shared-provider deliveries still reconcile through their original owner.
Source-managed profiles retain exact identifiers, constraints, credential bindings and
read-only local controls. Only secret reference/name metadata crosses the presentation seam.

## Dependency and delivery contract

The leaf references safe Models and BaseLib only. Its evaluated closure includes Models'
existing neutral abstractions; it excludes module implementations, ProviderManagement,
Infrastructure implementation, Core/runtime/drivers, EF, Voice, Canvas and Web. The sandbox
references only this leaf and its framework. Production theme CSS is linked as content,
not as a Web project reference. BaseLib registration, theme/fonts/icons and isolated CSS
are served by the sandbox in source and published modes.

Boundary tests traverse the actual compiled closure and reject forbidden, unresolved and
cyclic edges, including transitive ones. Public property/constructor/method signatures are
checked and renderer injection of services is rejected. A2, Workspace, Projects and Resources
roots must retain their previous closures. Graph/watch comparisons are recorded with the
task evidence, not hard-coded into a claimed target project count.

The supported viewport is 1920×1080 at 100% zoom. `ListDetailShell` owns the catalog/editor
panes; the rate table owns horizontal scrolling. Shared tabs, form sections, text controls,
sticky footer and Dialog preserve the original composition. Thinking actions use the Dialog
footer. No small-screen validation or redesign is part of PP1.

## Proof and closure

The selected PP1 renderer cut is complete. The immutable package, its NOT_RUN template,
sealed manifests and historical bundles are unchanged. Current private evidence is
`artifacts/provider-profiles-pp1/20261002-46d745a/evidence.json`; it records each attempt's
exact project/filter/commands, discovery, TRX counters, source hashes and compiled inputs.
Original failures remain separate from their passing follow-ups.

| Final selection | Discovered / passed | Evidence attempt |
|---|---:|---|
| Provider owners, callers, policy and A2 unit coverage | 361 / 361 | `unit-final-02` |
| Provider/native host components and retained integrations | 111 / 111 | `components-final-02` |
| Independent renderer, draft, lifecycle, graph and sandbox contracts | 30 / 30 | `leaf-final` |
| Native mutations/recovery, ownership, source/history and consumer adapters | 196 / 196 | `integration-native` |
| A2 editor preservation | 30 / 30 | `a2-preservation` |
| Six existing Editor/Projects/Files/Workspace/StorageSelection/Resources boundaries | 18 / 18 | `neighbor-*` |
| Source/published sandbox, provider/A2/Simple Chat/file journey, Workflow positive/negative | 5 / 5 | `browser-final-03` |

These are 751 distinct selected cases, with no failures or skips in the final selections.
Each no-build execution follows validated discovery and matching built binaries. Web's final
direct build and the final browser run were serialized after all watch hosts were stopped.
The earlier attempt disturbed by competing builds is retained as failed evidence.

The initial five regression cases reproduced four failures: reselect, reference refresh,
optional-reference failure and field reconciliation. Failed-target retry already passed.
The selected repairs preserve the original draft/context and reconcile untouched fields
without erasing later typing. A separate native failing-first browser assertion reproduced
Enter in the tag input implicitly submitting the enclosing form. The renderer now prevents
that input's default Enter submit while retaining TagEditor's own tag action. No BaseLib
or persistence redesign was needed. Initial component/leaf assertions and dispatcher issues
were repaired to exercise the moved controls and the shared context explicitly.

The final semantic journey creates a provider through the real UI and round-trips all four
tabs through native owners. It proves invalid raw input blocks Save, Thinking Apply alone
does not persist, and unrelated providers remain unchanged. That saved provider is selected
in the completed ten-section A2 editor and in Simple Chat. Simple Chat uses another model
accepted by the existing native model-suggestion policy; the policy was not relaxed for a
synthetic fixture name. The retained same-target selection helper observes the new no-op
contract instead of waiting for an editor to detach.

Two final proof corrections retain native contracts: the conversation read explicitly requests
a transcript page, and reused helpers send their full-page navigations through the existing
browser oracle. The metadata-only read does not promise message entries. The oracle's strict
request-failure rules and bounded expected-disconnect accounting were not changed.

The Project Structure journey checks exact proposed file arguments before approval,
committed identities, two native read-backs, preview/download hashes and unchanged sibling
content. Simple Chat checks the saved definition/provider/model, conversation and successful
native operation/message identity and content hash. Workflow/TestLab preserves both accepted
output and the HTTP-success-but-incomplete-output rejection control. Only external model
output is scripted on loopback; no paid requests were made.

Source and independently published Production sandbox browser cases passed at 1920×1080,
including focus, raw validation, scrolling, footer/dialog actions, every scenario, independent
second editor and late typing during held Save. Served assets and fonts have no HTTP/browser
errors; per-host compiled DLL/PDB/CSS/font hashes and served asset paths are retained.

Actual Sharing, History, source connection and refresh hosts have native identity,
composition, authority and reconciliation coverage. An authorized external two-instance
publication fixture was unavailable and is explicitly BLOCKED. No external publication or
credential issuance is claimed. This does not turn those retained integrations into extracted
or sandbox-owned features.

Broad Stable was not rerun to change its historical headline. Full bounded provider
owner/caller selections and native mutation/recovery/source tests cover the lifetime and
submission changes. There is no authority, schema, persistence or durable protocol redesign,
and all 44 existing non-Web graph roots remain unchanged. New isolated solution/CI entries
do not alter common test-host behavior. Reviewed portability baseline deltas cover moved
presentation/model comparisons and the sandbox README fence; scanner rules are unchanged.
Final portability enforcement without a write flag, package integrity, documentation and
shareable-artifact secret checks are recorded in the closure evidence.

## Measured development loop

| Measurement | Original Web | Final Web | Independent sandbox |
|---|---:|---:|---:|
| Evaluated project closure | 166 | 167 | 10 |
| Watch files | 4,635 | 4,645 | 457 |

The provider leaf itself has a 9-project closure. Across the evaluated roots, existing
non-Web closures and Projects source hashes are unchanged. These counts come from evaluated
MSBuild and `dotnet watch --list`, not a hard-coded target size.

Ordered edit-to-visible samples in seconds (median in parentheses):

| Host/method | Razor | C# | Scoped CSS |
|---|---|---|---|
| Original Web / hot reload | 143.159, 28.744, 23.981 (28.744) | 27.864, 5.167, 5.010 (5.167) | 76.082, 35.882, 14.862 (35.882) |
| Final Web / hot reload | 8.683, 7.517, 5.101 (7.517) | 5.482, 1.238, 1.075 (1.238) | 3.133, 25.841, 10.829 (10.829) |
| Sandbox / rebuild and restart | 6.412, 6.109, 5.685 (6.109) | 4.974, 5.394, 5.169 (5.169) | 4.905, 4.387, 4.810 (4.810) |

Web measurements use polling watch with hot reload in isolated original/final source copies,
with the same source dependency inputs, configuration, viewport and observation sequence.
Each observation includes navigation, hydration and normal New action; the C# probe changes
the new-draft status text. All probe bytes were restored and hash-verified. No feature-owned
JS file exists; the inline tag guard belongs to the Razor workload.

The working-checkout Web watcher built but stalled after static-asset manifest warnings.
Sandbox repeated Razor hot reload stopped becoming visible on sample two in both checkout
and clean copy, despite detecting the edit. Those failed attempts remain recorded. The nine
successful sandbox observations therefore use watch with hot reload disabled and automatic
rebuild/restart. Do not compare that method's latency directly with Web hot reload or infer
a uniform speedup. This is a tooling limitation, not a claim that repeated sandbox hot reload
passed. The smaller independently runnable graph and successful restart loop are verified.

Initial build, watch workspace load, browser navigation and hydration are separate fields
in `measurement-summary.json`. Launch-to-first-HTML is only an observed readiness upper bound
including scheduling delay, not pure server startup time. First/warm variability, the earlier
long-path/no-build/watcher-buffer attempts and the too-short observation deadline are retained.
No measurement ran concurrently with tests or other builds.

All owned watch/browser hosts, PostgreSQL/relay/network/volume fixtures and temporary source
copies are disposed after their final evidence is captured. Ownership receipts protect the
normal application host, unrelated processes and sibling checkouts. Raw evidence remains
private; only explicitly selected shareable evidence is scanned and summarized.

## Remaining wave

| Family | Disposition |
|---|---|
| Projects P1 / Files P2, selected Workspace families, technical agent A1/A2 | Preserve completed renderers and native effect hosts |
| Provider catalog + Connection/Prices/Runtime/Thinking | Complete selected PP1 cut with recorded native and consumer proof |
| Sharing/publication/credentials, source connections, request History and operational dialogs | Working native host slots; later provider cut requires its own review |
| Capability/team authoring, residual chat/usage/Voice, Workflow, Workbench, Processes, Web residuals | Not started by PP1 |

`all_provider_ui_complete` and `application_release_ready` remain false. No push, merge, PR,
deployment, external source publication, credential issuance or historical-bundle cleanup
belongs to this task.

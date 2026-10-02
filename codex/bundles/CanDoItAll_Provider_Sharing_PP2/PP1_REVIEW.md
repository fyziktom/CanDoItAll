# Review of the pushed PP1 implementation

Review baseline: application `00c395ba0f4ce62c611c62718d8a510f9044f7b4` (product commit
`providers refactor`), parent `46d745ad1a9803f3945523f656b4d1dd26044b85` (PP1 entry/history).
Source facts below use the pinned register. C#/.NET, browser, Docker and watch executions by
this package author: NOT_RUN. No SDK or Docker executable is installed in the review environment.
The author's output checks are package/Python checks only.

## Preserve

R01 reports the actual leaf, real four-tab renderer and isolated sandbox. R02/R03 show the
production view adapter and existing native operations, not a second provider backend.
R06/R07 capture editor field revisions, raw configuration validation, full submission and
original pricing-row identities; reconciliation merges unchanged fields and retains later edits.
R24 shows the acquired same-target no-op and metadata-refresh retention. This is appropriate
for a writable local draft and must not be reverted globally.

Runtime/Connection display getters map imported model IDs through `GetModelDisplayName` (R03).
The protocol deliberately separates display and routing (R19/R20/R21). This is the correct
architecture; exact ID equality between source upstream names and client wire values is NOT
an acceptance requirement. Model labels must remain equal while routing remains opaque.

## Two concrete follow-ups

S0-R1: R04 interpolates `provider.DefaultModel` directly in the tree tooltip, and R05 passes
it to the real TreeView node. An imported default is an opaque route ID. Unlike the visible
Connection/Runtime getters, this human-facing description does not map the source name.
This is a bounded presentation inconsistency, not evidence of a wrong upstream invocation.

S0-R2: R24's Refresh accepts a new catalog then calls SelectAsync. AcquireAsync returns a no-op
for any ready same-ID draft, including imported profiles. R03 obtains labels from the NEW
SelectedProvider but model values/default from the OLD immutable-owned draft. A sync in another
client circuit followed by toolbar Refresh can therefore mix revisions: new models are absent
and removed old IDs are rendered through the new name map. The metadata-only refresh intentionally
preserves local edits; it does not establish correctness for remotely owned read-only fields.
Reproduce the two-circuit sequence and adopt a narrow source-owned reconciliation path. These
are source-derived findings; no new runtime reproduction is claimed by this review.

## Important proof gaps

R01 explicitly reports ExternalSharedProviderUi BLOCKED, not executed. Native in-process tests
and a UI-created local provider do not establish publication/import/relay correctness across
separate app containers, independent keys, isolated catalogs, network policies and restart.
The existing UI fixture R08 rewrites model lists to synthetic names; it cannot by itself prove
that native OpenAI preset/catalog metadata survives sharing. S0 adds this missing lane.

R01 also reports repeated sandbox Razor hot reload becoming invisible on the second edit.
Successful measurements used rebuild/restart with hot reload disabled. Preserve this distinction;
independent graph and restart-loop success do not mean repeated hot reload succeeded.

## Recorded PP1 results (not rerun here)

| Selection | Reported result |
|---|---:|
| Provider owners/callers/policy and A2 units | 361/361 |
| Native host components / retained integrations | 111/111 |
| Leaf renderer/draft/lifecycle/graph | 30/30 |
| Integration mutations/recovery/source/history/consumers | 196/196 |
| A2 preservation | 30/30 |
| Six neighboring boundaries | 18/18 |
| Sandbox, saved-provider/agent/chat/file, Workflow controls | 5/5 |

R01 describes these as 751 distinct selected cases. Private original TRX/log/image inputs were
not supplied to the reviewer; that count is an attributed implementation report. The external
fixture is outside the successful selections. Broad Stable was not rerun in PP1. Preserve its
historical mixed A2 result and later targeted repairs, never infer a new release-ready state.

The plan is to preserve PP1, complete rigorous S0, fix bounded findings and then extract PP2.
Neither missing cross-container proof nor the hot-reload limitation is proof of broken provider
persistence. Equally, neither can be silently described as passing.

# Review of PC4

Review source: `fd9e43547457eb18901185da47de123efdfd8108`; comparison base:
`a13c1add605d1ceec43dedc0513acb7ac5959f96`. The connector reported 16 commits ahead and a
valid GitHub PGP signature for HEAD. The report-only HEAD points back to executable source
`ca195c794349d4e4eb653bacb7605ed83df86c4b`. These are provenance, not execution requirements.
References below resolve through [review-sources.json](review-sources.json).

## Reviewed corrections to preserve

| Area | What the source now does | Assessment |
|---|---|---|
| HTTP launch | `MapLaunchRequestAsync` checks original-intent replay before fresh project/definition capture. One captured definition closure goes to project-node variable preparation and `ResolvedDefinitions`. | The earlier producer inconsistency is corrected in the examined path. |
| Empty drivers and public identity | Captured canonical selection is retained; explicit unknown/conflicting IDs are refused; XML documentation reflects that behavior. | Preserve the native identity/fingerprint boundary and current API shape. |
| Retained launches | `ProcessRetainedLaunchRecovery` validates runtime/plan identities and accepted preparation evidence. Missing or contradictory origin produces an explicit reconciliation refusal, not an absent-run result. | Preserve safe reuse and refusal; never weaken lifetime checks to reuse by public project ID alone. |
| Role delete/reimport | Delete removes the head's matching resource and placements; Add/Import allocate keys against usages and resources together. | The demonstrated orphan-resource collision is addressed. Immutable earlier revisions are not rewritten. |
| Behavioral tests | HTTP tests hold enrichment across publication or project replacement and assert actual preparations/assignments. The predecessor browser test verifies native reentry, retained rows and a new OS process, plus browser visibility. | Meaningful layer-specific proof. It is not an external live-model or all-browser-click proof. |

No new PC4 blocker was identified in these reviewed paths. This is not a claim that every
line, every predecessor input or every runtime behavior was independently proved. Reopen a
PC4 topic only for a reproducible new failure, a source delta, or a dependency changed by CD1.

## Reported validation, not reviewer execution

The maintained PC4 record reports a fresh complete Stable union of 63 class shards over
34 projects: 17,346 discovered cases and 17,401 successful executed rows, with theory expansion
accounting for the difference. It reports no failed/skipped rows in that final union, 18/18
native Chromium browser cases, product/test builds, portability enforcement and the canonical
pending-model check. Interrupted attempts without complete TRX are not counted as final passes.

The executable tuple uses Windows, Release, SDK 10.0.303, Components
`f3745356182444656edfdef56aa827095632d3ea` and FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. FileTools' CI pin
`498b36825bd5a5222429972af120b04becf4b3f6` has the same recorded tree; do not repin merely
because the commit strings differ. Recheck current inputs at execution.

The reviewer read the maintained report and selected test/production sources, not its private
TRX or screenshot files. No C# build, PostgreSQL, Playwright, live model, evaluated MSBuild
closure or CodeAnalytics query was executed by this review. GitHub returned zero check-runs
and zero Actions runs for HEAD; the current workflow does not trigger on a push to this branch.
These facts neither invalidate the reported local run nor create an independent CI pass.

## Limits that do not justify another PC wave

The PC4 semantic index covered one Application project, not the whole repository. Its
Processes census classifies nine active renderers and their owners, not every product surface.
Existing library names and a passing Stable union do not replace a whole-product rendering census.

Authoring-aware portable transfer/export is safely refused pending a separate history/remapping
contract. Large-catalog database paging is also separate. Preserve these guardrails and record
the product limitations for the next owner; do not silently claim those features implemented.

Two small, inactive prior tool-recovery directories were retained because deletion was blocked
by approval policy. They are accurately reported operational limits, not unfinished component
extractions. Do not bypass the denial or reproduce the earlier large fixture/output accumulation.

## Why CD1 rather than PC5

The concrete remaining work is at the application boundary, plus global closure and targeted
integration proof. The shared historical map still discusses PC2; Agents and Workbench records
contain historical stage language. Their recorded structure can be reused, while the current
source and evidence register must supersede their task status outside shared. Do not reinterpret
an old demo, live-provider or validation checkpoint as permission to re-extract finished renderers.

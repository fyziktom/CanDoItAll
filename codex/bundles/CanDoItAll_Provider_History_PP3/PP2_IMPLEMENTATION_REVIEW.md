# Review of the pushed PP2 completion

## Verdict and evidence boundary

Reviewed main HEAD is `643a295e112ca29835323501907bf1d8920a5945`, seven commits ahead of S0.
The compare contains the historical completion handoff and actual production/UI/test changes.
The final commit is a test/closure checkpoint, not evidence that only documentation changed.
GitHub reports a valid signature for HEAD. [S01–S03]

The inspected PP2 boundaries and local-concurrency repair should be retained. No additional
blocking defect was established in those inspected paths. That is not a full independent execution
of every test: the reviewer had no .NET/Docker/PowerShell toolchain or private raw receipts.
The retained report, source implementations and test code were read; product results below are
implementer-reported, not independently reproduced. [S04–S06]

## Implementation that must survive

`SharedProviderImportDraft` owns separate Baseline/Latest values and token pairs. Clean drafts adopt
saved local settings; dirty drafts accept a metadata-only revision only when baseline local values
still match. A changed local alias/enabled pair raises a conflict. Explicit resolution keeps only
edited fields while adopting reviewed saved values elsewhere. Immutable submissions and field
revisions preserve post-submit input. Native management still owns optimistic checks. [S07–S09]

The actual Sharing/Sources/Refresh renderers are in a neutral five-project sandbox graph reported
by the implementation. The leaf declares BaseLib and SharedProviders.Abstractions rather than
ProviderManagement/Infrastructure. Native management, recovery, delivery acknowledgement and profile
composition remain in the host. Source and catalog submissions retain the original operation while
read-back/delivery retries do not repeat the write. Inspect successors after awaits rather than
reintroducing current-row assumptions. [S04,S06,S08,S10]

The same-provider refresh fix defers a re-read while a Sharing mutation/delivery is busy, then
processes the pending revision on completion. A different provider still retires the old operation.
Workflow provider projection now carries model display catalog and source ownership into the
existing neutral selector; opaque route IDs remain actual values and source-managed overrides are
refused. Neither change is permission to rewrite provider persistence or the Workflow engine. [S08,S11]

## Reported proof: do not flatten stages

The final image reports 19/19 protocol scenarios and native-default comparison across source and
both clients. The initial final-consumer selection passed 6/9; exact follow-ups completed History/
credential rotation, Agent/file and image/vision. The nine distinct journeys are accounted for by
multiple receipts, NOT by a replacement claim of one green nine-case run. Image inputs are tied to
signed production and a documented test-harness delta; later source equivalence is a report claim
until the native raw artifacts are inspected at execution. [S04–S06]

Frozen Stable: 24 assemblies, 16,104 discovered rows, 16,159 expanded runtime cases; 16,144 pass,
15 fail, zero skipped. Ten Integration failures are attributed to the owned one-gigabyte tmpfs
filling; the identical assembly later passes 102 selected cases on a larger isolated PostgreSQL.
Four failing guard/classification checks have test-only corrections. The source-secret scan passes
on a hash-verified source-only copy, with separate safe-artifact scanning. It does NOT retroactively
scan ignored historical artifacts or make the original checkout Stable all-green. [S05]

The clean SharedProviders sandbox reports three successful same-PID Razor/C#/CSS updates. This
improves current dev-loop evidence but does not explain or relabel the old PP1 hot-reload failure.
No paid live-model request was issued. [S04]

## Remaining bounded items

Two logged detached-circuit Tooltip disposal cancellations match an uncaught cancellation path in
the current sibling TooltipInterop. Reproduce/repair S0 in the actual component owner, with negative
controls for unrelated active-call exceptions. Existing Tooltip tests cover successful overlap and
late import, not this failure. Do not call it a proved PP2-introduced regression. [S05,S12,S13]

Keep the pre-campaign antiforgery rejection as an unassigned historical observation unless it recurs
with a reproducible current caller. Background History maintenance budget retries and mapping/query
warnings were not declared clean. During PP3 measure actual read bounds and classify any recurring
failure; do not suppress logging or authorize a wholesale maintenance redesign. [S05]

The next family is Request History, not a replay of PP2. Its partial neutral results/metadata/content
UI exists, while full filters, read/detail lifetime and production hosts still live in the module.
Complete that coherent boundary before provider operational dialogs. [S14–S23]

See [sources](SOURCES.md) and [test matrix](VALIDATION_MATRIX.md).

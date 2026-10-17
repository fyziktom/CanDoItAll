# Managed continuation: integrated validation checkpoint

Status on 2026-10-16: integrated, compiled and executed: **404 default cases,
395 passed and 9 failed**, plus **one browser case passed**. The frozen candidate's
final portability enforcement passed. Targeted repairs are prepared in the managed
checkout and require a new build/discovery/execution checkpoint. This record does not close the broader initiative or replace
the [incomplete Stable result](P02bc-stable-remediation.md).

The parent transferred exactly 145 reviewed source/document files from the single
managed checkout into the clean primary checkout at
`702d0a93b1081c42d5a06893e7070219aee5d089`. The ignored
`managed-candidate-transfer.json` records every original target and incoming file
hash; all copied hashes matched. Main remained frozen throughout this execution;
subsequent managed authoring is separate and has not inherited its results.
Subsequent primary edits corrected four missing namespace imports, refreshed the
reviewed Workspace.Contracts rendering fingerprint and clarified the C2 retained
receipt limit/source lease order. None changes the independently counted test rows.

## Builds

All affected production projects were built directly in standard Release with
`UseLocalCanDoItAllLibraries=true` and `/m:1`: SharedKernel, AgentFramework.Models,
Infrastructure, AgentFramework.Core, AgentFramework.Maf, Workspace.Contracts,
Projects.Contracts, Projects, Projects.UI, Workbench, Composition and Web.
All twelve have a final zero-error build. Unit, Components, Integration and
Playwright owning test assemblies also have zero-error builds; Integration builds
the maintenance child through its existing build-only reference.

Original failed Workbench and Integration build logs remain retained. Their errors
were missing `CanDoItAll.SharedKernel` imports in the new task-basis implementation
and Integration fixture, and the concrete Infrastructure factory import in the
effect-only fixture. Only affected pending builds were repeated after corrections.
Existing package advisory warnings remain; zero errors is not a zero-warning claim.
Detailed command arguments, durations and log hashes are under the existing owned
`.artifacts/concurrent-mutations/validation/p02b-managed-validation` proof root.

## Focused test plan

Independent source accounting records **404 expected discovery/execution rows in
27 default lanes**. Each lane has an exact owning project/filter, fixed Fact or
InlineData count, source identities and explicit category exclusions in
`p02b-focused-selections.json`. There are no dynamic or skipped cases in this plan.
The original stalled continuation is a separate one-case regression. The editor's
conditional whole-class confirmation adds 17 rows, overlapping its one-case
diagnostic; the real browser creation case is a separate opt-in row.

All 27 default lanes matched source-derived and build-backed discovery counts,
then executed against the same frozen source/runtime files, with zero skipped cases.
The failures were two exact receipt-provider inventory assertions, three emitted
OpenAPI shape assertions, two API description coverage assertions and two Workbench
component assertions. They exposed an omitted existing HR provider in the fixture,
missing emitted array/enum/reference metadata and an incorrect 415 response schema,
plus assertions that ignored successful seed removal and accessed a disposed bUnit
component. The real routed 415 remains the existing general API error envelope.
No runtime media acceptance was broadened to satisfy a test.

All 23 commit-knowledge owner regressions passed. The originally stalled
continuation passed after its receipt-only provider composition was separated.
The editor acquisition diagnostic passed once; its original intermittent failure
cause remains unresolved and its conditional 17-case confirmation was not run.
This focused execution does not complete the interrupted broad Stable suite.

The opt-in Workbench browser case passed (one discovered/one executed). Its two
1920-by-1080 captures were visually inspected: exact recovery reference/actions and
disabled repeat-save state were readable, including the scrollable modal/footer.
Owned host PID 38800, port 56411 and its exact isolated database were absent after
fixture disposal; Web runtime identities were unchanged. The process snapshot was
taken after completion, so it is not a full historical descendant/start ledger.
See [the Workbench record](P02cd-workbench-create.md) for artifact identity.

The retained manifest SHA-256 is
`ef652e34d114adb745cd8541ab2832c60ce7707ff63cf7bdaf6563324ca5b69e`;
the frozen source identity is
`d119650d114b17b6098d8318aaa7f63e17403657cb58541ce5f4c384a366c3f6`.
The corrected summary explicitly sums dictionary values; its earlier null-total
aggregation is retained separately, and no test record was changed. No failed lane
or unknown-outcome command is overwritten or automatically retried.

Capacity, performance, current HTTP authority observation (H2), R1b/c effect
reconciliation, later owner families and the remaining producer cutovers are still
separate gates. Dormant contracts/capture code do not establish their activation.

## Static and resources

Baseline-tool tests passed 6, secret-scanner tests passed 4, and rendering tests
passed 7. The rendering register passes 1,196 inputs after its reviewed fingerprint
refresh; that refresh does not claim a new evaluated project-reference inventory.
The final complete scan covered 8,425 files of 8,450 candidates, including 16
untracked protected inputs, and returned 36,409 findings without truncation.
All 17 added/two stale baseline findings were reviewed; the intentional refresh
changed allowances from 15,439 to 15,454. Final enforcement **without the write
flag passed with 15,454 allowances**. Scan SHA-256:
`539a4298f5e8c4cfb2e55f27e7b773c36a5de061b179b53bb8f1234c4b36ab5d`.
Baseline SHA-256:
`d3ba07cc8235e7e5acb41fc8d576e77f8e7ba71456ede27e84a3ae187fde4b25`.
Later source changes require renewed scan/review/final enforcement.

The existing owned PostgreSQL server is reused. The interrupted test's exact
database/directory cleanup and the inactive SPA hook-directory cleanup were rejected
by automatic approval review; their exact retained resources and blockers remain in
the [resource record](../resources.md). No replacement server was created.

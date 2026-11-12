# P02a validation in progress

This is an intermediate conditional-write milestone, not complete Projects replay or
whole-engine closure. It is included in the incremental P02a implementation commit.

## Executed core proof

Release builds completed for Projects.Contracts, AgentFramework.Models, Projects.UI,
Projects, Workbench, Web, Projects.UiSandbox and the canonical PostgreSQL migrations.
The final native UI changes were also refreshed before execution. Existing NU1902/NU1903
dependency advisories remain; no package remediation is claimed by this change.

`FullyQualifiedName~ProjectDefinitionConcurrencyTests|FullyQualifiedName~ProjectDefinitionHttpTests`
discovered exactly **31** cases from refreshed Integration output and passed **31/31**,
zero skipped, in 1m47s against the task-owned PostgreSQL 18.6 fixture. Owner tests are
17 cases; HTTP is 14. Proof includes actual stale full/scalar rejection, independent
project updates, child ownership and deletion, frozen-clock root advancement, a paused
coherent read, and a root revision changed after admission but before SQL execution.
That last case verifies failed root CAS rolls back proposed child removals. The HTTP
release-failure case verifies a committed scalar acknowledgement survives lease cleanup
failure. Logs/TRX are under ignored `.artifacts/concurrent-mutations/validation`.

The next owning regression filter discovered exactly **80** cases and passed **78** in
4m17s, with two failures and zero skipped. Its first run found
a historical migration assertion reading the complete current EF entity after a
deliberately refused downgrade had already removed a newer technical column. The test
now projects its actual subject, `ExternalKey`, without assuming the current schema.
The new same-ID tool-recreation case also exposed a duplicate template key in its second
agent fixture. Both repaired cases passed in a seven-case rerun together with five actual
MAF disclosure/revocation cases, including the new definition-read tool (51s, zero skipped).
The original failed run is retained; the unaffected 78 cases were not needlessly repeated.

The rules/tool-policy selection discovered and passed **142** cases. The lightweight
Projects UI topic discovered and passed **38** cases. The native editor/page/URL topic
discovered and passed **51** cases (17 editor, 26 page, 8 URL; 2m24s), all with zero skipped.
The native browser conflict selection discovered and passed **1** case in 27 seconds,
zero skipped, against an owned real Web host and PostgreSQL lease. Two independent
1920x1080 browser contexts used the same local operator: this proves concurrent drafts,
not separation of authorization principals. Stale root/phase/option editing preserved
unsaved input; comparison did not adopt the newer revision; invalid partial date input
blocked manual reconciliation until explicitly cleared. Manual reconciliation and
explicit discard/reload both preserved the competing saved children. Both screenshots
were visually inspected, and the host PID, listener and database lease were absent
after disposal; exact evidence is in `resources.md`. Final production output was
refreshed after the last UI source edit (15:30:08 UTC): UI DLL 15:30:17 and native Projects
DLL 15:30:18. No later production UI edit is included in these passed runs.

The passed regression set includes both new revision migration cases, four Projects owner
mapping/persistence cases, six historical lifetime migration cases and the strengthened
transfer/package case. Retained revisions survive two reopen/migrate cycles; raw inserts
receive distinct nonempty revisions; a source revision cannot update an imported target.

## Dependency review

CodeAnalytics snapshot `snap-20261016152552-09fa4aa8` scoped three projects and 83
documents: Projects.Contracts, Projects and Projects.UI. Within that scope it reports
no project cycles and the expected edges Projects -> Contracts/UI, UI -> Contracts.
The writer is registered Scoped. Focused consumer-outline output selected no source
blocks, so this is not claimed as complete semantic usage proof. The snapshot reports
191 diagnostics, including 187 workspace errors from existing NuGet advisory/MSBuild
loading reports; actual Release builds provide compilation evidence. External dependency
closure and owner/source contracts were also reviewed directly. No project references
were added by this slice.

Independent review found and repaired the original/current lifetime mismatch in the
registered tool, loss of known scalar acknowledgement on lease cleanup, and native
postcommit draft-reconciliation classification. It confirmed original revision predicates,
snapshot reads, sorted owner/source locks, and child-writer/maintenance boundaries in
the inspected perimeter. The final native accepted-result retention review found no
remaining source blocker; its conditional execution requirements passed as recorded above.

## Static review

Final complete scan, including untracked protected source: **8,220 files / 36,160 findings**.
Pre-refresh enforcement reported exactly four new executable-source fingerprints, all in the generated
canonical migration designer: existing `RootPathState`, `RootPathSyntax`,
`RaisesEscalation`, and `EscalationCount` model properties copied into the new historical
model. They introduce no host path, process launch or platform-specific implementation.
These are reviewed intentional snapshot additions. The reviewed baseline diff adds only
those four allowances plus count/timestamp metadata. Final enforcement **without**
`--write-baseline` passed: **15,423 reviewed executable-source findings unchanged**.

Portability-tool tests passed 6+4 cases; rendering-tool tests passed 7. The rendering
register passed with **1,189** current inputs after the final native review refinements.
Documentation validation passed for **424** maintained Markdown files; `git diff --check`
passed. Canonical `has-pending-model-changes` found none. The generated idempotent SQL
from `20261009124700_AddProcessAuthoring` contains only the guarded nonnull UUID column
addition with `gen_random_uuid()` and the migration-history insert, in one transaction.
No destructive or unrelated schema operation is present. The existing EF tools/runtime
minor mismatch (10.0.3 / 10.0.4) was reported; the command completed successfully.
These checks do not replace runtime, browser, performance or broad stable proof.

## Remaining proof

The matched candidate performance result and open tail-latency investigation are in
`projects-performance-candidate.md`; they are not a P02b capacity pass. P02a is an
intermediate original-state milestone. P02b still changes the shared lifecycle and
canonical schema; the named broad Stable checkpoint is the integrated, frozen P02
foundation, not each intermediate repair or commit. No broad Stable result is claimed here.
No resource cleanup claim is made while the shared owned PostgreSQL fixture is required
by later lanes and the unfinished end-to-end bundle.

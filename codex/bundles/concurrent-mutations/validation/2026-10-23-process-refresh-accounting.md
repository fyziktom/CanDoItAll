# Original Process refresh reconciliation accounting

Status: Pass with follow-up for original refresh accounting. P28 Behavioral proof tier. Signed prerequisite
`bead7cbcd59b21a69db25a3f975e292aa4110fd0` qualifies original-child stop and commits the
concrete accounting design before implementation. Full refresh remains unadvertised.

## Owned behavior

This checkpoint owns the reconciliation portion of the user's requirement that users,
agents and workflows can edit concurrently and that an agent may keep its start snapshot
or reload under an explicit policy. It does not close that whole requirement. The shallow
implementation would treat a stopped Agent as unused work or complete every Reconciling
scope. Real accepted-effect, unknown-publication, cancellation and forged-command cases
distinguish those outcomes and require the original evidence.

The registered Application coordinator resolves the exact admitted action and original
input/child, then checks its deterministic accounting receipt before pending-state checks
or another owner read. Typed pending reasons preserve unknown, missing or open work and
its original grant. Owner reads occur outside SQL. NoEffect settles the original local
and linked root work once while retaining the blocked Process and Reconciling generation.
An accepted effect completes its original result with its verified terminal status and
work; refresh reports that original completion overtook it. Neither path captures fresh
material, enrolls a successor or dispatches provider work.

Runtime owns the accounting identity and exact permitted mutation shape. Application
owns original-input preparation, orchestration and receipt interpretation. Persistence
rechecks the retained admission, original child/material, current root/generation/scope
and original budget under the existing root transaction fence. It receives no Agent
callback. Only this validated intent can permit an original accepted completion through
the frozen shared-scope guard. A distinct pure refresh completion transition still requires
the original terminal member and complete bounded membership/allocation quiescence.
Ordinary result/scope completion and root cancellation retain their existing authority.

The optional accounting reference belongs to the in-process commit request. Existing
version-7 generation identity, closure and settlement receipts retain durable authority;
no receipt format, migration, project reference, Core contract or public API changes.
Normal terminal result events retain their exact RuntimeProjection messages. NoEffect
creates no event/message, and external/substituted messages are refused. A selected
receipt lookup may load the current run through the existing idempotency owner; this is
an explicit continuation, not a history polling or global worker interface. Mutation
validation compares the selected structures without serializing runtime history.

## Candidate and qualification

Four changed production owners (Runtime, Application, Persistence and Modules.Processes)
and both test projects build directly with standard Release outputs and serialized local
library builds. Source-derived discovery and results confirm 275 passing cases: 210 Unit
and 65 Integration, with no skipped or unexecuted selected case.
The new accounting classes contribute 33 Unit and 26 Integration cases. Existing run-scope,
review completion, terminal accounting and original-child-stop selections cover dependent
behavior. Exact filters, discovery and command records are retained in
`.artifacts/concurrent-mutations/validation/c4-process-refresh-accounting-verified`.

The first candidate passed 19 of 31 new Unit cases and failed twelve: its guard incorrectly
forbade the RuntimeProjection messages created by normal result completion. Execution
stopped before other lanes or SQL. The repair permits only each exact event's projection
and adds two negative payload/subscriber cases. Its failed evidence is retained under
`c4-process-refresh-accounting`; none of its passing cases substitute for final proof.
Its resource observation closed with no process, database or temporary-resource delta.

The repaired candidate passed all 210 Unit cases and 24 of 26 new SQL cases. Two assertions
incorrectly expected the three-second cost of a different fixture helper; this fixture's
manager review records one second. The assertion now compares with the actual closed owner
work. `c4-process-refresh-accounting-final/test-only-repair-comparison.json` proves that
only this Integration test source and its DLL/PDB changed; production and Unit outputs are
identical. The final selection passes all 65 SQL cases, retaining the unchanged 210 Unit
cases. The failed two cases are not counted as passing proof. Exact discovery rejects zero,
missing, extra and stale method names; every lane stops on failure.

Each selected class uses `dotnet test <owning-project> --configuration Release --no-build
--no-restore -p:UseLocalCanDoItAllLibraries=true /m:1 --filter FullyQualifiedName~<namespace>.<class>.`
with both TRX and normal console loggers. The manifests, discovery reviews and command
records contain the complete arguments, exact class names and expected counts. No
unfiltered suite is selected: there is no new global build, dependency or bootstrap
invalidation trigger. Existing broad-suite limitations remain in the closure ledger.

The SQL cases exercise both snapshot modes through registered Workbench/Core owners:
proposal-only stop/settlement, accepted-before-fence completion, simultaneous continuations,
transaction rollback, lost actual SQL commit acknowledgement and full host restart with
child reads forbidden, root cancellation after owner observation, and actual uncertain
Project publication. Direct forged-commit cases cover missing authority, changed allocation,
wrong shared-scope expectation and altered result. Replay cannot charge work twice.

## Architecture, static checks and resources

Snapshots `snap-20261023170919-65134217` and `snap-20261023173045-65134217` cover Runtime,
Application and Persistence. The final scope has 309 documents, 918 types and 7,609 members,
the same three intended project edges, zero scoped cycles and no new finding. The collector
retains 189 WS0002 diagnostics and `hasBlockingErrors=true`; actual direct builds supply
compilation evidence. No business partial, service locator or extra transaction owner
was introduced. The small preparation, receipt and SQL guard types have separate reasons
to change and leave orchestration at its Application owner.

Final full portability scans 10,117 files and 47,423 findings. No-write enforcement passes with
all 15,697 reviewed allowances unchanged. Static-tool tests, rendering register,
documentation and whitespace checks pass. The candidate pins 1,263 sources and 44 outputs.
Final runtime/source reconciliation confirms all pins and outputs unchanged during each
run. The verified observer covers all seven commands over 510 seconds; the final observer
covers all four SQL commands over 1,149 seconds. Both have zero remaining observed processes,
zero database-name delta and zero new temporary paths. All three proof roots were recorded
before creation; the existing isolated PostgreSQL server on 53619 is reused and retained
for the next owner checkpoint. Six owned bulk scan files were removed after path/hash
review, preserving compact proof. Secret scans review the private-variable references in
the execution helper; sentinel and supplemental text checks find no credential value.
Ordinary 5032, SPA and installed tooling remain preserved. Prior cleanup denials remain
unresolved and are not retried here.

The architecture gate passes with follow-up: there are no new dependency or construction
defects, no production placeholder or fixture-specific branch, and independent Unit plus
registered SQL/owner tests prove both positive and negative behavior. The accounting owner
may be used by the next internal successor checkpoint; no public activation is qualified
by these tests.

Replacement capture and generation advance are next. Durable dispatch, public manual and
automatic refresh, native/SPA UI and whole-engine E2E remain open. SPA stays on
`codex/concurrent-mutations` at signed `1139939`, for colleague review without main integration.

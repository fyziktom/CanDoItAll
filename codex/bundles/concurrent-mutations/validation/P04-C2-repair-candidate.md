# C2 readiness and migration repair candidate

Status: completed with **321 passed, 9 failed, 0 skipped** from 330 executions. All ten
standard Release builds, discovery and final static validation passed. This is a new candidate after the retained
[482-case failure checkpoint](P04-C2-W1b-R1b-candidate.md), not a relabeling of that run.

## Changes and reasoning

The ten permanent refusal branches in the new finite-metadata migration now use P0001.
The three refusal Facts require exact `PostgresException`, `RaiseException`, and
`IsTransient == false`, retaining rollback, schema/history and payload checks. No model
or historical migration changed. Regenerated idempotent SQL must reflect this body.

The [installed-pool fence](../reviews/C2-installed-pool-fence-and-migration-refusal.md)
requires the exact compiled pool/version set and valid active retained prefixes during
startup, attachment and recovery. Fresh reactivation checks the previous active prefix
under the staged lifecycle exclusion before closing every old pool. An already applied
Prepared plan validates its committed target and cannot initialize missing issued metadata.
Unknown/orphan retained metadata or Open epochs outside the inspected namespace refuse
readiness. Queries run only at the control-plane boundary; foreground writes gain no
global audit. These checks bound returned data, not total work over retained history.

Four new PostgreSQL Facts cover old binaries, corrupt active/historical metadata,
lost lifecycle and lost activation acknowledgement, plus a real installer lock wait and
two-pool closure. The coordinator selection has nine rows including its five regressions.

[Fixture repairs](../reviews/C2-focused-fixture-repairs.md) use actual SQL transaction
identities and an independent reactivation flow. Workspace inserts explicitly declare
inactive heads; bootstrap tests use deliberate canonical attachment and a real provider.
The [historical rollback audit](../reviews/P04-C2-historical-rollback-fixture-repair.md)
targets each original migration before testing its refusal, then verifies forward upgrade.
Its seven classes retain 54 cases; no historical Down guard was relaxed.

The shared strict OpenAPI test resolver recognizes only the reviewed description plus
single-reference wrapper. Receipt fault injection now matches the Projects receipt query,
not the neutral epoch observation sharing its lifecycle alias. It still proves the closed
epoch was read, exact original accepted bytes survive, and no duplicate effect is created.
The synthetic epoch closure is explicitly restricted to the Projects pool.

[Workspace provider labels](../reviews/P05-W1b-provider-comparison-labels.md) use the
already-read reference catalog in both comparisons; exact saved IDs remain unchanged.
One focused renderer Fact covers enabled, disabled, missing and absent references and
ensures rendering makes no owner calls.

## Validation boundaries

Evidence root: `.artifacts/concurrent-mutations/validation/c2-repair-validation`.
The source-derived `c2-repair-focused-selections.json` selects ten disjoint lanes:
7 migration, 32 finite contract, 52 finite persistence, 83 Workspace owner, 54 historical
migration, 25 Workspace presentation, 49 definition HTTP/schema, 23 commit-knowledge,
2 separately selected LiveProcess and 3 separately selected browser cases. Total:
**325 Stable plus 5 separate cases = 330**. Counts precede discovery; a failure, skip,
identity mismatch or count mismatch cannot be treated as a pass.
Independent source audit confirms all selectors, partials, literal/enum theory rows,
categories and lack of overlap/skips/dynamic providers. Manifest SHA256:
`61144528C3CD9B611198968D64357B3F6D0E582E769132C0AB466596585FFFAD`.
The original precompile manifest is retained. Its counts and selectors are unchanged;
only the new pool-fence test's namespace import changed before the first discovery.

Ten production/host/test projects refresh their ordinary Release outputs serially.
The final complete portability scan includes untracked source, any reviewed baseline
delta and final enforcement without writing a baseline. Rendering and maintained-document
checks remain separate. The existing isolated PostgreSQL fixture is reused.
Static tool self-tests passed 6 + 4 + 7. The rendering register passes 1,197 inputs;
complete-scan enforcement passes with the same 15,465 reviewed allowances and no baseline
refresh. Maintained-document validation passes all 426 files; whitespace validation
passes. The initial Integration compile failure and its log are retained: the new
partial needed the `CanDoItAll.Infrastructure` namespace import for the existing writer
fault seam and path-policy factory. Its repaired build and the Playwright build pass.

Regenerated predecessor-to-C2 idempotent SQL has SHA256
`B69A2CAE36CA04D29D4802D4FEEE5B0C9A9295F70E0235C32BEF454A2F80E7EA`.
It is byte-for-byte equal to the previous reviewed script after substituting only the
nine Up refusal codes from 55000 to P0001. The tenth refusal is Down and is checked by
the migration Fact. Existing dependency/analyzer and EF tool-version warnings remain;
this change does not claim to resolve them.

R1c activity-publication repairs and performance optimizations are excluded. The maximum
journal allocation/latency finding remains an activation blocker. This candidate does not
close producer activation, cross-restart recovery, SPA cutover, remaining owner mutations,
agent/process freshness settings, broad Stable validation or capacity qualification.
Pending signing and previously rejected cleanup actions remain recorded blockers.

## Completed execution

The ten lanes executed 7/7 migration, 32/32 finite contract, 51/52 finite persistence,
82/83 Workspace owner, 47/54 historical migration, 25/25 Workspace presentation,
49/49 definition HTTP/schema, 23/23 commit knowledge, 2/2 LiveProcess and 3/3 browser
cases. Every discovery label, execution definition and unique execution ID matches;
there are no skipped or incomplete rows. Final source and binary identities match
the frozen discovery candidate. Source identity:
`50be02bc1853405b62667b5472c4e12900f2c434f6e995a95008791ecc1aa4fc`.
The immutable `completed-summary.json` SHA256 is
`F15BF434431C04D4AFE7688CC105A21BE71ED6FF0A17757311A5D309ED2105D8`.

The readiness failure expected MissingState after retaining an orphan epoch; the
stronger installed-set audit correctly returned InconsistentMetadata. The
[test repair](../reviews/C2-readiness-classification-repair.md) separately preserves
foreground MissingState proof. Workspace's restart fixture lacked a valid connector
configuration; its [repair](../reviews/P05-W1b-provider-fixture-restart-repair.md)
uses the existing ScenarioHarness provider contract without network access.
The seven historical failures require [metadata-aware assertions](../reviews/P04-C2-historical-project-metadata-repair.md)
across the added Project revision column and current 168-entity model. These source
repairs require a new candidate; they do not relabel the nine failures as passes.

The three browser images retained under this candidate's `browser-evidence` were
visually reviewed at 1920x1080, 1920x1080 and 1600x1000. Current/retained values display
provider names, keep draft choices explicit and show no new overlap in these viewports.
Post-run observation found no Web/Workspace sandbox processes and only the previously
policy-blocked residual database with zero clients, plus the PostgreSQL admin database.
The shared isolated fixture remains needed. This is a current observation, not a
complete historical descendant-process ledger or a cleanup-complete claim.

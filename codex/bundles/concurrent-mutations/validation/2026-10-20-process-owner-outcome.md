# Original Process child outcome reader validation

Status: checkpoint passed with 129 distinct passing behaviors across the explicit
correction boundaries below. Product producer/manager activation remains open.
The signed prerequisite is 187ef2df5.

[Architecture and risk design](../plan/P07-process-owner-outcome.md) defines the
Application contract, Workbench owner, access/accounting separation and following
runtime-result integration. Application, Workbench and AgentFramework module are the affected production
projects. No project references, migration, runtime partial or resource family changes.

## Selection and invalidation

Proof tier: Governed, using this existing bundle's plan, validation and retained JSON
shape. The exact owned candidate is .artifacts/concurrent-mutations/validation/c4-process-owner-outcome.
Shared standard Release outputs and the existing isolated PostgreSQL fixture are reused.
The crash recovery found both initial production builds complete and no running tests;
source is preserved and the final changed Workbench candidate is rebuilt.

The first Integration build stopped on CS0103 because the new test omitted the existing
HistoricalFileObservation helper's namespace. The import was added; no production logic,
assertion or selected count changed. The failed build transcript is retained, Integration
was rebuilt, and the full static scan is regenerated after the repair. No tests ran on
the failed build.

Before execution, review found that the ordinary Agent run reader could take the
workspace-wide publication lock and perform recovery writes. The initial frozen
candidate was invalidated without executing tests. The revised candidate uses the
existing bounded historical reader and its canonical profile adapter. Five publication
cases now hold the publication lock during accounting; one new case requires an explicit
recovery refusal, no file mutation and no original-source query. Source/runtime manifests
for the initial candidate remain retained separately. All selected tests run on refreshed
standard Release outputs after this production correction.

| Lane | Expected discovery | Reason |
| --- | ---: | --- |
| owner-outcome | 30 | New pure accounting, exact identity, pending and corruption cases |
| process-codec | 62 | Existing inherited proposal interpretation used by the reader |
| agent-codec | 11 | Existing ordinary proposal path through the shared pure decoder |
| registered-outcome | 14 | Actual registered reader with provider, SQL, held publication lock, recovery and access changes |
| registered-runtime | 12 | Dependent provider/saved-tool flow after the fixture/decoder changes |

Total expected: 129. Exact class filters are recorded before discovery and execution.
The contract/decoder/registration changes invalidate those owners and one dependent flow;
they do not justify an unfiltered suite. Historical full Stable failures remain recorded.

The first frozen execution completed 129 cases: 121 passed and eight registered outcome
cases failed. The reader mistook the retained dispatch lease ID for active work. The
actual lease owner releases its lock without clearing that ID. Closed publication,
definite non-commit, approval and basis waits were consequently misclassified as active
work. The source correction removes that liveness assumption; pure terminal fixtures now
retain the token, and a retained-token approval case explicitly expects approval.

The corrected selection is 30 pure owner cases plus all 14 registered outcome cases.
The 85 codec and provider/saved-tool cases remain valid: their source and production
paths are unchanged, and they do not call the corrected outcome interpreter. This is a
reviewed dependency boundary, not an unchanged-production claim. The correction changes
only the pure interpreter and its Unit test; all registered assertions remain unchanged.
The earlier failed result and source/runtime identities are retained in
[the initial proof](../proof/2026-10-19-planning-authority/c4-process-owner-outcome.json),
SHA256 `9ADDEEBA95741C4F334FB7465D94F55C045FEFA5015084D64A402E4AC1819E9B`.
Its observer covered all five commands; no observed processes or database-name deltas
remained. Thirty-one temporary entries without proven ownership were preserved.

The repaired run completed 44 cases: 39 passed and five publication cases reached an
overbroad SQL assertion. Their outcomes, exact work/receipt, held-lock read and unchanged
files passed; the assertion incorrectly forbade the bounded accepted-source projection's
own `PayloadJson` and the explicit legacy-only `CASE` branch. The test now verifies the
persisted `OriginalProjection` version, the legacy-only conditional, exactly the two
source payload references, and no payload in the material metadata query. Four bounded
SELECTs, no Workbench queries and no extra provider calls remain required. Production
is unchanged. The exact five-case method is repeated after refreshing Integration and
the full static gate. The 39 other owner cases and the 85 earlier independent cases
remain valid under their recorded boundaries.

[The repaired proof](../proof/2026-10-19-planning-authority/c4-process-owner-outcome-repaired.json)
has SHA256 `4517D7179169F6F62B78E7DA26AB8D340A01AB758C4EC48FBE26097D0430BB74`.
Its source/runtime identities were unchanged throughout execution; both commands were
observed, no processes or database-name deltas remained, and six unowned temporary
entries were preserved.

The five-case repeat reached the final payload reference count and failed all five:
the expression also counted SQL's `AS "PayloadJson"` output alias. It now counts
qualified column references only. The conditional and persisted-version assertions
already passed; production remains unchanged. The failed repeat is retained as
[proof](../proof/2026-10-19-planning-authority/c4-process-owner-outcome-verified.json),
SHA256 `777FBC9CDF3E71878B276E80E4729A9D55C8163749A752BE00FBD97D3D06EB2B`.
Its complete source/runtime identities and one-command observation passed, with zero
remaining processes/database-name deltas and five unowned temporary entries preserved.

The qualified-reference repeat also failed five cases at the count assertion. This
disproves the proposed alias explanation; a projection reference count is not the
required cost invariant. The final assertion identifies the launch table alias from
the explicit legacy-version condition, then forbids any launch-payload reference outside
that condition. It keeps the persisted version, bounded material read, four queries,
unchanged files and no extra provider assertions. The failed run is retained as
[proof](../proof/2026-10-19-planning-authority/c4-process-owner-outcome-final.json),
SHA256 `91AD209FD3C556B2947D8DFB2ABDF89D02B926BFE3B50EAA9A57F3BABB2BC2E5`.
Source/runtime identities and observation again passed, with zero remaining processes/
database-name deltas and five unowned temporary entries preserved. Production is unchanged.

The resource manifest uses the existing registered SQL/file fixture families and exact
source pins. A bounded observer must cover every test command. Freeze complete source
and refreshed runtime inputs before execution; verify the same identities afterwards.
Require full portability enforcement without baseline writes, architecture comparison,
documentation, secret scanning and observed process/database reconciliation before closure.

## Final proof

The final five cases passed in 164.841 seconds, completing 129 distinct behaviors:
85 unaffected codec/provider cases from the first run, 39 owner cases after the
production correction, and these five exact SQL assertion cases. The complete-source
comparison permits only the Integration test file and this validation record to differ
from the repaired production candidate. Only the Integration DLL/PDB differ in runtime
inputs. This is not a claim that 129 cases were executed on the final test assembly.

[Final proof](../proof/2026-10-19-planning-authority/c4-process-owner-outcome-sql.json):
SHA256 `42ECE1C471A2F7E93D9A81D62619FE9E402C905FD2918CC99A2BD81B59D27A2A`.
The 9718 source inputs and 3896 runtime inputs were unchanged throughout final execution:
source manifest `80A06570213C520AAF0F25274BCB0F2833628A811D2CC505980EF4F373276EE8`,
runtime manifest `06AADE18EEC24111F1674036C33ECC8FE4CA1E58996CFF81153436154E7489D7`.
All five retained proof documents passed secret scanning.

Direct Release builds passed for Application, Workbench and AgentFramework module;
Unit and Integration were refreshed after their respective changes. Final Integration
build/discovery passed in 55.321/2.055 seconds. Full portability scan and enforcement
passed with 15651 reviewed executable-source findings unchanged, without baseline writes.
Documentation and diff checks passed. Historical full Stable remains not green:
16199 executed, 16182 passed, 17 failed, 2444 unexecuted.

The final observer covered the complete command. No observed processes or database-name
deltas remain; five temporary entries without proven ownership were preserved.
The initial/repaired/final SQL validation directories remain needed for the next result
integration's reviewed helpers and architecture notes. Automatic approval review rejected
removal of the two intermediate directories, c4-process-owner-outcome-verified and
c4-process-owner-outcome-final, with `blocked by policy`; both remain CleanupBlocked.
No deletion occurred and no denied path was retried.

## C# Architecture Gate Result

Status: Pass for this reader checkpoint. Final snapshot snap-20261020192532-6f5b6f80
contains 1894 types, 16743 members, 609 findings and 205 existing diagnostics. Relative
to the baseline, there are no new findings, diagnostic categories, project edges or
cycles; six existing Workbench module/type cycles remain. The separate AgentFramework
module comparison also retains its 220 findings, 216 diagnostics and two existing cycles.
Existing workspace diagnostics are not a clean-solution claim.

Application owns the neutral outcome contract; Workbench owns journal interpretation;
AgentFramework registers its existing canonical historical store. No new project
reference, runtime partial, service location or MAF checkpoint dependency is introduced.
Pure interpretation is tested without the Process host; registered tests prove the
composition and held-lock/read-only boundary. No blocking architecture finding remains.
The original result/accounting integration may proceed; producer and manager activation
remain gated by their separate behavior and capacity proof.

## Remaining work and resource boundaries

This reader supplies original owner evidence; the Process producer/result-commit path
still needs to consume it. Durable capture, manager approval/retirement, Workflow,
settings/SPA, sustained capacity and bundle closure remain open. SPA stays on
codex/concurrent-mutations. The previously denied cleanup of the three atomic-completion
directories and root-sequence probe remains blocked; those paths are not retried.

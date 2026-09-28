# Corrective acceptance and execution matrix

This matrix narrows the current task; it does not erase the original behavior obligations. Test case counts below are historical/source inventory unless explicitly recorded as a new executed run by Codex. The reviewer did not execute product tests.

## A. Deterministic and real-form regressions

| ID | Arrange / act | Required assertions |
| --- | --- | --- |
| R1.1 | DelayedReadback, same active draft, commit held before read-back, change party to None, complete any remaining applicable wait | Same draft/context; newer party retained; one store commit; current Pending cleared after its completion; Saved or truthful SavedWithWarning; CanSave true |
| R1.2 | Drive R1.1 through the actual BaseLib workspace, EditForm and InputSelect | Real submit event and selector; no model-only substitute; no reset/navigation required to recover; pending handler finishes |
| R1.3 | Read-only Retry after R1.1, then a later explicit edit/save | Retry never adds a commit; parent/eligible child IDs stable; explicit save is admitted once and stores the newer party/text |
| R1.4 | First admitted write/read-back, select B, then A and start an independent successor operation; complete the old one first | Original stored result correct; old completion cannot patch/unlock/relabel the successor; its own completion settles it |
| R1.5 | Project A → B → A while an older save is pending; actual party/reference changes | Expected admission follows explicit choice; old target receipt cannot acquire current target ownership; no unknown lock silently bypassed |
| R1.6 | DelayedRead, DelayedReferences, DelayedSave and DelayedReadback across reset, scenario switch and disposal | Applicable waits settle/retire without replacement callbacks; admitted fake writes stay separate from view retirement; no hanging event tasks |
| R1.7 | Known commit followed by cancelled/failed read-back | Known commit never becomes refusal/Unknown; identity remains; read-only recovery and truthful warning if needed |
| R2.1 | Known saved party, explicit switch to global null/null plan | Saved ID unchanged; existing party resolved/named; no false unavailable placeholder; no automatic admission recapture |
| R2.2 | Known saved party absent from current project list | Bounded fallback adds that exact known party; current options remain correct; no arbitrary-ID success |
| R2.3 | Genuinely unknown saved ID, MissingReferences scenario, failed saved lookup | Preserve ID; distinguish unresolvable choice from failed request; no fabricated name or silent clear |
| R2.4 | Delayed old list/fallback followed by project/party change, including A → B → A | Stale success/error cannot overwrite current choices, field, status or a save outcome |
| R2.5 | Production session with a global saved party and controlled owner fallback | Proves the already-existing production behavior that the sandbox mirrors; no production lookup redesign |
| C1.1 | Real page/session emits Unknown from a controlled owner result | Uncertainty-specific notification title and details; locked same draft; no second write from retry/project change |
| C1.2 | Refused, Saved and SavedWithWarning notification regression | Correct existing meaning; known commit and read-only warning recovery preserved |

A real-control test should capture draft and EditContext before dispatch, use actual input/select events, and retain an unblurred changed text field while the held operation completes. Inspect notifications/state separately from the stored submission. Follow current bUnit dispatcher/event-task and disposal helpers from `docs/testing.md`.

The two [source seeds](regression-seeds/README.md) cover only R1.1 and R2.1. They do not close this matrix, use no actual browser, and were not compiled/run by the reviewer. Adapt them to actual source and supported test conventions before using them as evidence.

## B. Browser closure

**Sandbox:** extend the existing TestLab sandbox journey rather than creating a substitute page. Select DelayedReadback, submit, verify the current controlled save reached its committed wait, change the actual Responsible party selector, release any applicable wait, and observe settlement for this operation. Assert one fake commit, retained party choice and enabled submit without resetting the editor. Check a subsequent explicit save and read-only retry at deterministic lower layers. In a representative scenario, switch Project to None and verify the known saved party's label. Keep the deliberate MissingReferences placeholder case.

**Production:** run the existing three TestLab browser methods, updated only as needed for the bounded notification/fixture work. They cover aggregate create/reopen/IDs, all sections and filters/global/history/missing navigation, actual held read-back with unblurred input, and real postcommit Activity warning with no replay. Capture fresh operation completion; never use a pre-existing Saved marker as proof of a new write. Use the real shell and reviewed log assertions.

Use the supported large desktop viewport (the current tests use 1600 × 1000), capture and inspect screenshots, and check styles/scripts/fonts plus browser and owned-server errors. A single successful rerun is not the controlled R1 reproduction.

## C. Suggested impact-based execution

| Lane | Existing seed | Closure treatment |
| --- | --- | --- |
| Light UI/scenario/boundary | `tests/Components/CanDoItAll.TestLab.UI.Tests`, filter `FullyQualifiedName~CanDoItAll.Tests.Components.TestLab` | Required; original documented count 20 plus actual added rows; derive and discover current total |
| Production session | `tests/Unit/CanDoItAll.TestLab.Tests`, filter `FullyQualifiedName~TestLabSessionTests` | Required for parity and mutation safeguards; original documented count 28; include new positive fallback test if absent |
| Real page/notification and reconciliation | `tests/Components/CanDoItAll.Tests.Components`, current new notification topic plus `TestLabReconciliationTests` | Required for C1 and production host wiring; three reconciliation cases exist in reviewed source |
| Owner/postcommit/admission | `OwnerPostcommitPageTests`, `TestLabOwnerPersistenceTests`, `ResourceTestLabAdmissionIntegrationTests`, `OwnerPostcommitPersistenceTests` | Expand when production draft/session/contracts/persistence or their fixtures change; preserve old tests; do not claim untouched historical runs as fresh |
| TestLab Playwright | `tests/Playwright/CanDoItAll.Tests.Playwright`, filter `FullyQualifiedName~TestLabBrowserTests` | Required production/sandbox closure; reviewed class has three Fact methods before additions |
| Shell/Collaboration | `ConversationShellHostTests`, affected host consumers, `CollaborationBrowserTests` | No S0 rewrite. Re-run required affected lanes if touched or current drift invalidates prior proof |
| Cross-module/full Stable | Current named invalidation triggers in testing guide and CI | Not an automatic per-edit step; mandatory when an actual trigger applies |

Commands must use actual project filenames, supported configuration and current source. Build each changed production project directly, then build/discover the selected test assembly. Example pattern:

```powershell
# Set these from the actual checkout and source-derived test inventory.
dotnet build $affectedProject --configuration $configuration /m:1
if ($LASTEXITCODE -ne 0) { throw 'Affected production build failed.' }
dotnet test $testProject --configuration $configuration --list-tests --filter $filter /m:1
if ($LASTEXITCODE -ne 0) { throw 'Discovery failed.' }
# Verify the observed case count equals the recorded expectation before execution.
dotnet test $testProject --configuration $configuration --no-build --no-restore --filter $filter --logger "trx;LogFileName=$lane.trx" --results-directory $evidence /m:1
if ($LASTEXITCODE -ne 0) { throw 'The focused test lane failed.' }
```

These are patterns, not a self-running script with guessed configuration. Count theory cases and test assembly boundaries; do not invent a green run from zero discovered tests. Confirm new tests participate in their owning solution and current CI shard. Keep database-dependent production proof out of the light renderer project.

## D. Gates, resources and reporting

Use current isolated PostgreSQL 18 test setup. Record sanitized server version/endpoint and owned resource identifiers; preserve port 5032 and all ordinary data. Use matched test/child-host configurations, unset external browser base URLs for owned fixtures and clean up only task-owned resources.

Require transitive-boundary tests, backend-free sandbox startup and Parity assets. If project/build/watch inputs changed, refresh evaluated graph and watch evidence. Do not rerun or invent the original pre-extraction benchmark; preserve its limits and record a fresh narrow development-loop smoke instead.

Read and run current mandatory portability-static, review genuine versus intentional findings, inspect baseline changes and finish with enforcement without `--write-baseline`. Run relevant documentation/evidence checks and `git diff --check`.

Final receipt separates failing-first proof, corrected current executions, source-only conclusions, historical 115-case report, blocked/skipped/unrun dimensions, package checks, cleanup and signed commits. Stopping on any required failure means that dimension is not complete, not that it can be labeled green.

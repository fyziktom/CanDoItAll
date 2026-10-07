# Validation matrix

No group is pre-passed. Copy [the evidence template](templates/evidence.json) to the owned run directory. Use actual test methods/filters from current source; group counts are not test counts. Several tests may support a group, and one test may support multiple groups without becoming multiple unique executions.

| Group | Required outcome | Proof layer |
|---|---|---|
| PP1-01 | Current source/history/dependency entry and A2 preservation | source/native |
| PP1-02 | Complete renderer and caller census; four tabs and retained two-tab composition | source/component |
| PP1-03 | Evaluated graph, public signatures, forbidden/unresolved/cycle controls | build/unit |
| PP1-04 | Catalog/new/exact selection and same-target reselect preservation | unit/component |
| PP1-05 | Refresh, missing target and failed same-ID acquisition retry | unit/component |
| PP1-06 | Immediate text, raw JSON/numeric fields, tab/context lifetime | component/browser |
| PP1-07 | Independent secret metadata states, no value resolution | native/component |
| PP1-08 | Complete pricing dimensions, missing rates, row identity and private policy | component/native |
| PP1-09 | Thinking table/dialog, policy parity, Apply/Cancel and default/None | component/native |
| PP1-10 | Stale nested edits, target/kind/model/source revisions and two instances | unit/component |
| PP1-11 | Full captured Save, double admission, ID/token and later field reconciliation | native/component |
| PP1-12 | Known rejection/conflict/commit warning/unknown and exact recovery | native |
| PP1-13 | Saved-target Health, diagnostic persistence, no retry replay | native/component |
| PP1-14 | Draft-only discovery, superseded result and no implicit save | native/component |
| PP1-15 | Delete/native blockers and safe post-commit reconciliation | native/component |
| PP1-16 | Source-managed read-only displays and native constrained identifiers | native/component |
| PP1-17 | Actual retained Sharing/source connections/History identities | component/browser |
| PP1-18 | Local provider created through UI and canonical round-trip | browser/native |
| PP1-19 | A2 agent and Simple Chat use real saved provider/model data | browser/native |
| PP1-20 | Agent Project Structure file approval/write/attach/read-back/download | browser/native |
| PP1-21 | Workflow/TestLab accepted output and negative output control | browser/native |
| PP1-22 | Independent realistic sandbox including true Pricing/Thinking children | component/browser |
| PP1-23 | Standalone Production publish and served assets | build/browser |
| PP1-24 | 1920x1080 focus/scroll/footer/menus/dialogs and navigation | browser |
| PP1-25 | Before/after graph/watch and restored comparable edit probes | measurement |
| PP1-26 | Protected A2/Projects/Workspace/Resources neighboring families | native/browser |
| PP1-27 | Final portability/docs/secret-safe evidence gates and broad decision | static |
| PP1-28 | Final source/binary/attempt correspondence, resource cleanup and roadmap | closure |

## Current test anchors to inspect and preserve

`ProviderEditorOperationsTests`, `ProviderProfilesSessionTests`, `ProviderProfilesSeamTests`, `ProviderSharedReconciliationTests`, existing ProviderModelPricingEditor/ProviderModelThinkingEditor tests, registry mutation/concurrency/reconciliation tests and current API/provider selector suites. The review read only the anchor ranges identified in the source register, not an executed whole-test discovery. Resolve actual files, namespaces and current row counts before running. [R24, R25]

A2 preservation anchors: native `AgentEditorVerificationTests`, actual editor whole-draft/template round-trips, Memory invocation/context consumers, StorageSelection boundary/selection tests and existing file-approval runtime browser fixtures. Do not duplicate long application harnesses merely to name PP1 in a new test. Add assertions to reusable journeys when that preserves clear ownership/provenance. [R01, R04, R05]

New regression controls should fail for the old erroneous transition, not because setup skipped initial readiness. Use deterministic held operations and explicit observer completion. Test current failure as well as stale failure, true absence as well as unavailability, and valid action as well as restriction. A fake that echoes a submitted model does not prove native normalization, concurrency, source restrictions or durable commit.

## Commands and interpretation

Follow current `docs/testing.md`: direct product build, exact owning filter, build-backed discovery with expected count, then execution; use `--no-build --no-restore` only with matching rebuilt assemblies. Keep discovery expansions and blocked prerequisites explicit. Tests use task-owned PostgreSQL 18, not an ordinary local database. Keep per-attempt binary/source/asset and result hashes. [R26]

Only substantive green results can close a group. Unsupported external fixture is BLOCKED, historical evidence is historical, a gate-off pass is NOT_RUN. Do not require a new paid provider call just to color the report green. A clearly bounded PP1 closure can carry a separately stated unavailable external integration proof, but must not claim all integrations or application release readiness.

No whole Stable loop after every stage. Record named invalidation triggers and final broad/no-broad justification. Full portability-static, correct native owner evidence and actual desktop/publish proof are not waived by that decision. A changed public runtime contract or shared host protocol invalidates more than the leaf tests and must be tested accordingly.

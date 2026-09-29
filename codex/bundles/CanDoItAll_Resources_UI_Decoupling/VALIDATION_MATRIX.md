# Validation matrix — outcomes, not a fixed test quota

Apply the current repository test/CI rules first. Each row is an obligation to map to
actual current tests and observable evidence, not a required new test method or project.
Derive expected discovery from source before running; verify it in a fresh owning assembly.
Explain theory expansion separately. Zero discovery, skipped prerequisites and quarantined
or failing cases are not green proof. [EV05, EV06, EV07]

## S0: bounded Memory carry-over

| ID | Required behavior | Minimum proof |
| --- | --- | --- |
| V-ME-01 | A query already visible at A/R0 is not represented as current A/R1 after refresh | Failing-first controller test and actual Query/result panel assertion |
| V-ME-02 | Held R0 query discovers R1 only in its own automatic read-back | Controlled owner completion ordering; original receipt/operation/context retained |
| V-ME-03 | Removal/reappearance and same-revision refresh have distinct semantics | Missing is not another provider; unchanged revision keeps valid presentation |
| V-ME-04 | Stale read cannot erase newer result; raw inputs/manual feedback survive | A-B-A, delayed success/error, explicit versus auto feedback origin; no extra dispatch |
| V-ME-05 | Real owner revision signal and shipped UI agree | Real snapshot/profile update plus representative browser journey; historical vs current visible |
| V-ME-06 | No regression to Scheduler fixes or unsupported Memory features | Focused source/impact check and affected existing tests; no new runtime capability |

## Complete boundary and production wiring

| ID | Required behavior | Minimum proof |
| --- | --- | --- |
| V-BD-01 | Entire Registry and Browse tree mapped, with nested config/preview/dialog dependencies | Current source/DI/assets/consumer inventory and before baseline |
| V-BD-02 | New UI/sandbox closure is light in actual source replacement mode | Evaluated transitive projects/packages/native/runtime assets, no unresolved/cyclic edges; negative boundary tests |
| V-BD-03 | Both production and sandbox use all actual extracted renderers | Real child traversal plus route/component/browser interactions, not placeholder slots |
| V-BD-04 | Contracts preserve used serialization/enums/defaults/identity and project admission | Consumer compile, actual contract/API tests as applicable; no entity/authority/service leak |
| V-BD-05 | Config field dependency resolved without duplication/heavy Workspace reference | Direct affected Workspace caller tests if shared field moved; real raw numeric/JSON/secret-reference controls |
| V-BD-06 | Route and Agent context remain exact | resource-only historical, project-only create, resource+project mismatch/missing/recreated lifetime; registry/browse navigation fence |
| V-BD-07 | Real registration and test inclusion complete | Direct module/Composition/Web build, route discovery; product excludes tests; actual Components/Stable/CI lists include new light project |

## Registry state, forms and results

| ID | Required behavior | Minimum proof |
| --- | --- | --- |
| V-RG-01 | Pre-blur text/config/validation/EditContext survive read and tab changes | Real input events, incomplete numbers, structured/JSON fields and same form context |
| V-RG-02 | Submission captured before all owner awaits; later edits survive | Held Save, all fields/config/project admission; edit-away/back, parent ID accepted before read-back |
| V-RG-03 | Duplicate/conflicting Save/Delete rejected in handler; independent target can progress | Direct second dispatch, actual submit/footer, pending operation counters |
| V-RG-04 | Reads cannot reset successor editor or block correct completion | Select A-B-A, Reset, project/connector change, party lookup, stale error/finally/disposal |
| V-RG-05 | Known rejection is distinguishable from committed warning and unknown | Existing Resource postcommit tests plus held lost-acknowledgement case; exact target recovery |
| V-RG-06 | Known ID survives failed/missing read-back; later save cannot create duplicate | Actual owner row count/ID; read retry has zero writes; do not adopt absent row as create |
| V-RG-07 | Missing references and schema mismatch never silently select another target | Project/party/secret/connector unavailable cases; explicit change policy; preserve stored metadata |
| V-RG-08 | Filtered-empty differs from empty; history/current project filtering is correct | Real filters with current/recreated/retired project binding and selected editor retention |
| V-RG-09 | Governed storage resources remain read-only outside promotion | Actual normal-save refusal and renderer no source/locator edit; metadata cleanup behavior |

## Browse, promotion and file effects

| ID | Required behavior | Minimum proof |
| --- | --- | --- |
| V-BR-01 | Real source list, FileBrowser, search/paging and all classes render | Shared component session with deterministic bounded providers; source/policy states |
| V-BR-02 | Held catalog refresh cannot restore old selection | A-refresh-B, A-B-A, empty/repopulate, stale error; current Agent context |
| V-BR-03 | Stale source/preview acquisitions are released without touching successors | Noncooperative completion, replacement/disposal, exact lease/handle counters |
| V-BR-04 | Promotion submits original item/source/project/name/sensitivity | Real dialog controls and captured admission; newer selection/dialog cannot retarget command |
| V-BR-05 | Accepted promotion does not steal selection/reopen old dialog | Parent callback held/failing, source switch, same-ID reentry; original result remains reviewable |
| V-BR-06 | Promotion callback/cleanup failure does not erase known ID or mask primary outcome | Owner and dialog fault injection before/after Reset; no second null-state exception |
| V-BR-07 | Governed reopen reads actual content with current access and releases correctly | Production owner/temp file + FileInteraction; changed source, revoked access, retired view |
| V-BR-08 | Activation policy remains unchanged | Supported internal file vs unsupported pointer activation; keyboard promotion; denied local launch |
| V-BR-09 | Download/preferred-app/folder effects use exact authorized origin | Safe bounded effect seam; no launch from raw label/path; stale queued UI result suppressed |
| V-BR-10 | Search/content/state budgets are real | Page50, source512, search limits, preview16MiB, no unbounded content retention; repeated navigation disposal |

## Actual owner semantics and security boundaries

| ID | Required behavior | Minimum proof |
| --- | --- | --- |
| V-OW-01 | Registry save/update/delete on actual PostgreSQL owner | Exact ResourceId and metadata/config read-back; project lifetime/profile recorded |
| V-OW-02 | Project retirement/recreation is refused or cleaned per existing policy | Current admission, stale admission, historical resource-only, combined-route mismatch and exact cleanup |
| V-OW-03 | Save/Delete postcommit Search/Activity/logger faults retain confirmed facts | Existing postcommit tests, real persistence and faulted diagnostic sink; no automatic replay |
| V-OW-04 | Promotion durable result survives revision/log/cleanup faults | Writer commit then failing PublishScopeChanged/revision/log/revoke; exact ID/Created, revision unavailable not invented |
| V-OW-05 | Unknown durable acknowledgement is not converted into known commit | Fault at real commit acknowledgement, explicit exact observation, no name-based inference |
| V-OW-06 | Stable occurrence deduplication and authority cleanup are preserved | Same config + same project lifetime dedupe, different lifetime isolated; actual handle revoke evidence |
| V-OW-07 | File/source/actor/profile negatives remain fail-closed | Changed fingerprint, wrong source/item/scope, revoked access, denied project; no secret/opaque grant persistence |
| V-OW-08 | Related consumers retain semantics | Current Resource Memory snapshot, Workbench projection/enlisted reads, connector and HTTP consumers selected by impact |

## Browser, assets, development loop and closure

| ID | Required behavior | Minimum proof |
| --- | --- | --- |
| V-UI-01 | Actual Web Registry journey through real owners | Create/edit/read-back/delete, later unblurred typing, explicit current project/reference state |
| V-UI-02 | Actual Web Browse journey | Harmless task-owned file/source, promotion dialog, saved ID, authorized reopen/preview and back navigation |
| V-UI-03 | Faithful standalone sandbox | Both tabs, real file components, source/selection/error/unknown/partial/disposal scenarios and original fake-store reads |
| V-UI-04 | Source and publish asset closures are correct | Network responses, real font/scoped CSS/JS callbacks; standalone published host without DB |
| V-UI-05 | Desktop interaction is usable | Inspected 1600x1000 or current supported desktop viewport, focus/caret, list/detail scroll ownership and dialog stacking |
| V-UI-06 | Edit-loop evidence is measured, not predicted | Before/after Web/sandbox graph/watch, actual Razor/C#/CSS and applicable JS edits, samples and restoration |
| V-CL-01 | Fresh build-backed discovery and correct test membership | All changed production roots plus affected tests; exact filters/counts/status and widening rationale |
| V-CL-02 | Full portability closure | Proposed-tree and final enforced scan, reviewed intentional baseline delta, no-write enforcement pass |
| V-CL-03 | Documentation/evidence/secret gates and cleanup | Current canonical commands, owned processes/files/PG/handles cleaned, no ordinary app changes |
| V-CL-04 | Deliver S0 and complete Resources with honest limits | Requirement mapping, signed commit(s), remaining blockers distinguished; no third-module changes |

## Practical execution lanes

Start S0 in `tests/Components/CanDoItAll.Memory.UI.Tests` and the actual affected owner/page/
browser test projects. Derive current names/counts; the previous 184/76 receipt is historical.

For Resources, introduce a focused lightweight UI test project only if the extracted seam
needs one. Relevant existing owning projects are Components, Integration and Playwright.
Start with `OwnerPostcommitPageTests` (Resource data rows), `ResourceFileBrowsePaneTests`,
`ResourcesPageAgentChatContextTests` and `ResourceStorageObjectIntegrationTests`. These are
source-identified starting points, not a complete current selection. Discover other
Resource admission, connector, projection, navigation and FileTools consumers locally.

For every selection record production roots built, owning test project, exact filter,
expected discovery, actual discovery, expanded execution count, passed/failed/skipped and
artifact path. Keep red/failed attempts distinct. Wait on concrete operation/result identity,
not arbitrary sleeps, button disabled or stale generic Ready markers. Locate and dispatch
bUnit events on the renderer and await event tasks using current repository helpers.

Real PostgreSQL tests use the existing isolated database leasing contract and current
`CANDOITALL_TESTS_POSTGRES_CONNECTION` prerequisite. Do not probe, migrate, stop or reuse
the ordinary app/database. Browser child-host configuration must match the compiled
configuration. An unavailable provider account is not permission to fake a live-provider
pass; prove the relevant safe seam and state the boundary.

Full Stable/platform expansion follows the current named invalidation rules. Shared config
field, FileTools contract, root build policy, global fixture, owner/transaction or serialized
API changes can widen required proof. An optional N/A requires a concrete scope reason;
missing required proof remains blocked, not waived by this table.

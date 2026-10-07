# Acceptance and validation matrix

Source review supplies candidate reproductions, not failing-first results. All expected counts
must be established from current facts/theory rows and confirmed by build-backed discovery.
Refresh owning assemblies before no-build execution. Record exact filters, source/configuration,
expected/discovered/executed/pass/fail/skip counts and redacted evidence paths. No quota of tests.

## S0: API correction

| ID | Required proof |
|---|---|
| V-AP-01 | Current account-page Unauthorized completion then page/session retirement cannot cancel a disposed CTS; repeated Dispose is safe |
| V-AP-02 | Same ordering in token list and bounded page correction; external authority retirement with noncooperative read finishing before disposal |
| V-AP-03 | Old finally cannot detach/reset a newer pending read; A→B→A success/error/denial/finally protections remain |
| V-AP-04 | Current account-list denial retires shared management authority, clears synthetic disclosure and any owned password editor, and shows denied state |
| V-AP-05 | Current token-list denial retires both children; ordinary dialog close does not retire parent authority |
| V-AP-06 | Stale/closed-child/prior-activation denial cannot retire authorized successors; existing ApiSessionTests/ApiPageTests remain |
| V-AP-07 | Actual ApiAccessSurface and production Settings host: denied list, explicit retry, tab-away/back, disposal; no circuit/browser error or redisclosure |
| V-AP-08 | Safe admitted receipts persist; no query/issue/revoke replay; existing owner/secured HTTP consumers unchanged or rerun when affected |

## Boundary and catalog state

| ID | Required proof |
|---|---|
| V-BD-01 | Before/after source and consumer inventory; original real wizard baseline and protected Core/API graphs |
| V-BD-02 | Dedicated leaf renders full wizard with actual BaseLib children; negative forbidden/unresolved/cycle/public-type tests |
| V-BD-03 | Evaluated source-mode/package graph excludes runtime; no Core/API/Foundation reverse edge or broad guard relaxation |
| V-BD-04 | Settings active-slot and Recovery host callback preserve route/lazy behavior; real Recovery/pickers remain out of sandbox |
| V-ST-01 | Immediate pre-blur fields, incomplete numeric text, provider-specific values and validation survive reference refresh/step changes |
| V-ST-02 | Exact editor load is separate from list readiness: missing, failed/retry same ID, A→B→A, initial failure, New and disposal |
| V-ST-03 | Captured deep Save/Test/Delete including DefaultPurposes, secret ID and original profile; no mutable request read after await |
| V-ST-04 | Concurrent same-target mutations blocked in handlers; shared routing-purpose conflicts handled within the view; independent safe reads not blocked unnecessarily |
| V-ST-05 | Known identity accepted before secondary reads; newer fields/target/step/EditContext retained; deleted selected row not recreated accidentally |
| V-ST-06 | Missing/disabled secret or storage retained as exact reference; no first-item fallback; unknown provider/enum values explicit |
| V-ST-07 | Catalog/ref errors labelled stale/unavailable, not successful empty; partial reference failure does not erase acquired drafts |

## Owner, health and general behavior

| ID | Required proof |
|---|---|
| V-OW-01 | Actual catalog create/update/delete returns/verifies exact durable ID and row counts; missing target at write boundary does not create through editor path |
| V-OW-02 | Catalog acknowledged then routing fails before/during its per-purpose sequence: committed ID retained and routing completion not fabricated |
| V-OW-03 | Routing complete then Activity/log/read-back fails: exact committed/secondary result, refresh never repeats commands |
| V-OW-04 | Unknown actual write acknowledgement stays unknown and blocked; observation is not proof of original causality |
| V-OW-05 | Unsaved Test does not create a catalog row; existing Test retains documented draft/configuration persistence semantics; real driver effect counted |
| V-OW-06 | Test held across edits/provider/selection/New/disposal: original target/profile only; health badge tied to tested snapshot |
| V-OW-07 | Driver outcome, health persistence, Activity and read-back failures are distinguished; no automatic retest or credential leakage |
| V-OW-08 | Bootstrap/system-managed protection, original configuration bytes, omitted configuration, malformed unrelated rows, routing priorities/alternatives preserved |
| V-OW-09 | Credential resolution uses actual StorageCredential consumer policy; denied/missing credentials do not dispatch driver effects |
| V-OW-10 | Original canonical profile/factory and existing fences survive switch while work is held; no cross-profile later stage |
| V-OW-11 | Complete APPLICATION_REGRESSION_MATRIX.md with actual owning tests and justified optional environment lanes |

## Sandbox, browser and closure

| ID | Required proof |
|---|---|
| V-UI-01 | Representative/empty/large/error/partial/system-default/reference-missing scenarios use actual renderers and mutable bounded store |
| V-UI-02 | Controlled read/write/test/postcommit holds, multiple targets, routing partial failure and reset affect original store only |
| V-UI-03 | Production browser creates, edits and removes task-owned catalog, applies/unselects one purpose and tests a harmless private filesystem draft |
| V-UI-04 | Source and published Production sandbox runs without DB/vault/drivers; actual Steps/focus/footer/overlays/assets inspected at current supported desktop viewport |
| V-UI-05 | Recovery host callback preserves exact ID/null without executing recovery; deferred feature clearly labelled in sandbox |
| V-UI-06 | Measured graph/watch/edit loop, source restoration, no invented speedup and no automatic network/DB work on typing |
| V-CL-01 | Every changed production root built, current discovery/execution recorded; actual CI/test-solution membership updated |
| V-CL-02 | Current portability-static findings reviewed and final enforcement without write-baseline passes |
| V-CL-03 | Maintained docs updated including honest Workspace remaining-family status; secret/evidence gates and artifact checks pass |
| V-CL-04 | Broad Stable trigger decision recorded and honored; raw earlier failures not overwritten; exact owned cleanup/signing/worktree/siblings reported |

## Starting test inventory (not a prescribed execution filter)

API: current Workspace.ApiAccess.UI.Tests plus ApiIssuanceCaptureTests,
ApiTokenAdministrationTests, ApiUserAdministrationPanelTests, WorkspaceApiStatusTests;
relevant ApiAccess* / ApiSession* / ApiAdministration* owning tests from the current tree.
Core carry-over: WorkspaceFileIdentityTests and ResourceEditorReadinessTests.
Storage: StorageCatalogContractPersistenceTests (four methods read in ST06), current routing,
bootstrap/host-binding, secret-consumer, picker, Resources and attachment owner tests.
Discover exact paths/counts with available CodeAnalytics or local source before execution.
Do not run a blind `~Storage` union or silently drop unavailable cases to get a green count.

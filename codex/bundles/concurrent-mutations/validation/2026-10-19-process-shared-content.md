# Shared Process material content

Gate: **Pass with follow-up**, 2026-10-19. All 265 discovered cases passed on one
frozen candidate. This completes the mixed-executor content contract prerequisite;
parent accounting, shared active scope and governed runtime activation remain open.

## Behavior and boundaries

`SharedContentV1` binds a business content schema independently of an assignment's
exact Agent/Workflow execution contract. Both executors can consume one retained run
payload without impersonating a capture executor. Runtime owns the neutral typed
schema and immutable value; the business owner still interprets its payload. Dispatch
capability and source/manager authorization remain separate requirements.

The legacy `CanonicalSnapshotV1` framing and old admission bytes remain unchanged.
Shared content uses a distinct fingerprint domain. Its required content schema and
absent executor are checked in admission, scope storage and material reconstruction.
Unknown protocols, null/extra content fields, changed schema identity/version and
cross-protocol reinterpretation are refused. Original time and bytes remain immutable.

Migration `20261019211948_AddProcessSharedMaterialContracts` labels existing rows as
the original protocol without changing their meaning. Only shared rows permit a null
executor. Downgrade refuses shared scopes and original prepared/committed admissions,
including before capture. Exact reads and existing root transactions are reused; no
provider callback, new transaction, project reference or runtime partial was added.

## Validation

| Lane | Discovered/passed | Seconds |
| --- | ---: | ---: |
| Material persistence and compatibility | 49/49 | 173.9325484 |
| Existing budget, generation and publication | 85/85 | 296.260143 |
| Pure material, protocol and occurrence contracts | 121/121 | 3.3567838 |
| Prepared admission immutability | 10/10 | 45.1265269 |

Proof includes independent golden fingerprints, mixed Agent/Workflow sibling reuse of
one material, actual restart with both formats, migration round trips, downgrade refusal,
SQL shape constraints and refusal to reinterpret a retained legacy scope even after an
out-of-band assignment substitution.

Direct standard Release builds passed for Runtime, Application, Persistence, Process
module, Migrations and Web, plus Integration and Unit. EF reports no pending model
changes. Full final portability enforcement passes with **15,631** reviewed entries.
The four new entries are generated migration column metadata for existing
RootPathState/RootPathSyntax and RaisesEscalation/EscalationCount properties; there are
no stale entries or new executable host-path/process assumptions. Rendering,
documentation, diff and validation-tool checks passed. Existing advisory/tool-version
warnings remain.

CodeAnalytics `snap-20261019212802-d1ce2c09` has four projects, 1,023 types, 9,133 members,
29 entities, 332 findings and zero cycles. Against `snap-20261019205459-d1ce2c09`, no
finding was added or removed; four informational member counts reflect the explicit
content identity. The existing 190 load diagnostics and one collector ambiguity remain.

## Evidence and resources

Candidate: `c4-process-shared-content`. All 9,481 source inputs and 6,065 runtime inputs
were verified unchanged before releasing the freeze.

- Source SHA256: `09B24B578060E39CCAEBC68B551A8E35AA7FCD2BBB2A4972C6988E6F3D354454`.
- Runtime SHA256: `2BD26B0A920E116F9521E106B718C57FE44B7B95F2F66328882D55C1BFE13DF2`.
- Local proof `proof/2026-10-19-planning-authority/c4-process-shared-content.json`:
  SHA256 `EC7B1FCACA69ED65B7643813A265A9C5F2112FE08591298C2CF54BE6F8CD613B`.

The resource observer covered every complete test command over 536.0032092 seconds.
An initial parent check used the wrong startup filename; the actual V2 startup record
and running wrapper preceded the first test, and final interval verification confirms
coverage. No observed process or database-name delta remains. Fourteen unattributed
TEMP entries were preserved. The same PostgreSQL18 fixture remains needed; no stack or
resource family was added. Earlier cleanup denials remain unchanged.

This proof does not qualify workload capacity, enable Process/Workflow settings or
resolve earlier broad Stable failures. SPA remains on its separate branch.

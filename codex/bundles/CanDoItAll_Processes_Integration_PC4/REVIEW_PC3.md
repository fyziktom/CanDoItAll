# Source review of PC3

## Provenance and limits

Repository: `fyziktom/CanDoItAll`, branch `components-decoupling`.
Reviewed HEAD: `a13c1add605d1ceec43dedc0513acb7ac5959f96`; predecessor reviewed for comparison: `095c418b7981511ba9c96a75b7d2950ff4f326a6`.
The head timestamp is 2026-10-10 00:03:35 UTC, or **2026-10-09 20:03:35 America/Curacao**.
GitHub reports a valid PGP signature for the head. The comparison reports ten commits ahead
and zero behind. This is provenance, not an execution pin or independent local signature check.

The reviewer read the critical store/workspace/adapters, executable resolver, HTTP and
Workbench producers, observation reconciliation, native tests, selected browser code and
maintained reports listed in [SOURCES.json](SOURCES.json). Read ranges and report-only
claims are distinguished there. This is not an exhaustive semantic review of every changed
line, every migration or all active renderers. No product code was built or executed here.
The available environment did not provide a .NET runtime/build tool or reachable repository
network checkout, and CodeAnalytics plugin discovery found no available integration.

## Retain these improvements

- `EfProcessAuthoringStore` commits head, publication and receipt together with revision and
  owner checks; the native workspace coordinates the five editor families. Short-lived
  production scopes no longer reset accepted edits into separate in-memory dictionaries.
- The content envelope restores resolved execution guidance, preserves native process
  documents and scopes observations by profile, project and project lifetime.
- Publication pins executable dependencies; plans and preparations retain their selected
  content. The read reconciler distinguishes a known operation receipt from a newer
  authoritative revision instead of pinning the editor to old data indefinitely.
- Native tests use independent scopes and real PostgreSQL. Browser code includes actual
  controls, independent contexts, retained preparations and a new owned application PID.
  The reviewed restart test verifies exact workflow launch from publication v1 after v2.
- Existing UI libraries and sandbox boundaries remain the design to preserve. No evidence
  from this review justifies repeating the eight renderer extractions.

Sources: S02-S08, S13-S16, S20-S24, S30-S32, S36.

## F01 — HTTP launch preparation is not publication-coherent (high)

**Source-established missing integration, not an executed reproduction.**
`ProcessesApi.MapLaunchRequestAsync` performs retained-replay lookup first (correct), then
calls `BuildProjectScopedLaunchVariablesAsync` for project+node requests. That helper
calls `CreateVariables` without the executable closure. `ApplyLaunchVariablePreparation`
therefore uses a context without `DriverActivationsResolved`; the preparer loads distributed
**template** activations. The returned native request does not carry `ResolvedDefinitions`,
so compilation later resolves the authored publication independently.

In contrast, Workbench `StartAsync` resolves once before enrichment and carries the closure
into the launch request. A published definition with removed or changed drivers can therefore
get different producer variables through HTTP, and independent resolution allows a publication
change between enrichment and compilation. Fix the entire route, not only the preparer's
unit test. Preserve the early replay path and original producer fingerprint. Sources S17, S26-S27, S29 and S31. See [producer consistency](PRODUCER_CONSISTENCY.md).

## F02 — predecessor project-run lookup can become an unsafe absence (high)

**The filter is source-established; actual duplicate launch is a hypothesis requiring the
complete recovery-path test.** `MatchesCurrentScopeAsync` requires a captured closure when
a project run has no optional admission. `FindExistingLaunchAsync` discards a candidate when
that predicate returns false. A pre-PC3 project run can have neither field; the predecessor
lookup did not use this new condition. Current native compatibility tests create new runs
with a closure and do not exercise the exact predecessor payload.

Workbench subprocess recovery uses `FindExistingLaunchAsync(...) ?? LaunchAsync(...)`.
Other bridge/retained-preparation checks may intervene; do not claim a reproduced duplicate.
Prove the whole active/completed/stopped child path. A candidate with unproven historical
origin is not interchangeable with no candidate. Do not repair this by accepting public
ProjectId or caller variables as new lifetime authority. Sources S08, S17, S25-S28 and S37.
See [legacy recovery](LEGACY_RECOVERY.md).

## F03 — deleted role resource collides with reimport (medium/high)

**Deterministic source path under a resource-backed fixture; no runtime reproduction here.**
`ProcessRoleAuthoringAdapter` deletes a usage but does not remove its keyed entry from the
current head's `RoleResources`. Import allocation checks existing RoleUsages, then inserts
with `resources.Add(newRoleKey, resource)`. After an unreferenced resource-backed role is
deleted, its key can be allocated again while the resource dictionary still contains it.
The duplicate insertion then throws before commit. This can affect ImportRole or ImportProcess;
AddRole reuse also needs checking for unintended stale resource defaults.

Use a fixture whose source actually contains the mapped resource; a count-only import test
or resource-free first role does not exercise this path. Repair only owned head state; keep
immutable publication and historical receipt copies unchanged. Sources S09-S11 and S22-S23.
See [role resources](ROLE_RESOURCE_LIFECYCLE.md).

## F04 — producer input contract and XML descriptions need reconciliation (medium)

The API XML still describes an unknown explicit DefinitionId falling back to a default,
while current launch resolution refuses it, deliberately and correctly. It also promises
case-insensitive trimmed keys; new canonical owner comparisons are exact. Verify supported
wire normalization through actual HTTP and native producers and reconcile it at the input
boundary. Do not case-fold stored owner keys or restore unknown-ID fallback. Treat this as
part of F01, not a new universal identity refactor. Sources S08, S17, S27 and S29.

## Existing bounded residuals, not new regression claims

The PC3 report explicitly states that retained authoring facts refuse unsupported generic
project export/transfer/target restore, including global facts; guards prevent silent loss.
A new portable authoring history format is separate work. Do not delete guards here.
Catalog reads project/global metadata before in-memory paging; response Take does not bound
materialized row count. Preserve the documented limitation without pretending it is solved.
The installed shared map describes an earlier PC1/PC2 state; leave shared stable and put
current status in maintained product records instead. Sources S02-S03, S18-S19.

## Test report: recorded evidence, not reviewer execution

The maintained PC3 report preserves the original frozen Stable result: 17,328 executed,
17,308 passed and 20 failed. It subsequently describes a composite closure of 17,741 distinct
passing identities (15,081 unaffected retained, 2,247 affected replacements and 413 additional).
These sets must not be called a second full Stable PASS or added to overlapping lane totals.
The reviewer did not inspect local raw traces/TRX files or independently rerun those tests.

The report names a FileTools source commit different from the checked CI pin. That is a
reproducibility gap to verify, not evidence that the pin fails. Head check-runs and workflow
runs both returned zero in this review. Current CI push targets main/development, not this
branch. Do not describe missing CI as a failed CI job. Sources S02, S35, S38-S39.

## Next decision

Execute **PC4 integration repairs and closure**. Retain the PC3 architecture and existing
migration; do not pivot to a new unrelated UI module to avoid closing the identified paths.
A complete source-backed residual census may conclude the structural UI work is closed
while transfer support and other independent product work remain explicitly separate.

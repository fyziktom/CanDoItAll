# Historical planning receipts

The [historical plan](../plan/P04-planning-historical-receipts.md) is implemented and its
selected proof passes. The existing read-only service accepts validated effect-only
planning journals, passes their verified immutable decision to the Workbench reader,
and uses the original operation/caller with the existing SQL receipt owner. Original
Agent/source-project checks use the existing sealed historical catalog basis. No new
project reference, coordination lock, provider call or mutation authority was added.

`c4-planning-historical-validation` passes **75/75**: 25 Unit and 50 Integration,
including eight new registered historical cases, all 13 publication cases and 29
existing effect-journal cases. The new cases cover completed and unknown commits,
held workspace coordination with no file writes, read revocation, deleted/recreated
source lifetime, owner outage, original/refreshed generation identities, and a catalog
change during owner lookup. Original run bytes remain unchanged. Historical receipt
observation does not settle an open work anchor or permit another mutation.

Direct standard Release Core, Workbench and Composition builds passed, followed by
Unit/Integration builds and confirmed 25/50 discovery. Full portability enforcement
passes without baseline changes (15,610 findings); rendering, documentation and diff
checks also pass. The runtime execution uses the explicitly isolated PostgreSQL 18
fixture, not the ordinary application database.

CodeAnalytics `snap-20261019071949-dcc08a62` covers eight projects, 1,067 documents,
3,531 types and 17 scoped project-reference edges. No scoped project cycle exists;
the existing module cycle and ten type cycles remain, with no new historical adapter
in them. Its 346 diagnostics include 189 existing package-advisory errors, 126 generated
type-ambiguity warnings and 31 DI-inference notes/warnings. The actual per-call factory
passes the registered integration tests. The full snapshot response exceeded the IPC
limit; successful completion was confirmed by recent/inventory/dependency queries and
bounded review of canonical local JSON. This is not a clean global-health claim.

Source identity: `408EBB65E69C9B9D912DC9F41D774FCF45A1BFACC9F551A9F19B354E242145C8`
(9,217 files). Runtime identity:
`239A08E698CA2C496C440ADE466B08B9D1036EB11C78E5B70B185E0316FFADA2`
(6,436 files). Final verification found both unchanged. The observer covered both full
commands, and reconciliation found zero observed processes and zero database-name delta.
Six unowned temporary paths were preserved. The freeze is released for product admission
and coordinator integration.

The [retained proof](../proof/2026-10-19-planning-authority/c4-planning-historical-validation.json)
contains the commands, all results, architecture and identity/resource evidence.
Actual product admission/actions, process/workflow policies, remaining owner perimeters,
capacity and final broad validation remain required before whole-bundle completion.

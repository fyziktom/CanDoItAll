# Asynchronous original child binding

Status: all 192 selected cases pass. The exact-child admission can now await the
business owner's original target/context checks without moving them ahead of retained
lookup. Concrete Process source selection and Workbench publication remain in progress.

## Change and boundary

The binding callback receives the original candidate, admitted source session and
cancellation token. It runs only inside new-child admission while the existing exact
child lease is held. The workspace and coordinator pass the asynchronous callback
through; neither retained initial runs nor retained results create a new binding.

After the callback, admission checks cancellation, validates the complete schema-7
journal and rechecks current source authority against the original session before
initial save. Failure preserves the original exception and releases the child lease.
No synchronous blocking, second admission store, replacement grant or new lock was added.

The seven new cases cover suspension before save with the original candidate and
metadata, another child making progress, same-child reuse, cancellation both honored
and ignored by the callback, callback failure, and changes to read permission, dispatch
permission and source fingerprint during the await. The prior 185 shared/inherited
workspace, journal, coordinator, MAF and architecture cases all run again.

Scoped CodeAnalytics snapshots snap-20261020022908-aab4b286 and
snap-20261020030737-aab4b286 show one new immutable context record and three source
members. Five projects, 63 registrations, 680 findings, 27 open questions and 216
loader diagnostics remain unchanged. There are no new cycles, project references,
runtime partials or service locators. This evidence is scoped, not a clean-architecture
claim for the existing whole repository.

## Validation and evidence

Serialized standard Release builds pass for Core, Modules.AgentFramework, Integration
and Unit. Build-backed discovery confirms 132 Integration and 60 Unit cases; all pass.
The integration assembly was rebuilt after correcting a test's attempt to interpret
planning metadata before its journal existed. No test run or production change was
bridged. Full portability-static passes with 15,640 unchanged findings, no baseline
edit and final enforcement without a write flag. Rendering, documentation and diff
checks pass.

Frozen source: 9,563 files, SHA256
E5E78D973A3F3769EAE4DDA429A7C8490A5F14D9E774D2F29CCFCAAE3684714A.
Frozen runtime: 3,680 files, SHA256
AD42B9D874FA933350F8144A88E18902507196656517AE35979A25A243D9FBDA.
[Retained proof](../proof/2026-10-19-planning-authority/c4-asynchronous-child-binding.json)
SHA256 E12463C3744E009DBA43AC7FF6A2CB4F6B5C592576FF967981DC48815A365DC6.

The owned c4-asynchronous-child-binding root reuses the isolated PostgreSQL 18 fixture
on 53619, 119 existing resource families and standard outputs. The 386.8925493-second
observer covers both complete test commands, lasting 358.3632135 and 9.4477599 seconds.
Source/runtime identity verifies unchanged. Reconciliation finds no surviving observed
process or database-name delta; twenty unattributed TEMP entries are preserved. The
freeze is released. Historical cleanup denials and the failing broad Stable gate remain
open; the shared fixture is still required for subsequent integration.

# Process publication claim identity and expiry

Status: all 106 selected PostgreSQL cases pass. This closes the active-claim check in
the existing publication fence; producer activation and inherited Workbench publication
remain governed by the broader P07 integration plan.

## Change and risk

The previous fence accepted any live claim status for a running step with a non-null
active token. It did not establish that the active token matched that claim's attempt,
or that the lease remained unexpired. The exact child query now projects the expiry of
the matching run, step, token, attempt and dispatchable status. The nullable projection
uses the existing RunId/ClaimToken primary key within the same bounded SQL read.

The fence uses the existing injected TimeProvider after acquiring the root transaction
lock and reading current state. Dispatch requires expiry strictly after that observation.
The original historical child/grant result and the manager-review requirement for no
active token or live claim remain unchanged. No schema, public interface, project
reference, additional runtime partial, cache or lock was added.

Scoped CodeAnalytics snapshots snap-20261020024117-809c058b and
snap-20261020024615-809c058b retain five projects, 2,204 types, 107 registrations and 205
loader diagnostics. There are no new cycles. Findings increase from 742 to 743: the
existing internal observation record has nine source members after adding the expiry.
That informational DTO finding is reviewed; the record owns no orchestration.

## Validation

| Selection | Cases |
| --- | ---: |
| ProcessDecisionPublicationFenceTests |44|
| ProcessDecisionChildMaterialReaderTests |18|
| ProcessDecisionRunScopePersistenceTests |9|
| ProcessDecisionFundingPersistenceTests |35|
| Total |106|

The eleven additional fence cases cover expiry without status change, equality at the
expiry boundary, wrong token, wrong attempt, released/expired status, each supported live
status, a dangling active token during manager review, and clock advancement while
publication waits behind another root transaction. Historical reads remain available
after dispatch refusal. Existing claim rotation, manager review, quiescence, commit/
rollback, unrelated-root progress and exact-query tests also pass.

Affected fixtures use an explicit clock instead of relying on the machine date. The
funding fixture now gives its running step the same attempt as its claim; the prior
mismatch was hidden by the weaker predicate. No expected authorization outcome was
weakened. Persistence, Modules.Processes and Integration build in standard Release,
serialized against the usual outputs. Discovery confirms exactly the selection above.

Complete portability-static passes with 15,640 findings and no baseline change; final
enforcement has no write flag. Scanner tests, rendering register, documentation and diff
checks pass. The query-count tests still establish bounded lookup, not workload throughput.

## Evidence and resources

Source: 9,560 files, SHA256
13A4AD4508B01BB5B487B53094CA81C5D362100E098E9D41FD82AD6FF64A9C2C.
Runtime: 2,170 files, SHA256
0C0AAB6262655EC9C582EB214712FA32ADFA8A1DC4B329A64E9E150FAED6CC88.
[Retained proof](../proof/2026-10-19-planning-authority/c4-publication-claim-lifetime.json)
SHA256 E0F708FCCC7F821F04E860022A50259EF29A24B2296F7FB68071DA0111818852.

The candidate records ownership before creation and reuses PostgreSQL 18 on 53619 and
existing Process fixture families. The 422.7272699-second observer covers the complete
402.1951254-second execution. Source/runtime identities are unchanged before freeze
release. Reconciliation finds no surviving observed process or database-name delta;
four unattributed TEMP entries are preserved. The isolated fixture remains needed for
subsequent work. Historical cleanup denials and the failing broad Stable gate remain open.

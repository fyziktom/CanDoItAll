# P02b commit knowledge when receipt disclosure is revoked

Status: source candidate frozen for parent validation; no build, discovery, test, database operation or static scan executed for this repair. This closes one outcome-classification defect and does not implement the separate HTTP effect-time authorization observation.

## Change and invariant

`ProjectDefinitionWriter.RecoverOutcomeAsync` previously returned `Rejected` for a receipt protocol rejection even after the provider commit had been attempted. A lost acknowledgement followed by revoked receipt disclosure could therefore report non-effect after the database had committed.

The protocol-rejection branch now returns `Unconfirmed(originalOperation, CommitUnconfirmed)` whenever `CommitAttempted` is true. The existing outer known-committed/replayed path still preserves `Accepted`. Initial validation and disclosure rejection before dispatch remain `Rejected`; there is no new effect retry or caller-identity change.

## Focused source proof

`ProjectDefinitionOperationTests.Recovery.cs` adds one Fact, `Actual_commit_with_lost_acknowledgement_and_revoked_disclosure_remains_unconfirmed`. The existing supported EF transaction interceptor revokes the test receipt reader and throws from `TransactionCommittedAsync`, after PostgreSQL has committed. Independent contexts assert the root revision, operation receipt and enlisted search projection all exist. Lookup then reports denied disclosure, and a same-envelope retry rejects that new attempt without changing the persisted state. Restored read access returns an exact replay of the original accepted payload and revision. The original ambiguous outcome remains `Unconfirmed`.

This is a real-provider commit seam, not a simulated network disconnect. The mutable receipt-access test double exercises the owner disclosure boundary; separate HTTP wait/revocation tests remain part of the authority-observation card.

## Source-derived expected counts

Filter for the added case: `FullyQualifiedName~ProjectDefinitionOperationTests.Actual_commit_with_lost_acknowledgement_and_revoked_disclosure_remains_unconfirmed`: **1 discovery / 1 execution**.

Whole partial class filter: `FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectDefinitionOperationTests`: **23 discovery / 23 execution**, previously 22. Source arithmetic: primary file 5 Facts plus 9 InlineData rows; admission-recovery file 6 InlineData rows; recovery file 3 Facts. No MemberData, ClassData, disabled discovery or skip expansion is involved.

Parent validation must use the existing isolated PostgreSQL fixture and standard Release outputs. Run discovery against these expectations before execution, then the required repository static gate against the final source candidate. No performance, HTTP cutover or general concurrency-initiative closure is claimed here.

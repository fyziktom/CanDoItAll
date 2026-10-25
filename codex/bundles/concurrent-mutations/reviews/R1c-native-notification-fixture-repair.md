# R1c native notification fixture repair

Status: source-only repair; not built or executed. This delta is separate from the
in-progress historical receipt-review native integration.

The retained main lane `r1c-activity-repair-validation/execute-r1c-native-components.stdout.log`
reports one failure in `Native_recovery_distinguishes_known_commitment_from_uncertainty_and_access_denial`
at the whole-history success-notification assertion (line 71 in the failed source).
The other 56 selected native rows passed. The original failure remains valid evidence
for that candidate and is not overwritten by this source diagnosis.

`CreateRecoveryPanelAsync` in `AgentChatPanelResponsivenessTests.Recovery.cs` stages an
image before returning the fixture. Actual `StageUploadedAttachmentFilesAsync` emits
the success notification “Staged 1 attachment path(s) for the next prompt.” The failed
assertion included that setup event, and the retained xUnit output identifies collection
position 0. No successful receipt recovery is required to produce that notification.

The repair asserts the single expected setup success and its exact attachment-staging
detail before the first recovery, both in the opening arrangement and in each later
activity-fault arrangement that independently stages an attachment. Each final assertion still rejects every success
notification except that exact previously captured object. It does not filter by text,
severity count or a fixed list offset, so a newly emitted recovery success still fails.
Existing commitment, denial, cleanup, activity, composer and no-private-diagnostics
assertions are unchanged. No production source or shared fixture was changed for this
repair. The test file's four Facts and the selected native lane's 57 rows are unchanged.

Parent owns the focused rerun and static validation. Discover and execute the same
frozen test binary for this Fact; its later fault arrangements were not reached in the
failed run and must not be claimed proved until that rerun succeeds. Historical native
source is excluded from this repair's transfer inventory.

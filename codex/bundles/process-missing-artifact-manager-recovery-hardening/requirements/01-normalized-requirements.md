# Normalized Requirements

## R001 Detect Missing Required Completion Artifacts

After a step execution resolves as `Completed`, the dispatcher must compare required artifact expectations with recorded process artifact expectations before transitioning the step.

Success: missing artifacts are represented by exact expectation titles.

## R002 Ask The Process Manager

If required artifacts are still missing after projection, the dispatcher must request manager-mediated recovery instead of running the same step executor recovery loop.

Success: recovery uses the resolved manager technical agent when one is available and records a manager directive journal entry.

## R003 Ground Recovery In History

The manager recovery prompt must instruct the manager to use previous step history, upstream artifacts, execution run ids, tool receipts, changed files, and existing current-run evidence to recover only the missing artifacts.

Success: directive text includes the missing artifact titles, previous execution run id, upstream artifact references, and a prohibition against broad implementation reruns.

## R004 Preserve Artifact Validation

The recovered artifacts must still pass existing process artifact projection and expectation matching.

Success: a step is not marked complete unless the required artifact expectation ids are recorded.

## R005 Fail Predictably

If no manager can be resolved or manager recovery does not produce the artifacts, the step must transition to `Blocked` with a concrete reason instead of silently stopping or looping.

Success: blocked outcome explains manager resolution failure or remaining artifact titles.

## R006 Audit And Loop Control

Manager recovery must leave durable journal evidence and must not recurse into repeated manager or executor reruns for the same completion gap.

Success: tests prove one manager recovery directive is generated for the missing artifacts and the dispatcher returns blocked when recovery cannot satisfy them.

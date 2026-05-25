# Current State Analysis

## What Codex Improved Correctly

The branch added a process-owned finalizer and moved the direct AgentFramework completion path through it. The finalizer can project execution artifacts, reload artifact records, validate required artifacts, persist diagnostic journal entries, invoke manager recovery, and then apply the final transition.

This is the correct direction because process completion should not be controlled solely by an agent's structured output.

The branch also routed workflow-backed role completion through the finalizer and tightened manager recovery fallback to require explicit recovery capability rather than generic `lead` matching.

## What Remains Structurally Weak

### Workflow-backed roles still do not carry process artifact contracts

The workflow candidate branch in `LoadDispatchCandidateAsync` returns a `DispatchCandidate` with:

- `TechnicalAgentId = Guid.Empty`
- `ExpectedArtifacts = []`
- `RecordedArtifactExpectationIds = new HashSet<Guid>()`
- `ArtifactInputs = []`

The finalizer validates `candidate.ExpectedArtifacts.Where(expectation => expectation.IsRequired)`. Therefore a workflow-backed process role can go through the finalizer while the finalizer has nothing to validate.

This is a false sense of process-owned validation.

### Subprocess parent steps still bypass the finalizer

Subprocess parent dispatch calls `ProjectCompletedSubprocessArtifactsAsync` and then transitions directly to the subprocess-derived terminal status. It does not use `FinalizeStepCompletionAsync`.

This means subprocess completion is still not subject to the same process artifact validation as direct agent and workflow-backed steps.

### Subprocess projection still writes placeholder-like required artifact records

`ProjectCompletedSubprocessArtifactsAsync` creates a parent `ProcessArtifactRecord` with `ArtifactExpectationId = expectation.Id` even when `ResolveSubprocessSourceArtifact` returns null. The provenance then says no child artifact was available and the child ledger should be inspected.

That parent record should not satisfy the required expectation. It should be a diagnostic or gap record, not a completion artifact.

### Finalizer strictness is heuristic, not explicitly modeled

`ResolveArtifactExpectationMode` infers mode from title/validation text and artifact kind. Words like `log`, `screenshot`, `test output`, `json`, and `markdown` affect runtime classification.

For a generic process runtime, that is risky. A finance process may require a `decision log`, a legal process may say `not available` in a valid finding, and an operations process may have a `TODO register` as an actual deliverable.

### Prompt-only scope boundaries are not enough

`BuildExecutionPromptCore` contains many good instructions:
- do not add optional features,
- do not execute side actions unless the current step calls for them,
- non-mutating steps must not create external product roots,
- architecture/planning can record boundaries without creating products,
- implementation steps must create real deliverables.

However, the incident proves prompt-only guardrails are not reliable enough. The runtime must enforce step operation policy at tool-invocation level using metadata, not only text.

### Blocked state is overused for dispositions

A product defect, missing proof, rejected QA result, or failed validation is not always a process blocker. If the step has a modeled repair/rework/no-go/escalation branch, the correct behavior is often:
- complete the current review/decision step,
- select the repair/no-go branch,
- pass a durable artifact with the finding.

Hard `Blocked` should mean the step cannot make its governed disposition due to missing inputs, unavailable authority, denied tools, unavailable environment, or unsafe execution boundary.

### Upstream materialization can strand downstream steps

The current downstream missing-input path moves the downstream step to `Blocked` and then requests upstream materialization. The dispatch candidate query loads only `Ready`, `WaitingApproval`, and `InProgress`. Unless another mechanism unblocks the downstream step after upstream rerun, this can permanently stop a process that could continue once the missing artifact appears.

### Retry loop still needs no-progress compression

The finalizer catches post-completion artifact failures, but `ExecuteUntilSettledAsync` can still run multiple attempts for repeated incomplete tool/proof failures. Repeating the same missing tool, validation failure, or scope/tool-policy mismatch wastes time and can create noisy artifacts.

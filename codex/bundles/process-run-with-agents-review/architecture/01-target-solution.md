# Target Solution

## Target Runtime UX

Process Workspace should become the operator console for agent-backed process runs.

- Launch tab starts the process through the existing launch plan path.
- Activity tab shows live run health, active agents, pending approvals, retrying steps, dead-lettered automation, and stranded steps.
- Execution tab shows every attempt for the selected step, including raw AgentFramework state and governed process interpretation.
- Evidence tab shows artifact records plus an expectation ledger that makes missing and projected artifacts explicit.
- Runtime canvas shows the selected step's health and available operator actions.

## Target Recovery Model

Retries should be visible, classifiable, and controlled.

- `Automatic retry`: dispatcher retry within max attempts because required tools, concrete proof, artifacts, or governed outcomes are incomplete.
- `Crash recovery`: AgentFramework execution was interrupted or cancelled and process recovery is redispatching the step.
- `Context reset retry`: retry intentionally starts a fresh chat session to avoid poisoned context.
- `Provider repair retry`: assigned agent provider was changed before retry.
- `Manual rerun`: operator explicitly asks the agent to do the job again with recovery instructions.

## Target Artifact Model

Process artifacts should be modeled as an obligation ledger.

- Expected artifact: process definition requirement for a step.
- Execution artifact: file or response artifact produced by AgentFramework.
- Projection result: process artifact record linked to an expectation.
- Missing artifact: required expectation not satisfied by execution, response projection, or completed decision artifact.
- Projection failure: artifact was declared but file/path/storage/recording failed.

## Boundary Decisions

- Process state remains canonical in `CanDoItAll.Modules.Processes`.
- AgentFramework remains the source of technical execution runs, artifacts, approvals, checkpoints, logs, and tool receipts.
- Process Workspace should query both and present a single operator view.
- Do not add a separate orchestration UI outside Process Workspace for this flow.
- Do not turn logs into the primary recovery interface; logs are support evidence, not operator workflow.

## Data Surface Additions To Plan

- Process run health view model with outbox health and recovery classification.
- Step execution attempt ledger view model.
- Artifact expectation satisfaction view model.
- Retry/rerun command model with explicit reason, recovery directive preview, and audit decision record.
- Negative-path test hooks for deterministic missing artifact, crash, stale run, and dead-letter cases.

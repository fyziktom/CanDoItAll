# Requirements

## R01 Secret handling and report truthfulness

Remove committed provider credential values, add tracked-file secret scanning tests/scripts, add snapshot integrity validation for execution reports, and never echo raw secrets.

## R02 Structured output and finalizer enforcement

Preserve structured output contracts across all run paths, enforce required finalizer exact-once behavior, and validate/finalize machine output before assistant transcript persistence and run completion.

## R03 Tool governance

Respect built-in tool enabled/disabled configuration, use dedicated policy-block exception type, classify process mutation tools as mutations, approval-wrap or deny them, and deny unknown `processes_*` tools by default.

## R04 Recovery decision model

Implement typed `AgentRecoveryDecision` and `AgentRecoveryMode` with persistence and journal/read-model exposure.

## R05 Rework packet model

Implement typed `AgentReworkPacket` for QA rejections, failed proofs, incomplete governed outcomes, and manual rework.

## R06 Proof fingerprints

Implement proof fingerprints for build/test/browser/inspection proofs and reuse only valid fingerprints.

## R07 Retry ledger and loop control

Persist retry attempts with mode, reason, context strategy, provider/model, proof validity, outcome, and timestamps. Enforce budgets/backoff and escalate loops.

## R08 Escalation and approval control plane

Add first-class process escalations and a unified operator-facing approval model.

## R09 Process UI operability

Add control center, escalation queue, approval console, rework console, attempt timeline, proof validity display, and dead-letter recovery actions.

## R10 Maintainability/testability

Extract focused services from large partial classes and favor behavior-level tests over reflection/string tests.

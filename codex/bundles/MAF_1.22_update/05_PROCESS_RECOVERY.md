# Process autonomy without weaker safeguards

## Success means completion of justified work, not fewer red badges

The user reports steps that escalate despite being manageable autonomously. The code has several distinct layers: a step can return a manager issue; the manager may authorize recovery; a policy/budget decision can escalate the incident; the UI may request human action. Count these separately. A `NeedsManager` adapter result is not automatically a human escalation. [R17/R18/R20]

Use the actual typed states/events in the checkout. The terms below describe acceptance semantics, not a proposed replacement enum.

## Investigate one failing path end to end

Correlate process run, step instance, dispatch claim, agent execution, tool journal segment/batch, native session/checkpoint, proposal/approval and final artifact. Capture the stage and typed reason that first lost correctness; do not diagnose from the final UI message alone. Preserve restricted details separately from user-visible safe summaries.

Map current behavior through `AgentFrameworkProcessStepExecutor`, `ProcessAgentExecutionAdmission`, workflow execution, output validation/finalizer, completion coordinator, recovery policy, manager policy/budget and projection. Locate the current implementations via CodeAnalytics. Look for false finalizer/provider classification, lost required tools, incompatible checkpoint restore, partial execution receipts, repeated child launch, exhausted budgets charged by duplicate events, and error-to-success/cancellation-to-failure conversion. These are investigation targets, not assertions that every one is defective.

## Recovery decision contract

| Situation | Expected behavior |
|---|---|
| Provider 429/503 or another recognized transient cause **before effects, proven durably** | Bounded automatic recovery with cancellation-aware backoff; no unnecessary human decision |
| Completed read-only operation with an explicitly repeatable contract | Revalidate authority and retry/read when the existing policy allows; preserve evidence |
| Mutation attempted with uncertain outcome or interrupted receipt persistence | Reconcile by durable operation/business identity before retry; never assume zero recorded receipts means zero effects |
| Required finalizer invalid/missing after valid work | Bounded structured repair using current-run evidence; do not repeat completed mutations or invent artifact references |
| Existing pending/completed child process | Reuse/observe the authoritative child; do not create another because the parent resumed |
| Approval outstanding | Wait for the exact required decision; no auto-consent from model text or fabricated history |
| Permanent auth/configuration/unsupported host or missing rights | Accurate actionable diagnostic; only the allowed owner can repair rights/configuration; no repeated provider loop |
| User cancellation or host cancellation of the run | Cancellation propagates; no false completion or unsolicited retry; owned resources released |
| Budget exhausted or genuine unresolvable policy/effect uncertainty | Explicit justified escalation with safe reason, attempt history and preserved artifacts |

A transient failure classification is only one prerequisite for retry. Retry safety and idempotence must be proven independently. The existing attestation deliberately requires strong durable evidence [R18]; never downgrade an unknown-effect case to safe just to avoid escalation.

## Required process scenario set

Use a small, bounded project-scoped task, not an ambitious app-generation benchmark as the only gate. Example: create a short Markdown note containing a unique run marker in an authorized project asset, then produce a valid `ProcessStepOutcomeResult` with genuine current-run evidence. Use only tool names exposed by the actual runtime contract. Do not assume a general `processes_*` provider exists. [R08]

P01: normal single-agent step completes with a materialized artifact and zero human escalations.  
P02: one deterministic transient pre-effect failure, then success; one recovery decision, bounded attempts, one final artifact.  
P03: permanent authorization failure identifies the cause rather than cycling through generic transient/finalizer retries.  
P04: mutation completed but acknowledgment/receipt is interrupted; recovery reconciles, and the effect does not duplicate.  
P05: invalid finalizer then valid structured repair using real evidence; repair budget honored.  
P06: parent resumes while its child exists; no duplicate launch.  
P07: workflow-backed step correctly transfers terminal output and failure/cancellation.  
P08: user cancellation during waiting/streaming prevents later success and unexpected re-dispatch.  
P09: genuine missing permission and deliberately exhausted recovery budget still stop/escalate correctly.

These are acceptance scenario IDs, not existing test method names. Implement them with the narrowest deterministic fixtures first. Use actual PostgreSQL persistence for restart, claims, receipts and ownership scenarios; fake only external failure sources where needed.

## Live comparison

After standard tests are green, repeat the normal project chat/process/workflow journeys with the existing approved real-provider configuration and bounded spend. Record model, configuration, relevant tool/policy fingerprint, run IDs, attempts, cause codes, artifact IDs and human interventions. A modest predeclared repetition count such as three normal runs is reasonable; choose based on cost and report all attempts, including failures. Deterministic injected-failure tests are the primary proof of each repaired defect; a few stochastic successes are not statistical proof of general reliability.

Where the old behavior can be safely measured, compare like-for-like fixtures/provider settings. The baseline comparison can be a targeted old-version repro and a native state fixture; it does not require an early full-suite run. Report “not measured” rather than claim an escalation-rate improvement from the dependency release notes.

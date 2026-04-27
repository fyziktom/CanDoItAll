# Requirements

## R00 - Secret safety

Remove plaintext secrets from tracked source and add scanning.

Acceptance:
- No real-looking `sk-...` OpenAI key remains in appsettings, docs, source, tests, or fixtures.
- The exposed key is rotated/revoked outside the repository.
- A test or CI script fails on realistic API key patterns.

## R01 - Process tool mutation classification

All process tools that mutate definitions/runs/assignments/artifacts must be classified as mutation tools.

Acceptance:
- `processes_definition_save`, `processes_definition_publish`, `processes_definition_delete`, `processes_definition_import`, `processes_run_start`, `processes_step_transition`, `processes_assignment_resolve`, and `processes_artifact_record` classify as `Mutation`.
- Post-finalizer invocation of any process mutation tool is considered a finalizer sequence violation.
- Mutation process tools are wrapped for approval or blocked when approval is unavailable, unless the governed automation policy explicitly suppresses approval in a safe internal context.

## R02 - Recovery mode taxonomy

Introduce typed recovery decisions.

Acceptance:
- Retry decision returns `AgentRecoveryDecision` with mode, category, reason, attempt number, and source execution run id.
- Modes include format repair, fresh step retry, rework continuation, provider fallback retry, approval continuation, and human escalation.

## R03 - Typed rework packet

Create a persistent `AgentReworkPacket` model for QA/build/test/browser/artifact repair.

Acceptance:
- QA rejection creates a packet.
- Build/test/browser proof failure can create a packet.
- Manual rerun can attach human directive into a packet.
- Repair step prompt includes packet id and a compact packet summary; the packet itself is persisted.

## R04 - Efficient context strategy

Select context strategy per recovery mode.

Acceptance:
- Format repair never creates a new agent run.
- Fresh retry uses a fresh session and durable context.
- Rework continuation uses the typed packet and target artifacts.
- Approval continuation uses the same compatible session.
- Failed session transcript is not blindly replayed.

## R05 - QA return loop

Make QA-driven repair minimal and typed.

Acceptance:
- QA findings propagate into `AgentReworkPacket.Findings`.
- Repair agent receives target artifact/file references.
- Repair agent is instructed not to regenerate unrelated work.
- QA recheck references the packet and repair attempt.

## R06 - Proof fingerprint reuse

Replace tool-name proof carry-forward with fingerprint-based proof reuse.

Acceptance:
- Receipts contain command, working directory, relevant file hashes, artifact hashes, environment/tool version, status, and timestamp.
- Reused proof receipts include a reuse reason.
- Mutating relevant files invalidates dependent proof receipts.

## R07 - Retry ledger and loop control

Persist recovery attempts and detect loops.

Acceptance:
- Ledger tracks process run id, step run id, recovery mode, failure category, failure signature hash, provider, source execution run id, rework packet id, and next attempt time.
- Identical repeated failures escalate rather than loop.
- Provider fallback budget is separate from normal step retry budget.

## R08 - Provider approval capability proof

Verify and align MAF approval capability for Chat Completions vs Responses.

Acceptance:
- If installed MAF supports `ApprovalRequiredAIFunction` with Azure/OpenAI Chat Completions, feature matrix and tests reflect that.
- If not, documentation and tests explain the installed-version limitation.
- No mutation tool requiring approval is exposed when no effective approval path exists.

## R09 - Domain recovery guidance provider

Move domain/project-specific retry guidance out of generic dispatcher.

Acceptance:
- Generic recovery directive builder contains generic structure only.
- Domain guidance is selected by template/project type/tags/artifact expectations.
- Static regression prevents calculator/Blazor-specific guidance in generic dispatch files.

## R10 - Behavioral tests and docs truthfulness

Add behavior tests and update verification docs.

Acceptance:
- Tests cover process mutation classification, post-finalizer process mutation sequence violation, QA rejection packet creation, proof fingerprint invalidation/reuse, format repair without new run, failed governed structured output, approval continuation session strategy, and provider approval matrix.
- Verification docs list only tests that exist and were run.

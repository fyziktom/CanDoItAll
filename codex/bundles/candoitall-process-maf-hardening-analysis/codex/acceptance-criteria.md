# Acceptance criteria checklist

## Diagnostics

- [ ] Operator action for blocked step includes exact step key, step id, process run id and last strategy receipt.
- [ ] If AgentFramework observation is missing, runtime receipt diagnostics are still displayed.
- [ ] Operator message does not recommend blind retry when diagnostics are missing.
- [ ] Rework prompt includes `BlockedStepPacket` with exact next action.

## Subprocess bridge

- [ ] `StepKind=Subprocess` with runtime-owned contract does not require the agent to call `project_structure_process_subprocess_launch`.
- [ ] Parent waits/defer while child is active.
- [ ] Parent completes when child accepted handoff exists.
- [ ] Parent blocks with concrete no-go when child escalation packet exists.
- [ ] Parent produced artifact is written under parent run/step path.

## Artifacts

- [ ] Expected output prompt includes expectation key/title and primary managed ref.
- [ ] Produced artifact refs are derived from managed artifact content.
- [ ] Artifact ledger events use the applied result after finalization.
- [ ] Missing expected output names the semantic artifact, not only GUID slot id.

## Tool preflight

- [ ] Required runtime tools are checked against actual composed providers before agent execution.
- [ ] Missing/denied tools produce deterministic runtime diagnostics.
- [ ] Agent is not invoked when mandatory tool preflight fails.

## Templates

- [ ] `prepare-solution-skeleton` has typed accepted outputs for initial and repaired setup handoff.
- [ ] `setup-repair-escalation` is typed as no-go child output.
- [ ] Manual skip is disabled or typed as an output-producing branch.
- [ ] Template loader validates subprocess contract consistency.

## Regression

- [ ] Parent/child subprocess behavior is covered by tests without live LLM.
- [ ] Observation truncation bug is covered by tests.
- [ ] Finalization/ledger divergence is covered by tests.

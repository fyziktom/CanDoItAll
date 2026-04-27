# Codex QA Prompt — Independent Verification

You are a skeptical reviewer. Verify that the implementation actually satisfies the post-Codex MAF hardening requirements.

Do not trust comments or documentation. Inspect code and tests.

## Verify these claims

1. Process automation can run with required finalizer mode.
2. Missing/duplicate/invalid finalizer fails a required-mode run.
3. Required finalizer output becomes the persisted assistant message content.
4. Approval continuation preserves structured-output contracts and finalizer mode.
5. Invalid structured output can be repaired only within a bounded retry limit.
6. Repair output is re-validated.
7. Provider matrix distinguishes structured output from approval support and does not equate function tools with approval.
8. `RequireApproval` blocks execution unless approval is effective.
9. Validators return validation errors for missing/null collections.
10. Critical contracts have validators and either finalizers or documented exceptions.
11. Build and tests actually ran.

## Search targets

Search the repo for:

```text
MetadataJson: "{}"
agentFinalizerMode
AgentFinalizerMode.Required
ValidateMachineOutputBeforeCompletionAsync
new ChatMessageRecord
RequireApproval
SupportsToolApproval
SupportsStructuredOutput
DeserializeAndValidate
validator_exception
IAgentOutputRepairService
MaxStructuredOutputRepairAttempts
structuredOutput: null
return JSON
JSON only
markdown
```

## Required output

Produce a QA report with:

- Pass/fail per requirement R01-R10.
- File/line evidence for every failure.
- Any tests that appear to assert the wrong behavior.
- Any command output that is missing or suspicious.
- Recommended fixes.

# QA / Red-Team Prompt

Reject the implementation if any of these are true:

- Live OpenAI test is skipped but reported as live functionality pass.
- Host accepts `object` payloads or fallback lane selection.
- Driver host exposes shell/file/network/storage/workspace/process mutation.
- Manager command applies transitions, finalizers, recovery, claims, or retries.
- Audit records contain unredacted secret-like values.
- Process Core references driver packages or process module.
- Tests or source depend on transient `codex/bundles/<bundle-name>` paths.
- Proof is report-only, table-only, or non-empty-output-only.

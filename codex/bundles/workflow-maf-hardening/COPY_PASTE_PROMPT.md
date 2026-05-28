# Copy-paste prompt for Codex

You are working in `C:\repositories\CanDoItAll` on branch `processes-hardening`.

Use `.codex/bundles/workflow-maf-hardening` as the controlling execution bundle. Execute exactly one subbundle at a time, in the recommended order, and stop at each progression gate until the required proof is captured and `reviews/01-execution-report.md` is updated.

Primary mission: harden CanDoItAll Agents/Workflows after the MAF update so repository workflow definitions are validated, compiled/adapted into native Microsoft Agent Framework workflows, executed through typed source-generated C# executors where appropriate, and safely extended by plugin executors with schemas, permissions, cancellation, artifacts, telemetry, and approval gates.

Do not replace template YAML workflows with hard-coded C# examples. Do not overwrite user-managed workflow definitions. Do not skip the repo-local audit in SB01. If an SDK/package/feed/live-service prerequisite is missing, record it as evidence and continue with the safe static/refactor work that can be completed.

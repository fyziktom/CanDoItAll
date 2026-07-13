# C# Testability Plan

## Unit Tests

- Launch variable resolver resolves `{Key}`, `${Key}`, and `{{Key}}`, detects cycles, and reports unresolved tool-critical values.
- Completion gate evaluator aggregates product path, readback, receipt, artifact, and blocker gates.
- Required receipt matcher rejects missing `workspace_pwsh_run_script` even when scaffold receipts exist.
- Recovery classifier maps safe/idempotent completion-gate issues to current-step retry and maps budget exhaustion to manager escalation.
- Recovery instruction builder emits resolved paths and exact missing receipts.
- Subprocess resolver preserves child diagnostic code and result kind.
- Artifact bridge rejects physical file existence without accepted ledger/slot evidence.
- Tool plan guard validates exact tool, args, paths, scopes, and side-effect manifest.
- Template validators reject missing execution class, missing required receipt metadata, and prose-only hard gates.

## Integration Tests

- Reproduce the calculator incident with empty `.slnx`, existing Blazor project, missing helper receipt, and failed solution membership readback.
- Verify the parent subprocess packet contains child root cause and repair attempt details.
- Verify managed artifact wording and slot promotion happen only after gates pass.
- Load all process templates and artifact templates through the hardened validators.

## Negative Proof

- A test must fail if `workspace_dotnet_new` receipts are treated as sufficient proof of solution membership.
- A test must fail if `{CurrentProcessRunId}` reaches a tool-critical path.
- A test must fail if a parent accepts child markdown/file existence without accepted slot evidence.
- A test must fail if a safe/idempotent diagnostic routes to manager escalation before retry budget exhaustion.
- A test must fail if a hard template gate exists only in markdown prose.

## Manual Validation

- Rerun or simulate the blocked 5032 calculator flow.
- Confirm first failure routes to targeted rework, not manager escalation.
- Confirm repeated identical fingerprint escalates only after configured budget and includes root-cause evidence.

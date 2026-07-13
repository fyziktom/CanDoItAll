# Codex execution plan

## Goal

Fix recurring unnecessary process escalations caused by safe/idempotent completion-gate failures, weak child diagnostic propagation, prompt-only deterministic plans, and unresolved launch-variable placeholders.

## Required working branch

Create a new branch from the current `memory-providers` state, for example:

```text
process-safe-rework-and-dotnet-setup-hardening
```

## Implementation order

Do the tasks in this order:

1. `01-launch-variable-placeholder-resolution.md`
2. `02-completion-gate-aggregator.md`
3. `03-safe-auto-rework-recovery.md`
4. `04-diagnostic-specific-rework-packets.md`
5. `05-subprocess-child-diagnostics-and-ledger-bridge.md`
6. `06-managed-artifact-acceptance-order.md`
7. `07-runtime-owned-dotnet-solution-setup-plan.md`
8. `08-template-agent-contract-hardening.md`
9. `09-test-and-validation-checklist.md`

## Non-negotiable principles

- Do not weaken product validation to make the calculator run pass.
- Do not remove required `workspace_pwsh_run_script` receipt checks.
- Do not treat physical markdown existence as accepted process artifact evidence.
- Do not solve this by only adding more prose instructions to prompts.
- Safe/idempotent product completion failures must not go directly to human manager escalation unless retry budget is exceeded.
- Parent subprocess packets must expose child root-cause diagnostics.
- All new code comments must be in English.

## Expected final behavior for the provided incident

Given the observed child run:

- solution exists but is empty,
- app project exists,
- `workspace_dotnet_new` receipts exist,
- `workspace_pwsh_run_script` receipt is missing,
- file content gate fails,
- diagnostic is safe/idempotent,

runtime should produce:

- aggregate diagnostic containing both missing helper receipt and failed solution membership readback,
- `RecoveryDecisionKind.SafeRetry`,
- `RecoveryRouteKind.CurrentStepRetry`,
- targeted rework packet telling the agent/executor to write and run `DotNetCreateProjectScript` at a resolved script ref,
- no manager escalation on first attempt.

If the same fingerprint repeats beyond the configured budget, then escalation is allowed, but the manager packet must include the child root cause and exact attempted repair plan.

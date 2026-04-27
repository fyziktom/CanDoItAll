# 10 - Behavioral Tests and Documentation Truthfulness

## Problem

Some hardening is still likely covered by static source scans. Round 3 needs behavior tests and accurate verification docs.

## Required tests

1. Secret scanner rejects realistic API keys in appsettings.
2. Process mutation tools classify as mutation.
3. Process mutation after required finalizer violates sequence validation.
4. Process automation finalizer missing cannot complete step.
5. Wrapped JSON format repair does not create a new agent execution.
6. QA rejection creates typed rework packet.
7. Manual rerun creates or attaches a rework packet.
8. Repair prompt includes packet id, findings, target artifacts, and minimal-delta instruction.
9. Proof fingerprint reuse/invalidation works.
10. Approval continuation uses same compatible session.
11. Fresh retry after provider failure uses fresh session/provider fallback.
12. Provider approval capability matrix matches installed MAF behavior.

## Documentation acceptance

- Verification docs list only tests that exist.
- Verification docs include commands actually run.
- If tests cannot be run, docs say exactly why.
- No doc claims all recovery behavior is complete until typed rework packet and ledger are implemented.

## Execution status

Completed with residual repository-level test risk documented. Focused round 3 tests pass, docs list the commands actually run, and full solution test failures are recorded as existing unrelated broad-suite failures.

# SB01 Semantic Invariants

## Shallow-Pass Trap

A change that only changes wording in prompts or summaries while still producing no failed receipt for a real project-structure tool failure must fail.

## Adversarial Negative Proof

Simulate a blocked writeback result claiming `project_structure_node_create` failed with no failed tool receipt. Expected result: governed completion fails with the no-receipt reason.

## Semantic Positive Proof

Simulate or execute a project-structure tool failure that records a failed receipt. Expected result: completion evaluation recognizes the failed tool and enters the intended blocked/recovery/escalation path with safe diagnostics.

## Anti-Stub Audit

Search changed files for `TODO`, `NotImplemented`, `throw new NotImplementedException`, `stub`, and fixture-only branching.

## Raw Note Literal Closure

- Closes `N004` and `N005` only after durable failed-receipt behavior is proven.

## Production Behavior Artifact Matrix

| Artifact/Signal | Producer | Consumer | Lifecycle | Negative-Test Citation |
| --- | --- | --- | --- | --- |
| Failed project-structure tool receipt | Tool wrapper/audit writer | Completion evaluator/recovery | Failure occurs, receipt persists, recovery reads it | Pending |
| Safe diagnostic | Gateway/tool wrapper | Agent and operator review | Exception maps to code/message without sensitive payload | Pending |

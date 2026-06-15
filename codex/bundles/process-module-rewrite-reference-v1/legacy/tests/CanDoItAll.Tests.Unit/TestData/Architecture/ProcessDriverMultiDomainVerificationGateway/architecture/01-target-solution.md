# Target Solution

## Desired End State
A stable Process Core with deterministic read models/rules and a set of verification-only domain drivers that can inspect already-supplied process evidence and return diagnostics/audit facts without side effects.

## Allowed In This Bundle
- New read-only verifier packages for Office evidence, business-analysis evidence, and artifact evidence if they consume supplied payloads only.
- A controlled process verification gateway with an explicit allow-list and no dynamic registry/selector/DI.
- Process-module read-only observation aggregation, as immutable in-memory results only.
- Shared verification test harness and malicious fixture corpus.
- Documentation and release gates.

## Explicitly Denied
- Production generic driver runtime host.
- Registry, selector, DI extensions, manager commands, scheduler hooks, workflow hooks.
- Shell execution, package restore, Graph/Office runtime calls.
- Workspace/storage writes, process mutation, claim/transition/finalizer/retry/provider repair.

# Post-closure boundary hardening (2026-07-11)

## Trigger

The prior bundle closure exposed two new defects during the Tetris regression path:

- an initial implementation child could return a typed no-go but its parent did not have an explicit parent branch route;
- generic completion composition directly performed native workspace filesystem inspection.

Neither defect is specific to Tetris, Calculator, Blazor, or a generated scaffold.

## Decisions

### Parent child-output routing

`ProcessSubprocessChildOutputContract.ParentBranchOutcomeKey` is an explicit template contract. The generic subprocess bridge only carries the declared child disposition to that branch; it does not know any .NET, QA, or product-specific route.

The `dotnet-development-slice` template maps an accepted feature handoff to `implementation-ready` and a typed feature no-go to `implementation-needs-manager-repair`. The latter starts `dotnet-quality-repair`, which already separates diagnosis, repair, independent validation, bounded bughunt, and no-go. This avoids blind retry without adding an implicit runtime policy.

### Completion-gate ownership

`ProcessCompletionGateFactory` now retains only the generic rule that a completed product mutation needs evidence. Native product-root, required-path, and content inspection are provided by `WorkspaceProductFilesystemCompletionGateContribution` through the existing ordered contribution chain.

The contribution deliberately suppresses its mutation-output inspection when the generic evidence gate is already missing evidence. Required path/content checks still run. That preserves the existing recovery signal and avoids changing diagnostic priority as a side effect of the extraction.

### Bootstrap contract

The .NET launch driver accepts explicit solution, app, test, template, and framework values as authoritative. It has no Tetris or Calculator logic and no long natural-language scaffold instruction blob. Runtime-owned setup receives a small structured execution plan rather than agent prompt text.

Compatibility inference remains an explicitly recorded transitional fallback until a generic subprocess-launch variable contribution can pass a typed `DotNetBootstrapDecision` from the architecture artifact to the runtime-owned setup child. The generic launcher must invoke a declared contributor only; it must not parse .NET architecture.

## Rejected designs

- Do not add a `DotNetQualityRepairScaffoldEvidence`-style executor or special case.
- Do not add a second generic manager/retry loop; the existing quality-repair subprocess owns this domain workflow.
- Do not let the generic runtime model conjunctions of arbitrary branch gates. Templates express transitive prerequisites or split a step; each assignment continues to carry one branch gate.
- Do not reintroduce `partial` classes or a service locator.

## Testability contract

- Parent no-go routing is covered by bridge and strict template-mapping tests.
- Workspace inspection has direct contribution tests plus adapter-characterization tests for evidence-first priority.
- Every shipped template is checked for at most one distinct branch-conditioned dependency per step.
- Agent seed v47 is required for the revised topology and acceptance-ledger instructions to materialize into managed agents.

## Follow-up boundary

`ProcessCompletionIssueResultFactory` and blocked-outcome retry recovery still reach workspace filesystem helpers for defect-evidence classification. A future isolated extension seam should move those calls behind a completion-defect-evidence contribution and a blocked-outcome recovery contribution. It is intentionally not folded into the E2E stabilization change.

# Implementation Agent Prompt

You are implementing `process-runtime-adapter-architecture-refactor-bundle-20260709`.

Hard rules:

- Do not add new adapter partial files.
- Do not add generic runtime/dispatcher hardcodes for .NET, Tetris, Calculator, Blazor product rules, QA step keys, or software-delivery branch keys.
- Do not weaken completion gates or required receipts.
- Do not implement broad helpers/managers/common buckets.
- Every moved responsibility must become a top-level service/policy/evaluator/driver component with direct tests.
- Every critical subbundle must update proof manifests and source assertions.

Execution order:

1. Read README, requirements, architecture files, and the target subbundle README.
2. Run baseline source assertions before editing.
3. Add characterization tests before moving behavior.
4. Move one responsibility at a time.
5. Delete old adapter behavior as soon as production path uses the extracted type.
6. Run targeted tests and build.
7. Run CodeAnalytics when project references/contracts change.
8. Update proof manifest and execution report.

Closure is blocked if tests for moved behavior still instantiate `AgentFrameworkProcessExecutionAdapter`.


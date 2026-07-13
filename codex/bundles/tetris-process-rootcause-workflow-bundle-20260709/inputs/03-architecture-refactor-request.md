# Architecture Refactor Request

## Raw Request

The process execution adapter must be refactored as a real architecture boundary rather than a partial-class file split. `AgentFrameworkProcessExecutionAdapter` currently mixes MAF execution, subprocess coordination, runtime-owned execution, completion policy, managed artifacts, product evidence, routing, and result conversion.

The generic process runtime and dispatcher must remain domain-neutral. .NET/software-delivery knowledge may exist only behind an isolated driver or policy contribution. Existing escalation/root-cause bundles must guide the repair, and a Tetris end-to-end run must be observed without external intervention when local prerequisites permit it.

Before a Tetris E2E run, remove prior process-run artifacts from the TetrisGame project-structure node while preserving its workflow input artifact, and clear `C:\programovani\dotnet\output`.

Keep agent provider model selection on `gpt-5.4-mini`. Update OpenAI NuGet packages only when a compatible non-breaking update is available.

## Literal Constraints

- No permanent `partial` declaration for `AgentFrameworkProcessExecutionAdapter`.
- The adapter must become a thin boundary and must not retain extracted behavior through private forwarding methods.
- Extracted responsibilities must be directly unit-testable without constructing the adapter or a full MAF workspace.
- Generic runtime/dispatcher/completion code must not branch on .NET tool names, software-delivery step keys, Tetris, Calculator, or Blazor terms.
- Domain-specific receipt matching and recovery guidance must be isolated behind explicit driver/policy contributions.
- E2E observation must not manually transition steps, write product source, or rescue agents.

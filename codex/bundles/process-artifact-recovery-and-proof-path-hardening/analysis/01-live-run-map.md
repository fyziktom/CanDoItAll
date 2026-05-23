# Live Run Map

## Finding 1: Implementation Proof False Negative

The execution receipts for the blocked step included current-attempt reads of concrete product files under the managed output root:

- `output/scopes/organization/.../process-runs/<run-id>/TetrisGame/TetrisGame.csproj`
- `output/scopes/organization/.../process-runs/<run-id>/TetrisGame/Program.cs`
- `output/scopes/organization/.../process-runs/<run-id>/TetrisGame/Components/Pages/Home.razor`
- `output/scopes/organization/.../process-runs/<run-id>/TetrisGame/Application/TetrisGameEngine.cs`

The proof detector only recognized absolute/external-target request paths and then rejected managed roots such as `output/` as non-product paths. That made real product reads invisible to implementation proof.

## Finding 2: Browser Evidence False Positive

Result-summary evidence refs with paths like `artifacts/scopes/.../process-runs/20260522/.../stdout.txt` were treated as browser console logs because the old classifier accepted any `.txt`, `.log`, or `.json` path containing `process-runs`.

That is too broad. Browser evidence must come from a browser tool or a browser evidence directory under the scoped process artifact root.

## Finding 3: Upstream Artifact Recovery Gap

The existing runtime already avoids retrying a downstream step when a governed outcome explicitly blocks on missing upstream artifacts. The missing behavior was orchestration: after detecting configured upstream artifact inputs are missing, the process did not automatically ask the producing step to materialize the missing artifact and then retry the downstream step.

The generic repair is:

1. detect missing configured artifact inputs before dispatching the downstream agent
2. block the downstream step with a concrete missing-upstream-artifact reason
3. request a targeted rerun of the producing agent-owned step with a materialization directive
4. when the producing step completes, reopen dependent blocked steps whose block reason was missing upstream artifacts
5. allow the downstream step to load artifact inputs again and continue

## Boundary

No Tetris-specific or Blazor-specific process rule is introduced. Tetris is only the example product that exposed the generic failure.

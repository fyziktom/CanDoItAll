# Manual process validation after implementation

Run a new simple calculator process with the same kind of project structure.

## Expected observations

1. Parent `prepare-solution-skeleton` either launches or observes a `dotnet-solution-setup` child run.
2. Child `create-dotnet-project` creates the solution and Blazor app.
3. The solution is wired before the step completes.
4. `Calculator.slnx` or `Calculator.sln` contains the app project path.
5. If the helper script was initially skipped, the first failure is auto-reworked and does not require manager.
6. The operator packet, if any, names the exact child step and exact diagnostic.
7. Parent step does not say only „No AgentFramework result summary“ without child context.
8. `ProducedArtifactsJson` is non-empty only for accepted artifacts.
9. The process proceeds to test project creation and validation without human escalation for deterministic setup repair.

## Commands to inspect product target

Use equivalent local commands on the target machine:

```text
dotnet sln C:\programovani\dotnet\calculator-output\Calculator.slnx list
```

Expected output includes:

```text
src\Calculator\Calculator.csproj
```

or slash-normalized equivalent.

## What still justifies escalation

Escalation remains correct when:

- tool execution is actually denied by policy,
- product root is outside allowed scope,
- sideEffectManifest is invalid or unsafe,
- same safe retry fingerprint repeats beyond budget,
- template schema is inconsistent,
- child process produces explicit no-go output.

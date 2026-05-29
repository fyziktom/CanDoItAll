# 01-maf-1-8-upgrade-and-api-delta-gate

## Objective

Create a clean MAF package/API baseline before any deeper runtime changes. Attempt a staged upgrade from the current `1.6.2` MAF stable line to the current NuGet stable line observed during bundle preparation (`1.8.0`), plus matching A2A preview packages where compatible.

## Exact source references

- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `codex/bundles/workflow-maf-hardening/inventories/03-maf-version-baseline.md`
- `codex/bundles/workflow-maf-hardening/reviews/02-final-architecture-review.md`

## Implementation steps

1. Capture package baseline with `dotnet list package --outdated --include-prerelease` for affected projects.
2. Upgrade only MAF-related packages in a focused commit:
   - `Microsoft.Agents.AI`
   - `Microsoft.Agents.AI.OpenAI`
   - `Microsoft.Agents.AI.Workflows`
   - A2A/Hosting A2A preview packages if compatible with the stable line.
3. Restore and build.
4. Fix compile errors caused by MAF API changes with minimal adapter updates.
5. Run targeted workflow compiler/runtime/executor tests.
6. If upgrade cannot pass, revert code/package changes and write an ADR with:
   - exact package versions attempted,
   - exact compile/runtime blockers,
   - exact temporary version decision,
   - follow-up trigger for retry.

## Do not do

- Do not mix HITL/checkpoint/artifact refactors into this package gate.
- Do not downgrade packages.
- Do not change workflow semantics unless required by API migration.

## Acceptance checklist

- Package baseline is either upgraded or documented with exact blockers.
- `MafWorkflowCompiler` and `MafInProcessWorkflowExecutionBackend` compile against the chosen API line.
- Existing workflow template, compiler, executor, and runtime tests pass or have documented unrelated pre-existing failures.

## Proof required

- Package scan transcript.
- Restore/build transcript.
- Targeted test transcript.
- ADR if not upgraded.

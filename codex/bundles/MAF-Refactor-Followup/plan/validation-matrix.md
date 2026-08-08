# Validation matrix

## Builds

```powershell
dotnet build .\CanDoItAll.slnx -c Release
```

## Test projects

```powershell
dotnet test .\tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj -c Release --no-build
dotnet test .\tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -c Release --no-build
dotnet test .\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -c Release --no-build
```

Do not add new exclusions. Existing environment-specific tests must be classified explicitly and rerun where their prerequisites are available.

## Required scenario matrix

| Scenario | Expected invariant |
|---|---|
| Canvas -> Gantt between turns | next turn sees transition; active run retains Canvas snapshot |
| Project X -> Y during approval | continuation remains X; next send gets Y/new epoch |
| Unknown context source publishes Project Y | no project authority unless source provider validates it |
| Read-only canonical authority with write-capable agent | mutation tools absent/denied |
| Profile switch during authority lookup | admission fails after post-await generation fence |
| Organization runtime, project turn, provider timeout | recovery reads project-scoped artifact only |
| Project script execution | policy inspection resolves same project-managed path as command tool |
| Restart with pending approvals | exact proposals and original authority restore or fail closed |
| Envelope with provider conversationId | native payload is inspected after compatibility judgment |
| Tool schema changes, name unchanged | state becomes incompatible or migrates explicitly |
| Two pending tools | independent approve/reject decisions |
| Abandoned WaitingOnTool | no auto-decision; cache/lease capacity eventually reconciles |
| Stateless LLM empty response | one safe retry; usage retained; second empty fails typed |
| Stateless LLM provider exception | public/workflow sees sanitized typed failure |
| Handoff build failure | shared workspace bundle disposed once by owner |
| Linux path roots differing by case | identities remain distinct |

## Architecture proof

- CodeAnalytics snapshot and cycle report before/after project-reference changes.
- No production reference to broad `IAgentRuntime`.
- No MAF -> Modules project reference.
- No UI-derived grant path.
- No `new DefaultAgentToolInvocationPolicy()` in MAF runtime composition.
- No recovery/script helper using captured base scope.
- No all-proposals bool mapping in the primary UI path.
- No full-agent construction in workflow/lightweight LLM path.

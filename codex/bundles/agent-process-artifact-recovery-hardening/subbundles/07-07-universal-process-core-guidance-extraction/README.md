# 07 Universal Process-Core Guidance Extraction

## Status

- `Completed`

## Objective

Correct the earlier app-specific repair by making the process and agent-cooperation core domain-neutral. Process dispatch may require concrete source, artifacts, tool receipts, validation after mutation, and explicit blockers, but it must not contain calculator, Blazor, or .NET-specific recipes.

## Covered Notes

- The core process must work for standard documents, spreadsheets, applications, and other task types.
- Calculator and .NET guidance belongs in skills, tools, or agent instructions, not in universal process code.
- The workflow must not manually repair the final generated app as the process fix.
- When the goal is clear, agents should keep progressing through explicit recovery and validation instead of stopping after repeated identical attempts.

## Prerequisites

- Subbundles 01-05 remain the artifact/retry hardening foundation.
- Subbundle 06 is treated as diagnostic history and is superseded for process-core implementation by this subbundle.
- The seed catalog is available so domain guidance can be relocated or generalized without deleting valid specialized capabilities.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedNormalizer.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentRuntimeHardeningStaticRegressionTests.cs`

## Scope

- Remove calculator, Blazor, and .NET-specific recovery/proof rules from universal process dispatch.
- Replace domain-specific implementation proof with generic concrete-deliverable and validation-after-mutation proof.
- Keep technology guidance in seeded agents, reusable skills, and tool capabilities when explicitly scoped.
- Generalize reusable seed resources that used the calculator sample as default Blazor guidance.
- Add regression tests and source scans that prove process-core neutrality.

## Dependency Impact

- Prevents future document, spreadsheet, research, or non-.NET application runs from receiving irrelevant calculator or Blazor recovery directives.
- Keeps .NET and Blazor delivery quality guidance available to the agents that explicitly own those tasks.
- Reclassifies the app runtime failure as evidence of missing runtime proof, not justification for hardcoded sample-app guards in process orchestration.

## Validation Depth

- Focused integration tests for required-tool resolution and validation-after-mutation proof.
- Unit/static regression test that dispatch recovery and proof files stay domain-neutral.
- Build proof for the touched test project.
- Source scans proving process dispatch has no calculator/Blazor/.NET repair recipes.
- Source scans proving reusable seed assets no longer contain calculator-specific reusable examples. Subbundle 08 tightens this further by removing globally seeded sample-task skills.
- Bundle validator rerun after documentation sync.

## Implementation Steps

1. Audit dispatch, retry, artifact validation, prompt, seed asset, and bundle files for calculator, Blazor, and .NET hardcoding.
2. Delete sample-specific recovery providers and replace them with neutral no-op domain extension points.
3. Replace app-specific proof checks with generic concrete source/project read plus required validation after latest mutation.
4. Generalize reusable seeded agent/skill resources and do not keep sample-specific app guidance in reusable examples.
5. Update tests and static regression coverage for the corrected boundary.
6. Run build, tests, prohibited-string scans, and bundle validation.

## Do Not Do

- Do not repair the generated calculator app as part of this subbundle.
- Do not add another sample-specific guard to process dispatch.
- Do not remove valid specialized .NET or Blazor tool capabilities just because they are specialized.
- Do not hide sample-specific task instructions in universal process-core rules.

## Acceptance Checklist

- Process dispatch contains no calculator, CalcApp, CalculatorEngine, legacy Blazor hosting, or dotnet-specific recovery recipe.
- Reusable Blazor seed guidance contains neutral examples rather than calculator defaults.
- No calculator-specific reusable seed guidance remains; subbundle 08 removes the stale globally seeded calculator skill.
- Required validation is enforced generically after concrete mutations when validation tools are required.
- Bundle documentation identifies subbundle 06 as superseded for process-core implementation.

## Proof Required

- `dotnet build tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`
- Focused integration tests for neutral required-tool behavior and validation-after-mutation proof.
- Focused unit/static regression test for domain-neutral dispatch files.
- `git grep` scans for prohibited sample-specific strings in process dispatch and reusable seeds.
- Completed bundle validator run.

## Browser Validation Logging

- Browser proof is not required for this subbundle because it changes process orchestration, seeded text, and tests rather than a rendered UI route.
- If a future run uses the corrected process to repair a generated UI app, that run must capture its own runtime and browser proof through the proper implementation/QA agents.

## Progression Gate

- Bundle closure may stand only after source scans, focused tests, build proof, and bundle validation pass.
- If process dispatch still contains sample-specific repair recipes, keep this subbundle open.

## Suggested Agent Prompt

```text
Implement subbundle 07 only. Remove calculator, Blazor, and .NET-specific recipes from universal process dispatch. Keep technology guidance in seeded agents, skills, and tools; generalize reusable seed examples; then prove the boundary with focused tests, source scans, and bundle validation.
```

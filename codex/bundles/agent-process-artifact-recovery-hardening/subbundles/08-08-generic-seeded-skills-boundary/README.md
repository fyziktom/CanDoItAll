# 08 Generic Seeded Skills Boundary

## Status

- `Completed`

## Objective

Remove globally seeded sample-task skills and keep the seed catalog reusable across application, document, spreadsheet, research, and other delivery types. Seeded skills may be technology-specific when explicitly scoped, but they must not encode one sample app, project name, vendor, or fixed workload as the default behavior.

## Covered Notes

- Skills must be generic too.
- The agent could be asked to build any type of app, not only the calculator sample.
- Task-specific guidance belongs in the run input, generated bundle, selected task skill, or agent/tool instructions scoped to that task, not in the universal seed catalog.

## Prerequisites

- Subbundle 07 completed the process-core neutralization.
- Seed assets and seed catalog wiring are available for replacement rather than app repair.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\skills`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\resources`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\manifest.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedNormalizer.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentRuntimeHardeningStaticRegressionTests.cs`

## Scope

- Delete the stale calculator-specific seeded skill.
- Replace the one-off office-order seeded skill and example with a generic document/spreadsheet reconciliation skill and generic output example.
- Wire the new generic skill into the seed catalog for spreadsheet and finance-oriented agents.
- Remove retired built-in inline skills generically when they no longer exist in the current seed catalog.
- Add static regression coverage and source scans for sample-specific seeded skill content.

## Dependency Impact

- Prevents future app-generation runs from inheriting calculator-specific path, route, validation, or UI behavior.
- Prevents non-app document/spreadsheet runs from inheriting one vendor/order fixture as a universal rule.
- Keeps valid specialized technology skills, such as Blazor SSR delivery, available only as scoped technology guidance.

## Validation Depth

- Static regression test scans seeded skills/resources for sample-specific workload terms.
- Source scans prove seeded assets and seed catalog code no longer contain calculator-app or one-off office-order guidance.
- Build/test proof confirms the seed catalog and regression test compile.
- Bundle validator rerun after documentation sync.

## Implementation Steps

1. Audit seeded skills/resources and catalog wiring for sample-specific application or workload guidance.
2. Remove the calculator-specific seeded skill.
3. Replace the task-specific office-order skill/resource with generic document/spreadsheet reconciliation guidance.
4. Make stale built-in inline skill retirement generic instead of hardcoding the old task key.
5. Add regression coverage and run focused validation.
6. Sync bundle docs and final closure proof.

## Do Not Do

- Do not repair the generated calculator app in this subbundle.
- Do not remove scoped technology skills just because they are specialized.
- Do not add another globally seeded task skill for a single sample request.
- Do not hide task-specific app requirements in reusable seed examples.

## Acceptance Checklist

- No seeded skill/resource contains calculator-specific sample app instructions.
- No seeded skill/resource contains one-off office-order or vendor-specific reconciliation instructions.
- Retired built-in inline skills are removed through a generic stale-seed rule.
- The seed catalog still exposes reusable skills for app delivery, spreadsheets, and reconciliation.
- Validation proof is recorded in the execution report.

## Proof Required

- Focused unit/static regression test for seeded skill neutrality.
- `git grep` scans for calculator/sample app and one-off office-order terms in seed assets and seed catalog code.
- Targeted build or test proof for the touched seed/test projects.
- Completed bundle validator run.

## Browser Validation Logging

- Browser proof is not required because this subbundle changes seed text, catalog wiring, and static tests rather than a rendered UI route.

## Progression Gate

- Bundle closure may stand only after seeded sample-specific scans, focused tests, and bundle validation pass.

## Suggested Agent Prompt

```text
Implement subbundle 08 only. Remove globally seeded sample-specific skill guidance, replace one-off seed skills with generic reusable capabilities, keep scoped technology skills available, and prove the seed catalog is neutral with source scans, focused tests, and bundle validation.
```

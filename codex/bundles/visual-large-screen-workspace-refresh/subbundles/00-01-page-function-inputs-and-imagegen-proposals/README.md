# 00-page-function-inputs-and-imagegen-proposals

## Status

- `Ready`

## Objective

- Keep the bundle grounded in the real implementation by maintaining page inputs for every product route/page group, each real tab body, and each dialog family, then validating accepted `imagegen` proposals against those functions before product code changes start.

## Covered Inputs

- RN-001 improve visual look, working space, and clarity.
- RN-002 large-screen-only hard rule.
- RN-008 analyze each page and generate design proposals, including tab contents and dialogs.
- RN-009 use BaseLib/Tailwind/shared component mechanisms instead of own CSS.
- RN-010 use dialogs for pages with too much information.
- RN-012 professional B2B customer-video readiness.

## Prerequisites

- Raw request and reference screenshots are preserved under `inputs`.
- Current Razor route/component scan can be repeated with `rg`.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\00-page-input-index.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\01-shell-dashboard-projects.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\02-processes-live.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\03-agents-workflows.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\04-prompts-plugins-settings-resources.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\05-crm-hr.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\06-operations-supporting.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\analysis\03-imagegen-proposal-review.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages`
- `C:\repositories\CanDoItAll\src`

## Deliverables

- Page-input files remain complete and current for every product route/page group.
- Every real tab and dialog family has either a specific proposal panel or a documented grouped proposal.
- Regeneration decisions are documented when generated proposals do not cover a required function.
- Execution report notes that design proposals are planning evidence only.

## Dependency Impact

- All implementation subbundles depend on these inputs. If a page input misses a real function, downstream implementation risks hiding or removing behavior.
- BaseLib foundation subbundles use these inputs to decide which generic components are required.

## Validation Depth

- Critical planning foundation.

## Implementation Steps

1. Re-run route and tab/dialog scans before implementation starts if the source has changed.
2. Compare scan results with all `inputs/page-inputs/*.md` files.
3. Add missing page/tab/dialog rows and map them to proposal assets.
4. Review proposal images against the page-input functions.
5. If a proposal misses a required function or violates a hard rule, improve the prompt and regenerate the proposal before implementation uses it.
6. Update `analysis/03-imagegen-proposal-review.md` and the execution report.

## Scope Exceptions

- Do not treat image proposals as runtime UI proof.
- Do not tune mobile or medium breakpoints.
- Do not edit product code in this subbundle unless only test helper metadata is needed to complete route inventory.

## Do Not Do

- Do not invent page functions not present in source.
- Do not copy generated image labels into app copy.
- Do not skip a tab or dialog because it is nested inside a component.

## Acceptance Checklist

- Every product route/page group has a page input.
- Every major tab body and dialog family is described.
- Accepted proposal assets cover every page input or the exception is explicit.
- Regenerated shell proposal is the accepted source for DB/topbar behavior.
- Large-screen-only scope is stated.

## Proof Required

- Updated page-input files.
- Updated proposal review file.
- Proposal assets copied under `evidence/design-proposals/pages`.
- Prepared-stage bundle validator pass.

## Browser Validation Logging

- Browser proof is not required in this planning subbundle.
- The downstream screenshot subbundle must use these page inputs as its route checklist.

## Progression Gate

- Product implementation cannot start until page inputs and proposal review cover every route/page group, tab body, and dialog family listed by the current source scan.

## Suggested Agent Prompt

```text
Execute subbundle 00-01 only. Re-scan the Razor routes, tabs, dialogs, and major actions, compare them with inputs/page-inputs, update missing page inputs and proposal review rows, regenerate any insufficient imagegen proposals, and stop before product UI implementation.
```

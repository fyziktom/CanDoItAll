# Process Template Inventory

## Scan Summary

Fresh scan of `repo://Templates/Processes/processes/*/definition.json` found:

| Template | RequiredReceipts | QA-like branch hits | CompletionIssueRoutes | Final disposition |
|---|---:|---:|---:|---|
| `software-delivery` | 2 | 43 | 0 | Migrated. Template receipts were made branch-aware and Workbench now emits accepted-branch receipt rules, repair-branch validation receipts, content checks, completion issue routes, and acceptance criteria metadata. |
| `blazor-app-delivery` | 2 | 22 | 0 | Migrated through Workbench root-definition metadata. Browser/runtime acceptance proof is scoped to accepted outcomes and content failures route to repair/escalation. |
| `blazor-app-repair-fix` | 0 | 15 | 0 | Migrated through the same Blazor delivery root-definition path. No template-local hardcoding required. |
| `blazor-backend-feature` | 0 | 15 | 0 | Migrated through the same Blazor delivery root-definition path. Backend validation still uses branch-aware repair routing when browser/runtime acceptance proof is present. |
| `blazor-frontend-feature` | 0 | 15 | 0 | Migrated through the same Blazor delivery root-definition path. Frontend runtime/browser proof is accepted-branch-only. |
| `blazor-fullstack-feature` | 0 | 15 | 0 | Migrated through the same Blazor delivery root-definition path. Full-stack validation receives accepted/recheck routes and acceptance criteria where explicit criteria exist. |
| `dotnet-feature-function-implementation` | 0 | 28 | 0 | Audited and exempt. Existing dynamic validation receipt map has no unconditional browser/runtime acceptance-proof gate matching the incident shape. |
| `dotnet-development-slice` | 2 | 53 | 0 | Audited and exempt. Slice validation allows validation receipts and failed exits as repair evidence; it is not blocked by accepted-only browser proof. |
| `dotnet-solution-setup` | 8 | 17 | 0 | Audited and exempt from branch-route migration. Setup uses deterministic product receipt/content gates and validation receipts; it does not share the accepted browser proof loopback failure. |
| `dotnet-ui-screenshot-writeback` | 6 | 0 | 0 | Audited and exempt. It has no accepted/repair branch outcomes; receipt rules remain tied to actual screenshot/writeback capture. |
| Other process templates | 0 | 0 | 0 | Audited. No branch-aware completion-route migration needed because they do not expose the accepted/repair browser proof failure mode. |

## Migration Rule

Every template with accepted/repair-style branch outcomes must receive one of:

- structured branch-aware receipt rules;
- template-level `CompletionIssueRoutes`;
- explicit exemption explaining why completion gate issues cannot route to repair and should remain retry/manager;
- tests that prove the selected behavior.

## Exact Templates Audited

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`
- `repo://Templates/Processes/processes/blazor-app-repair-fix/definition.json`
- `repo://Templates/Processes/processes/blazor-backend-feature/definition.json`
- `repo://Templates/Processes/processes/blazor-frontend-feature/definition.json`
- `repo://Templates/Processes/processes/blazor-fullstack-feature/definition.json`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`

## Template Acceptance Questions

- Does the validation step have an accepted branch and a repair branch?
- Are required receipts branch-aware or unconditional?
- Are browser/runtime receipts used for tool exposure, completion evidence, or both?
- Is deterministic product defect evidence sufficient for repair branch?
- Does an accepted-branch content/proof failure route repair instead of retry/manager?
- Does the template define where runtime gate findings are written?
- Does the template require acceptance criteria ids, not only screenshots/build/test?

## Implementation Closure

- `software-delivery` template metadata was updated directly in `repo://Templates/Processes/processes/software-delivery/definition.json`.
- Blazor delivery roots were generalized through `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs` so future root launches receive the same branch-aware metadata.
- The acceptance criteria matrix is emitted only when explicit criteria are substantive; simple calculator-style projects do not get artificial `AC-*` gates.
- Domain-specific recovery guidance moved to `repo://src/Modules/CanDoItAll.Modules.Workbench/Processes/DotNetSoftwareDeliveryRecoveryAdviceProvider.cs`.
- Generic process application/runtime boundary scans are recorded in `bundle://proof/shared/transcripts/anti-stub-audit.txt`.

# Structured Input

## Bundle Profile

- Profile: `initiative`
- Reason: architecture-heavy runtime/process contract repair spanning process runtime, dispatcher adapter, contracts, Workbench contributors, templates, artifact templates, and tests.

## Source Assertions From GPTPro

- Calculator passing means earlier basic scaffolding, `.slnx`, helper, and writeback bugs are not the primary cause.
- Tetris exposed a higher-level QA branch/routing issue.
- QA branch `repair-required` should not require acceptance-only browser/runtime proof when deterministic defect evidence exists.
- `quality-accepted` plus deterministic scaffold/content failure should route to the configured repair branch instead of exhausting same-step retry budget.
- Product required receipts and capability scope receipts duplicate browser/runtime obligations and produce duplicate diagnostics.
- `ProcessStepRecoveryInstructionBuilder` contains software-delivery and .NET domain knowledge in generic application code.
- QA/recheck prompts remain ambiguous enough for agents to confuse missing proof with a product defect.
- Project-structure requirements are not converted into a machine-checkable acceptance matrix.
- Existing tests cover local gates, not the real combination of branch outcome, receipts, deterministic defect, and retry budget.

## Fresh Repository Observations

- CodeAnalytics snapshot `snap-20260709103653-3a49f8a9` loaded 22 scoped projects and 634 documents with no blocking errors.
- Scoped dependency analysis reported no cycles.
- `AgentFrameworkProcessExecutionAdapter.CompletionGates.cs` evaluates product receipt gates, process receipt gates, content gates, and artifact gates without branch/purpose context.
- `AgentFrameworkProcessExecutionAdapter.Types.cs` has `ProcessCompletionIssue` without route metadata.
- `AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` returns `NeedsManagerForCompletionIssues` before branch signals can be created from completion issues.
- `ProcessCapabilityScopeModels.cs` has receipt fields for current-run/success/minimum count but no branch applicability or purpose.
- `ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(JsonElement)` reads string arrays only.
- `ProcessLaunchApplicationService.FormatProductCompletionRequiredStringList` strips object arrays to strings and would lose structured receipt rules.
- `ProjectStructureProcessLaunchVariableContributor` emits string-array receipt maps for software-delivery QA/recheck.
- Process template scan found zero `CompletionIssueRoutes` in all process definitions.
- Similar accepted/repair validation branch flows exist in Blazor, .NET feature, .NET slice, and software-delivery process templates.

## Hard Constraints

- No Tetris-specific runtime fix.
- No new generic application/runtime hardcodes for .NET, Blazor, scaffold files, QA branch names, or tool names.
- Backward compatibility for legacy receipt formats is mandatory.
- Branch routing must be deterministic runtime behavior, not prompt-only guidance.
- Acceptance matrix support must come from project-structure inputs and remain generic.

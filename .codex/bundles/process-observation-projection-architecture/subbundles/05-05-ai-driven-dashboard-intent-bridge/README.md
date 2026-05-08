# 05-ai-driven-dashboard-intent-bridge

## Status

- `Ready`

## Objective

- Prepare a read-only, typed bridge from AI/user conversation intent to process observation dashboard focus, filters, and lazy detail descriptors.

## Success Criteria

- AI or chat output is represented as a strongly typed observation intent.
- Intent handling can focus dashboard state and open/load observation detail descriptors without mutating process runtime.
- Ambiguous or unsafe intents fail explicitly.
- Example request for QA/testing detail can resolve to the appropriate observation focus and dialog payload plan when matching process data exists.

## Covered Inputs

- R-003, R-007, R-009, R-012.
- User's target future mode: speaking with an AI agent and having the process dashboard change to show relevant process/stage/detail views.
- Observation contracts from `02`.
- Cache and source-of-truth rules from `03`.

## Prerequisites

- `02-observation-contracts-and-boundary` is complete.
- `03-projection-cache-and-invalidation` is complete or an implementation note explains why this bridge will use uncached read-only observation calls safely.
- `04-ui-observation-shell-and-dialogs` is recommended before any visible UI focus behavior, but not required for pure contract/service work.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.ManagerChat.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.Chat.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Chat\WorkspaceChatProjectionBuilder.cs`

## Deliverables

- `ProcessObservationIntent` and related typed focus/filter/dialog plan models.
- Intent resolver service that maps AI/user requests to read-only observation queries.
- Guardrails for project scope, authorization, ambiguity, unsupported actions, and mutation attempts.
- Tests for supported, ambiguous, unsupported, and mutation-like requests.
- Optional UI state integration that applies focus and opens a descriptor only when subbundle `04` is complete.
- The implementation should also reference observation contracts and state files introduced by subbundles `02` and `04`.

## Dependency Impact

- `06-validation-performance-and-rollout` depends on this phase to prove future AI dashboard control remains read-only and generic.
- Future conversational dashboard work depends on this phase to avoid direct component-state mutation and string-command coupling.
- Weak proof here creates security and correctness risk because AI output could drive unauthorized or mutating behavior.

## Validation Depth

- `Read-only AI integration foundation`

## Implementation Steps

1. Review existing manager chat/process assistant code and identify the safest integration point.
2. Add typed intent models for process observation focus, filters, time windows, detail targets, and dialog descriptors.
3. Add an intent resolver that maps structured AI output or parsed request data to observation queries.
4. Enforce project/authorization scope before resolving snapshots.
5. Explicitly reject mutation-like intents in this bridge.
6. Add tests using the QA/testing detail example and at least one ambiguous request.
7. If UI state from subbundle `04` exists, add a narrow adapter that applies a resolved intent to dashboard state.
8. Update execution report with supported intent examples, rejected examples, tests, and risks.

## Scope Exceptions

- Natural-language model prompting, speech input, and full conversational UI are out of scope unless already present and trivial to adapt.
- Mutation flows such as start/stop/retry/approve remain out of scope.
- App-specific QA stage names should remain data-driven from process definitions and step metadata, not hard-coded in the bridge.

## Do Not Do

- Do not allow AI output to call process mutation services.
- Do not accept free-form string commands as executable UI actions.
- Do not bypass existing authorization/project checks.
- Do not make dashboard behavior depend on a specific app-development process template.
- Do not add fallback behavior that guesses silently when the target process/run/stage is ambiguous.

## Acceptance Checklist

- Typed intent models compile.
- Resolver can produce focus/filter/dialog plans for a QA/testing detail request when matching data exists.
- Ambiguous targets return a typed ambiguity result.
- Mutation-like requests are rejected by this bridge.
- Tests prove process logic remains generic.
- Optional UI focus integration is covered by component/browser proof if visible.

## Proof Required

- `dotnet build CanDoItAll.slnx`
- New intent resolver unit/integration test command.
- If UI focus behavior is added: run `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspace"` and the `04` browser proof subset for the changed path.

## Browser Validation Logging

- N/A for service-only implementation.
- If UI focus is wired, target `/processes`, run large and narrow viewport checks, exercise a resolved QA/testing detail focus, and capture screenshots in the execution report.

## Progression Gate

- Downstream subbundles may continue only when tests prove the bridge is read-only, typed, scoped, and explicit about ambiguous or unsupported requests.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```

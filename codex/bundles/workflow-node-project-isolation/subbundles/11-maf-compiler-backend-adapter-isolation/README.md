# SB11 - MAF Compiler Backend Adapter Isolation

## Status

- `Completed`

## Objective

Isolate MAF-specific workflow compiler, backend, LLM component invoker, event normalizer, and handoff factory code behind adapter projects so MAF composes workflow runtime and executor services without owning workflow architecture.

## Success Criteria

- MAF workflow compiler/backend code lives in a MAF adapter project and depends on workflow/executor abstractions rather than concrete default/plugin executor projects where possible.
- Workflow runtime can select the MAF in-process backend through explicit backend catalog registration.
- MAF-specific event normalization and LLM component invocation keep existing behavior and diagnostics.
- No workflow core, template, or executor implementation requires MAF to compile.
- MAF compiler/backend/LLM/tool/MCP failures map to the typed workflow diagnostic contract instead of string-only failure summaries.
- The MAF backend is split by compile handling, backend run orchestration, event normalization, external request capture, payload/artifact capture, checkpoint creation, and diagnostics.

## Covered Inputs

- R05, R06, R07, R11, R12, R13, R14, R15, R17, R18.
- Architect note that MAF wrapper is too large and must not mix workflows/executors.

## Prerequisites

- SB10 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.MafAdapter\MafWorkflowCompiler.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.MafAdapter\MafInProcessWorkflowExecutionBackend.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.MafAdapter\MafWorkflowLlmComponentInvoker.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.MafAdapter\MafWorkflowEventNormalizer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\WorkflowExecutorJson.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.MafAdapter\MafHandoffWorkflowFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`

## Deliverables

- `CanDoItAll.AgentFramework.Workflows.MafAdapter` project for MAF-specific compiler/backend/invoker/normalizer/handoff adapter code.
- Focused helper/service split for the current large MAF backend responsibilities.
- Adapter registration extension that composes workflow runtime, executor catalog, templates, and MAF backend explicitly.
- Tests for compile, backend execution, event normalization, LLM component invocation behavior, external tool/MCP failures, handoff factory behavior, and explicit typed failure paths.
- Architecture proof that workflow core/runtime/templates/executor category projects do not depend on MAF adapter.

## Dependency Impact

- SB12 API/UI/Workbench adoption consumes the now-isolated workflow services and MAF adapter through host composition. If SB11 leaves MAF as the owner of workflow runtime/executors, the entire isolation goal is compromised.

## Validation Depth

- `Critical adapter isolation`
- Unit, integration, service-composition, architecture, diagnostics, and event compatibility proof.

## Implementation Steps

1. Move MAF-specific workflow compiler/backend/LLM invoker/event normalizer/handoff adapter code into the adapter project.
2. Split backend responsibilities before adoption when a moved class still mixes compile failure, run orchestration, event capture, payload/artifact handling, checkpoint creation, and diagnostics.
3. Replace direct default executor registration with category/plugin registration composed through executor abstractions.
4. Register the MAF backend through workflow runtime backend catalog contracts.
5. Add tests for compile success/failure, backend execution success/failure, event normalization, LLM invocation failures, external tool/MCP failures, and handoff factory output.
6. Add architecture checks proving workflow-owned projects do not reference MAF adapter.
7. Verify host composition still wires default and plugin executors.
8. Update execution report and proof.

## Scope Exceptions

- API, Blazor workflow page, editor, and Workbench adoption are SB12.
- Final cleanup of obsolete paths is SB14.

## Do Not Do

- Do not move workflow core behavior back into the adapter to simplify dependencies.
- Do not keep old MAF workflow registration as a fallback.
- Do not swallow compiler/backend failures; fail explicitly with actionable diagnostics.
- Do not move `MafInProcessWorkflowExecutionBackend` as one large class without splitting responsibilities and tests.
- Do not convert external tool/MCP/LLM failures to generic backend errors.

## Acceptance Checklist

- [x] MAF adapter project compiles.
- [x] Workflow-owned projects compile without MAF references.
- [x] MAF backend executes through workflow runtime contracts.
- [x] Default and plugin executors are composed through executor catalog registration.
- [x] Event normalization and LLM component invocation parity tests pass.
- [x] Compiler/backend/tool/MCP failures produce typed diagnostics with backend, provider/server/tool, node, retryability, redaction, and repair context.
- [x] MAF adapter files pass file-size/responsibility review.

## Execution Notes

- Added `CanDoItAll.AgentFramework.Workflows.MafAdapter` and moved MAF workflow compiler/backend/LLM/event/handoff ownership behind that project.
- Split backend responsibilities into configured artifact resolution, external request capture, progress observation, event normalization, LLM invocation, compiler, and compile-failure diagnostic mapping.
- Host and AgentFramework module now call `AddMafWorkflowAdapterServices(...)`; the old `AddBuiltInWorkflowExecutors` alias was removed so standard executor composition stays owned by `WorkflowExecutors.Standard`.
- Added typed compile-failure event payload proof through `WorkflowFailureDiagnosticEnvelope`, preserving existing executor/plugin diagnostics through executor core.
- Repaired template descriptor validation so known but unavailable plugin descriptors do not block template pack loading before plugin installation.
- Captured proof under `proof/SB11/`.

## Proof Required

- `proof/SB11/manifest.md` with changed file hashes, build/test transcripts, service composition transcript, architecture check transcript, and event compatibility proof.
- `proof/SB11/semantic-invariants.md` covering MAF adapter-only ownership, backend selection, event normalization parity, typed explicit compiler/backend/tool/MCP failures, executor catalog composition, file responsibility, and no fallback registration.
- Semantic Adequacy Gate proof with adversarial compiler/backend failures, positive end-to-end adapter run, and anti-stub audit.

## Browser Validation Logging

- `N/A`. Browser-visible adoption is SB12.

## Progression Gate

- SB12 cannot start until MAF is demonstrably an adapter and host composition uses isolated workflow/executor/template services.

## Suggested Agent Prompt

```text
Implement SB11 only. Isolate MAF workflow compiler/backend/invoker/normalizer/handoff code behind a MAF adapter project. Split large backend responsibilities, compose workflow runtime, templates, and executor catalog through explicit registrations, preserve behavior, add diagnostic tests and architecture checks, and capture Semantic Adequacy Gate proof. Do not perform API/UI/Workbench adoption.
```

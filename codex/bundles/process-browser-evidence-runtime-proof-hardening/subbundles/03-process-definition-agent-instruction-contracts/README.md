# SB03 Process Definition And Agent Instruction Contracts

## Status

- `Ready`

## Objective

Update generic software-delivery process definitions, work-brief generation, and agent instructions so UI/runtime QA steps demand process-visible browser artifacts and representative validation without putting product-specific details in process core.

## Covered Inputs

- `N006`: "processes core still must remain generic"
- `N007`: "detail should be in project strucure info ... skills and instructions ... process steps definitions"
- `R006`, `R007`, `R008`

## Prerequisites

- `SB01` progression gate must pass so exact browser artifacts can be recorded.
- `SB02` progression gate must pass so required browser proof can be validated and rejected when shallow.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `repo://src`

## Deliverables

- Generic process definition or seed updates so UI/browser QA steps declare concrete browser proof artifacts or typed evidence requirements.
- Agent instructions that require exact artifact paths, screenshot review, console diagnostics, and representative interaction assertions.
- Project-structure guidance hooks that can carry domain acceptance hints into QA prompts.
- Tests proving non-UI process steps are not forced into browser proof.
- Tests proving browser UI steps include artifact-backed proof contracts.

## Dependency Impact

- `SB04` needs these definitions and instructions to drive a live process through the corrected evidence path.
- Weak instructions here can reintroduce markdown-only proof even if the runtime validator is correct.

## Validation Depth

- `Process-definition and prompt-contract validation`
- Not the first critical foundation, but it is a downstream enforcement phase. It must include anti-hardcoding proof.

## Implementation Steps

1. Locate the seeded or synchronized source of the multi-team software-delivery definition.
   - Use `rg -n "Multi-team software delivery and release governance|Regression evidence pack|Repaired regression evidence pack|runtime or browser proof" src tests` during execution and record the exact source file once confirmed.
2. Update QA and repaired-QA evidence contracts to require exact generic managed artifact paths or typed proof categories for:
   - browser screenshot;
   - console log;
   - snapshot/DOM or evaluate output;
   - representative interaction summary.
3. Update agent prompt generation so QA agents know `.playwright-mcp` raw paths are not final process evidence unless imported.
4. Add project-structure acceptance hint propagation into browser proof prompts without product-specific process runtime checks.
5. Add prompt/definition tests for UI and non-UI scenarios.
6. Add source assertions or grep-based audit proving process core does not mention Tetris-specific runtime rules.

## Scope Exceptions

- If existing DB process definitions require a migration or reseed mechanism beyond code changes, document the operator step in `SB04` rather than hiding it.

## Do Not Do

- Do not create a Tetris-specific process definition as the default repair.
- Do not require browser proof for CLI, backend-only API, or non-UI process steps unless their own contracts require runtime/browser proof.
- Do not rely on agent politeness instructions without runtime validation from `SB02`.

## Acceptance Checklist

- Multi-team UI QA process steps require process-visible browser artifacts.
- Agent prompts include screenshot review and representative interaction instructions.
- Project-structure context can supply domain acceptance hints.
- Non-UI steps remain free of browser-proof gating unless explicitly required.
- Source audit finds no Tetris-specific process-core checks.

## Proof Required

- Targeted prompt/definition tests.
- Source assertions for process definition text and prompt output.
- Anti-hardcoding audit transcript.
- Execution report row showing `SB01` and `SB02` gates were reviewed before this phase.

## Browser Validation Logging

- Required analytics row: `SB03`, route `N/A prompt/definition`, viewport `N/A`, MCP evidence `N/A`, screenshots `N/A`, result based on tests and source audit.
- This subbundle affects browser-visible proof indirectly, so it must record the exact prompt/definition outputs that future browser validation will depend on.

## Progression Gate

- Do not start `SB04` until process definitions and prompts drive generic artifact-backed browser validation and anti-hardcoding proof is recorded.
- The execution report must include prompt/definition test results and the anti-hardcoding audit transcript.

## Suggested Agent Prompt

```text
Implement SB03 only. Update process definitions and agent instructions so browser QA steps require process-visible artifacts and representative validation. Keep the process core generic; project-specific facts must flow through project structure or step contracts. Prove with prompt/definition tests and an anti-hardcoding audit.
```

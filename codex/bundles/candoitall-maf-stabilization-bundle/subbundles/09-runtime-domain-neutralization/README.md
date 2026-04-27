# 09 - Runtime Domain Neutralization and Recovery Directive Cleanup

## Objective

Remove scenario-specific calculator guidance from the generic MAF runtime and move it into process/template/skill-specific recovery guidance.

## Primary files to inspect


- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs`
- Calculator/process template or seed files.
- Existing calculator/process mock tests.


## Required implementation tasks


1. Locate all calculator-specific text in generic runtime code.
2. Extract the generic repeated-tool guard into a domain-neutral service.
3. Move calculator-specific recovery guidance into one of:
   - process template instructions
   - scenario harness configuration
   - skill-specific recovery provider
   - process dispatcher recovery directive builder
4. Ensure the generic runtime can still report repeated tool invocation with neutral remediation.
5. Add a regression test that generic runtime source no longer contains calculator-specific strings.
6. Add a process/calculator regression test proving the scenario-specific guidance is still available where needed.


## Required tests


Unit tests:
- Generic repeated tool guard emits neutral message.
- Calculator-specific recovery provider emits calculator guidance only for calculator scenario/process/template.
- Generic runtime source/text does not contain `If this is the calculator process`.

Integration tests:
- Calculator process repair loop still works.
- A non-calculator process never receives calculator-specific recovery hints.


## Risks and constraints


- Removing hints without relocating them may regress calculator tests. Relocate first, then remove from runtime.


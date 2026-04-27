# 05 - Incremental MAF Workflow and Orchestration Alignment

## Objective

Align selected process/agent flows with MAF workflows and orchestrations without rewriting the process engine. The project already references MAF workflows and uses checkpointing for approvals, but process orchestration remains custom.

## Primary files to inspect


- `src/CanDoItAll.AgentFramework.Core/Execution/ExecutionCheckpointServices.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.*.cs`
- Process template/test fixtures, especially calculator/multi-role process tests.
- Any workflow or checkpoint abstractions in `src/CanDoItAll.AgentFramework.Core`.


## Required implementation tasks


1. Do not replace the process engine.
2. Identify one or two narrow MAF workflow alignment targets:
   - checkpointed process-step execution boundary
   - sequential agent subflow
   - concurrent review subflow
   - handoff from planner to implementer to reviewer
3. Implement an adapter/harness that maps process step context into a MAF workflow executor and maps validated results back into process state.
4. Extend checkpointing beyond pending approvals where it is useful and safe:
   - step started
   - tools completed
   - structured/finalizer output validated
   - process event emitted
5. Ensure existing process dispatcher remains the owner of process state and transitions.
6. Add tests proving the adapter does not regress the existing calculator/process automation flow.


## Required tests


Unit/component tests:
- Workflow adapter maps process input to typed workflow input.
- Workflow adapter maps typed result back to process event.
- Checkpoint payload includes process id, step id, run id, session key/state reference, and contract key.

Integration tests:
- Existing calculator or process mock flow still completes.
- A selected sequential subflow runs through MAF workflow harness.
- A checkpointed run can resume from a saved checkpoint in a test fixture.


## Risks and constraints


- MAF workflows should not become a second source of process truth.
- Avoid overfitting to calculator process; use it only as a regression fixture.


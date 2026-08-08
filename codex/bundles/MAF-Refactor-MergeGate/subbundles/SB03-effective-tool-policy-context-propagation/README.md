# SB03 — Effective tool-policy context propagation

        **Depends on:** SB02  
        **Required before merge:** Yes

        ## Goal

        Return and use the exact contributor-enriched policy context together with its decision.

        ## Required work

        1. Introduce a pipeline result containing EffectiveContext and Decision.
2. Use EffectiveContext for block guard, recoverable denial mapping, telemetry, logging, approval-path checks, and diagnostics.
3. Remove or isolate the contributor-bypassing IAgentToolInvocationPolicy implementation from the pipeline.
4. Replace ReferenceEquals contributor detection with explicit process enrichment validation against audit identity.
5. Require exact process run/step identity and required restriction fields for governed process evaluation.
6. Add end-to-end policy tests proving a process denial becomes the intended recoverable result.

        ## Primary files

        - `src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicyPipeline.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessToolInvocationPolicyContextContributor.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ToolGovernancePipelineAndApprovalLifecycleTests.cs`

        ## Acceptance

        - [ ] Downstream policy handling observes process run/step and process restrictions.
- [ ] Governed recoverable denials remain recoverable.
- [ ] An unrelated cloning contributor cannot satisfy the process contributor requirement.
- [ ] The MAF adapter remains process-semantic-free.
- [ ] Existing interactive and process tool-policy tests remain green.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.

## Execution contract

- **Owned finding:** MRG-003.
- **Proof tier:** Governed.
- **Progression gate:** SB04 unlocks only when every downstream policy consumer uses the exact effective context and process denial remains recoverable.
- **Reopen trigger:** Any consumer retains the original neutral context or process enrichment is inferred from object identity instead of typed audit identity/restrictions.

## C# Architecture Impact

Make contributor enrichment an explicit pipeline output while preserving provider-neutral MAF mapping.

## Boundary Ownership

Core owns pipeline/result contracts; Processes owns process enrichment; MAF consumes only neutral typed fields.

## Dependency Direction

Processes and MAF depend on Core contracts; Core never depends on Processes and MAF gains no process semantics.

## Pattern Decision

Use a typed pipeline result containing effective context and decision; reject ambient mutation or a decision-only return.

## Testability Contract

Direct pipeline tests plus MAF composition tests must distinguish a real process contributor from an unrelated cloning contributor.

## Partial Class Policy

Do not expand `AgentToolInvocationPolicy.cs` or add partial policy owners; keep the result cohesive and small.

## Architecture Proof Required

Governed failing-first/passing transcripts, all-consumer source assertions, recoverable-denial smoke, and MAF forbidden-semantic guard.

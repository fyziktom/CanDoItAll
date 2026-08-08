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

---
            key: multi-agent-chain
            id: 6220bfa1-fe75-51a5-a23b-47c0ed8fbfbd
            name: Workflow: Multi-Agent Chain
            group: workflow-orchestration
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: handoff, multi-agent, workflow
            promptTypes: architecture, plan, implementation, review, validation, migration
            blueprints: architecture-spec, implementation-plan, feature-implementation, validation-audit
            phases: discovery, architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Multi-Agent Chain
Organize this workflow as a chain of specialized agents:
1. architecture agent creates the design and constraints,
2. reviewer challenges the design and exposes risks,
3. planning agent converts it into milestones and checklists,
4. implementation agent delivers the code in slices,
5. validation agent gathers proof and performs the final audit.

Each downstream agent must inherit the outputs and unresolved risks from the previous one instead of rediscovering context from scratch.

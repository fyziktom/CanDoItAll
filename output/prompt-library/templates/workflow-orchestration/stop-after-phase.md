---
            key: stop-after-phase
            id: 6f42cb93-dd7f-5bb5-8077-a3534ba56bed
            name: Workflow: Stop After Phase
            group: workflow-orchestration
            blockKind: Constraint
            toolboxEligible: false
            recommended: true
            tags: gate, stop-rule, workflow
            promptTypes: architecture, plan, implementation, refactor, embedded, ui, validation
            blueprints: architecture-spec, implementation-plan, feature-implementation, safe-refactor, validation-audit, embedded-firmware-iteration, ui-ux-delivery
            phases: architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Phase Gate
After completing the current phase:
- run the required validation for that phase,
- summarize what changed,
- stop and wait for the next prompt or next instruction if this workflow is phase-gated.

Do not continue automatically into the next phase when the workflow is meant to be reviewed between steps.

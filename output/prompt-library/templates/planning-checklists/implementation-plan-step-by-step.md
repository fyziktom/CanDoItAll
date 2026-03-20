---
            key: implementation-plan-step-by-step
            id: 2af89b95-ad7f-589c-bd37-d6db39794f5c
            name: Planning: Step-by-Step Implementation Plan
            group: planning-checklists
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: checklist, implementation-plan, sequence
            promptTypes: plan, architecture, implementation, migration
            blueprints: implementation-plan, architecture-spec, feature-implementation, safe-refactor, embedded-firmware-iteration
            phases: planning
            stackTags: 
            templateTokens: target_feature_or_fix
            ---

            ## Implementation Plan
Create a step-by-step implementation plan for {{target_feature_or_fix}}.

For each step, include:
- the objective,
- files or modules likely involved,
- the validation that will prove the step is complete,
- any dependency on an earlier step.

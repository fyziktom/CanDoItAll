---
            key: checklist-update-loop
            id: 6cb92439-002a-530d-9f50-cb92afc4e72a
            name: Workflow: Checklist Update Loop
            group: workflow-orchestration
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: checklist, evidence, progress
            promptTypes: plan, implementation, refactor, bugfix, testing, validation, embedded
            blueprints: implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, test-strategy-and-automation, validation-audit, embedded-firmware-iteration
            phases: planning, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Checklist Update Loop
Maintain the active checklist during the session.

After each meaningful step:
- mark completed items,
- note what remains,
- record the validation that justified the state change.

Never report an item as done without naming the evidence.

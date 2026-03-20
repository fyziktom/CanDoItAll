---
            key: additive-refactor-first
            id: f9a46819-bc09-5053-935f-6b10bbf17f87
            name: Implementation: Additive Refactor First
            group: implementation-execution
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: additive, refactor, risk-reduction
            promptTypes: implementation, refactor, bugfix, migration
            blueprints: feature-implementation, safe-refactor, bugfix-with-regression-lock, performance-hardening
            phases: implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Additive Refactor First
When the work needs structural change, prefer this order:
1. introduce the new helper, contract, or seam,
2. wire it into the existing code with minimal behavior change,
3. switch behavior only after tests cover the new path.

Avoid large one-step rewrites unless the target area is already isolated and well tested.

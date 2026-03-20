---
            key: implement-in-small-slices
            id: 4f35eb31-c88d-5beb-9599-d04d08a3cfbc
            name: Implementation: Small Safe Slices
            group: implementation-execution
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: implementation, slices, verification
            promptTypes: implementation, refactor, bugfix, migration, embedded, ui
            blueprints: feature-implementation, safe-refactor, bugfix-with-regression-lock, embedded-firmware-iteration, ui-ux-delivery
            phases: implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Implementation Style
Implement the work in small, safe slices.

Each slice should:
- change one coherent behavior or structural step,
- keep the codebase buildable,
- be followed immediately by the closest relevant validation.

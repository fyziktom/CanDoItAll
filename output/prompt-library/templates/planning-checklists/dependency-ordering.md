---
            key: dependency-ordering
            id: ec579ec3-c769-53dc-94ed-10e480cf9f0e
            name: Planning: Dependency Ordering
            group: planning-checklists
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: dependencies, ordering, planning
            promptTypes: plan, architecture, implementation, migration
            blueprints: implementation-plan, architecture-spec, feature-implementation, safe-refactor
            phases: planning
            stackTags: 
            templateTokens: 
            ---

            ## Dependency Ordering
Order the work by dependency depth.

Start with:
- shared contracts and foundational abstractions,
- storage or protocol changes,
- thin wiring layers,
- user-facing surfaces and tests after the foundation exists.

If the order must differ, explain why.

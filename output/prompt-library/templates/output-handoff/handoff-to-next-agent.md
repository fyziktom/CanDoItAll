---
            key: handoff-to-next-agent
            id: 3bb914f8-df29-542a-b55a-de8d5e5c8f95
            name: Output: Handoff to Next Agent
            group: output-handoff
            blockKind: Delivery
            toolboxEligible: false
            recommended: false
            tags: continuity, handoff, next-agent
            promptTypes: architecture, plan, implementation, review, validation, migration, embedded
            blueprints: architecture-spec, implementation-plan, feature-implementation, safe-refactor, validation-audit, embedded-firmware-iteration
            phases: delivery
            stackTags: 
            templateTokens: 
            ---

            ## Handoff
When another agent will continue the work, include:
- the current state,
- artifacts or files they must read first,
- the next decision they must make,
- the constraints and risks they inherit.

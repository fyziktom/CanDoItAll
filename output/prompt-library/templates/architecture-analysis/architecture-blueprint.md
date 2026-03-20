---
            key: architecture-blueprint
            id: c8ff61a2-d52e-5725-81e7-a5c6362971ad
            name: Architecture: Blueprint
            group: architecture-analysis
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: architecture, blueprint, design
            promptTypes: architecture, plan, migration, review
            blueprints: architecture-spec, implementation-plan, validation-audit
            phases: architecture
            stackTags: 
            templateTokens: target_feature_or_system
            ---

            ## Architecture Blueprint
Produce an implementation-ready architecture for {{target_feature_or_system}}.

Cover:
- module or service boundaries,
- data flow and control flow,
- storage or state ownership,
- external interfaces or contracts,
- validation strategy and major risks.

The blueprint must be specific enough that another agent could implement it without redesigning it.

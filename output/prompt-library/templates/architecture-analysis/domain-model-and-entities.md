---
            key: domain-model-and-entities
            id: e92bafdb-77d1-5441-9ba3-446f605a9dfa
            name: Architecture: Domain Model and Entities
            group: architecture-analysis
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: data, domain-model, entities
            promptTypes: architecture, plan, implementation, migration
            blueprints: architecture-spec, implementation-plan, feature-implementation, embedded-firmware-iteration
            phases: architecture, planning
            stackTags: 
            templateTokens: target_feature_or_problem
            ---

            ## Domain Model
Define the domain model needed for {{target_feature_or_problem}}.

Make explicit:
- key entities or records,
- important identifiers and relationships,
- lifecycle states,
- invariants that must remain true.

Keep the model aligned with the existing system language instead of inventing a second vocabulary.

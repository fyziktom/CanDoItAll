---
            key: data-model-and-migration-design
            id: 84fb18f6-cf4f-5d1e-a408-800607952f6b
            name: Architecture: Data Model and Migration Design
            group: architecture-analysis
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: data-model, migration, persistence, schema
            promptTypes: architecture, plan, implementation, migration, performance
            blueprints: architecture-spec, implementation-plan, feature-implementation, performance-hardening
            phases: architecture, planning
            stackTags: 
            templateTokens: data_change_or_feature
            ---

            ## Data Model and Migration Design
Design the persistence changes for {{data_change_or_feature}}.

Cover:
- tables, documents, or persisted records affected,
- indexes and query paths,
- migration or seed strategy,
- backward compatibility and rollback concerns,
- test coverage needed across supported databases.

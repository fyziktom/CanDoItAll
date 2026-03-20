---
            key: stack-postgresql
            id: 9f46896c-6c66-5b1f-bb2c-43a853ed2f2e
            name: Stack: PostgreSQL
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: database, postgresql, schema
            promptTypes: architecture, plan, implementation, migration, performance, validation
            blueprints: architecture-spec, implementation-plan, feature-implementation, performance-hardening, validation-audit
            phases: architecture, planning, implementation, verification
            stackTags: postgresql, database
            templateTokens: 
            ---

            ## PostgreSQL Guidance
For PostgreSQL-backed work:
- design schemas and indexes around actual query paths,
- respect transaction and migration safety,
- be explicit about JSON, array, text search, or extension usage,
- confirm cross-environment behavior if the app also supports SQLite or in-memory testing.

---
            key: stack-sqlite
            id: 5a1a2019-fd4d-5853-bda5-80c66134331f
            name: Stack: SQLite
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: database, offline, sqlite
            promptTypes: architecture, plan, implementation, migration, testing, validation
            blueprints: architecture-spec, implementation-plan, feature-implementation, validation-audit, test-strategy-and-automation
            phases: architecture, planning, implementation, verification
            stackTags: sqlite, database
            templateTokens: 
            ---

            ## SQLite Guidance
For SQLite-backed work:
- remember the differences from PostgreSQL in typing, concurrency, and feature support,
- keep schema and query choices compatible with the intended runtime role,
- use it deliberately for local, test, or offline scenarios rather than as a silent stand-in.

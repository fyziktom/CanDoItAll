---
            key: stack-efcore
            id: c89c6ab9-0634-5bb1-bf45-73e87ec7c0f0
            name: Stack: EF Core
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: database, efcore, orm
            promptTypes: architecture, plan, implementation, refactor, migration, performance, validation
            blueprints: architecture-spec, implementation-plan, feature-implementation, safe-refactor, performance-hardening, validation-audit
            phases: architecture, planning, implementation, verification
            stackTags: efcore, .net, database
            templateTokens: 
            ---

            ## EF Core Guidance
For EF Core work:
- keep `DbContext` lifetime and ownership clear,
- shape entities and configurations explicitly,
- watch for N+1 patterns, tracking mistakes, and provider-specific drift,
- pair model changes with migrations, tests, and rollback notes.

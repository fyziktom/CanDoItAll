---
            key: toolbox-db-migration-dry-run
            id: 14847572-66c9-5ccb-8220-27525ebdacd2
            name: Toolbox: Database Migration Dry Run
            group: toolbox-snippets
            blockKind: Testing
            toolboxEligible: true
            recommended: false
            tags: database, dry-run, migration, toolbox
            promptTypes: implementation, migration, validation, performance, security
            blueprints: feature-implementation, implementation-plan, validation-audit, performance-hardening, security-hardening
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Database Migration Dry Run
Before declaring the data-layer work done:
- generate or apply the migration in a safe non-production environment,
- validate upgrade and downgrade behavior if supported,
- record the exact commands and any warnings,
- call out any provider-specific caveats.

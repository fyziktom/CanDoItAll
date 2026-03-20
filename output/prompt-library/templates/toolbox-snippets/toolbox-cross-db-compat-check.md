---
            key: toolbox-cross-db-compat-check
            id: 674221b0-20dc-5dfe-b504-7dee50634f53
            name: Toolbox: Cross-DB Compatibility Check
            group: toolbox-snippets
            blockKind: Testing
            toolboxEligible: true
            recommended: false
            tags: compatibility, database, postgresql, sqlite, toolbox
            promptTypes: implementation, migration, testing, validation
            blueprints: feature-implementation, implementation-plan, test-strategy-and-automation, validation-audit
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Cross-Database Compatibility
Validate this data-layer change across the supported database providers.

At minimum:
- note which providers were tested,
- identify provider-specific behavior or skipped coverage,
- avoid assuming PostgreSQL and SQLite behave identically.

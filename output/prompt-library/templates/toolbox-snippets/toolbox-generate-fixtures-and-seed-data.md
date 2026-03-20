---
            key: toolbox-generate-fixtures-and-seed-data
            id: 2a9776e9-88e4-58ee-9e43-2d5f0e823519
            name: Toolbox: Generate Fixtures and Seed Data
            group: toolbox-snippets
            blockKind: Testing
            toolboxEligible: true
            recommended: false
            tags: fixtures, seed-data, testing, toolbox
            promptTypes: implementation, bugfix, testing, validation, embedded, ui
            blueprints: feature-implementation, bugfix-with-regression-lock, test-strategy-and-automation, validation-audit, embedded-firmware-iteration, ui-ux-delivery
            phases: planning, implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Fixtures and Seed Data
Create focused fixtures or seed data for the scenarios being changed.

Prefer:
- the smallest data that still reproduces the behavior,
- deterministic values,
- fixtures that can be reused by unit, integration, or UI tests.

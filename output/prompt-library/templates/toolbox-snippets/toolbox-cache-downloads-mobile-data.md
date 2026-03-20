---
            key: toolbox-cache-downloads-mobile-data
            id: 188848e9-e26b-5ef3-9093-d4fe000d2ec8
            name: Toolbox: Cache Downloads to Save Mobile Data
            group: toolbox-snippets
            blockKind: Constraint
            toolboxEligible: true
            recommended: false
            tags: bandwidth, caching, mobile-data, toolbox
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: feature-implementation, test-strategy-and-automation, validation-audit, embedded-firmware-iteration
            phases: implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Cache-Aware Resource Usage
Be careful with network-heavy setup steps.

Reuse:
- dependency caches,
- Docker layers,
- browser caches,
- package manager caches,
whenever it is safe to do so.

The goal is to save bandwidth and mobile data without compromising reproducibility.

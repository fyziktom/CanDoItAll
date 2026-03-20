---
            key: keep-public-surface-minimal
            id: 872c3dec-82af-5d29-8eda-18795b57b7e6
            name: Implementation: Keep Public Surface Minimal
            group: implementation-execution
            blockKind: Constraint
            toolboxEligible: false
            recommended: false
            tags: api-surface, contracts, maintainability
            promptTypes: implementation, refactor, architecture, security, embedded
            blueprints: feature-implementation, safe-refactor, architecture-spec, security-hardening, embedded-firmware-iteration
            phases: architecture, implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Public Surface Control
Keep the public surface area minimal.

Only expose new types, members, endpoints, or settings when they are genuinely required by the use case.
Prefer internal seams over expanding the public API without a clear consumer.

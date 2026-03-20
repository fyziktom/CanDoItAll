---
            key: preserve-backward-compatibility
            id: 1816a5cd-778b-562b-8729-aca0498ec90e
            name: Guardrail: Preserve Backward Compatibility
            group: guardrails
            blockKind: Constraint
            toolboxEligible: false
            recommended: true
            tags: compatibility, contracts, migrations
            promptTypes: architecture, implementation, refactor, bugfix, migration
            blueprints: architecture-spec, feature-implementation, safe-refactor, bugfix-with-regression-lock, embedded-firmware-iteration
            phases: planning, implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Backward Compatibility
Preserve existing contracts, persisted data, and user-visible behavior unless the prompt explicitly calls for a breaking change.

If a breaking change is required:
- call it out explicitly,
- add migration or compatibility handling,
- document the risk and the rollback path.

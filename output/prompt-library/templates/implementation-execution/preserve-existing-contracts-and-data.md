---
            key: preserve-existing-contracts-and-data
            id: e7b9c4f8-e670-5ff0-9d7a-83776560ef37
            name: Implementation: Preserve Existing Contracts and Data
            group: implementation-execution
            blockKind: Constraint
            toolboxEligible: false
            recommended: false
            tags: compatibility, contracts, data
            promptTypes: implementation, refactor, bugfix, migration, embedded
            blueprints: feature-implementation, safe-refactor, bugfix-with-regression-lock, embedded-firmware-iteration
            phases: implementation, verification
            stackTags: 
            templateTokens: target_change
            ---

            ## Contract and Data Preservation
While implementing {{target_change}}, preserve existing data and protocol expectations unless a planned migration says otherwise.

Check:
- serialized payloads,
- storage format or schema assumptions,
- API consumers,
- user workflows that depend on the existing behavior.

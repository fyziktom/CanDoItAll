---
            key: mandatory-integration-tests
            id: b942715a-a9ec-55e8-b97e-1143dcda1314
            name: Validation: Mandatory Integration Tests
            group: validation-review
            blockKind: Testing
            toolboxEligible: false
            recommended: false
            tags: boundaries, contracts, integration-tests
            promptTypes: implementation, migration, testing, validation, security, performance
            blueprints: feature-implementation, test-strategy-and-automation, validation-audit, security-hardening, performance-hardening
            phases: implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Integration or Contract Tests
Add integration or contract tests wherever behavior crosses process, storage, or module boundaries.

Use them to cover:
- database behavior,
- API contracts,
- filesystem or network integration,
- service composition that unit tests cannot prove.

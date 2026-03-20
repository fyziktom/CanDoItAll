---
            key: toolbox-run-integration-tests-docker
            id: 3bb7003f-c041-5a39-9440-882f9a1519e9
            name: Toolbox: Run Integration Tests in Docker
            group: toolbox-snippets
            blockKind: Testing
            toolboxEligible: true
            recommended: false
            tags: docker, integration-tests, toolbox
            promptTypes: implementation, migration, testing, validation, security, performance
            blueprints: feature-implementation, test-strategy-and-automation, validation-audit, security-hardening, performance-hardening
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Integration Tests in Docker
Run the integration or contract test suite inside Docker.

Requirements:
- start only the dependencies the tests truly need,
- use persistent caches and volumes where safe to reduce repeated downloads,
- record the exact command and the services started,
- if external dependencies make Docker validation impossible here, explain the blocker and the closest fallback.

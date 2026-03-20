---
            key: toolbox-run-unit-tests-docker
            id: 091484b8-3a88-5e4f-8422-9e458d0bbc86
            name: Toolbox: Run Unit Tests in Docker
            group: toolbox-snippets
            blockKind: Testing
            toolboxEligible: true
            recommended: false
            tags: docker, mobile-data, toolbox, unit-tests
            promptTypes: implementation, bugfix, refactor, testing, validation
            blueprints: feature-implementation, bugfix-with-regression-lock, safe-refactor, test-strategy-and-automation, validation-audit
            phases: verification, delivery
            stackTags: 
            templateTokens: docker_compose_file_or_dockerfile
            ---

            ## Unit Tests in Docker
You must run the unit test suite inside Docker before declaring this work done.

Requirements:
- use {{docker_compose_file_or_dockerfile}} if available, otherwise create the smallest viable temporary test container,
- reuse package, image, and layer caches whenever possible to reduce network transfer and save mobile data,
- print the exact Docker command, target test projects, and result summary,
- if Docker validation is blocked, say so clearly and fall back to the nearest reproducible local command without pretending Docker passed.

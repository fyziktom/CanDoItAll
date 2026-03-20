---
            key: toolbox-run-ui-tests-docker
            id: 97467063-0452-5f05-b1a3-6df0346e210f
            name: Toolbox: Run UI Tests in Docker
            group: toolbox-snippets
            blockKind: Testing
            toolboxEligible: true
            recommended: false
            tags: docker, mobile-data, playwright, toolbox, ui-tests
            promptTypes: implementation, bugfix, ui, testing, validation
            blueprints: feature-implementation, bugfix-with-regression-lock, ui-ux-delivery, test-strategy-and-automation, validation-audit
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## UI Tests in Docker
Run the UI or Playwright suite inside Docker.

Requirements:
- install only the browsers and system dependencies actually needed,
- capture screenshots, traces, or reports for failures,
- reuse browser and package caches where possible to save bandwidth,
- include the exact command and artifact locations in the final output.

---
            key: stack-playwright-mcp
            id: 085189e5-ae6e-5482-acd7-f443ac8e0751
            name: Stack: Playwright MCP
            group: stack-profiles
            blockKind: Testing
            toolboxEligible: false
            recommended: false
            tags: browser-automation, mcp, playwright, testing
            promptTypes: testing, validation, ui, implementation, bugfix
            blueprints: test-strategy-and-automation, validation-audit, ui-ux-delivery, feature-implementation, bugfix-with-regression-lock
            phases: implementation, verification, delivery
            stackTags: playwright, mcp
            templateTokens: 
            ---

            ## Playwright MCP Guidance
When browser automation is useful:
- use Playwright MCP or the closest real-browser path instead of shallow DOM reasoning,
- capture screenshots, traces, or recordings when they materially improve proof,
- validate key flows end to end instead of only checking that elements render.

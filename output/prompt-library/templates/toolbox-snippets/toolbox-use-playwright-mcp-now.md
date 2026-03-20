---
            key: toolbox-use-playwright-mcp-now
            id: e5b8e812-2850-557d-9afe-4da0440d778d
            name: Toolbox: Use Playwright MCP Now
            group: toolbox-snippets
            blockKind: Testing
            toolboxEligible: true
            recommended: false
            tags: browser, mcp, playwright, toolbox
            promptTypes: ui, bugfix, testing, validation
            blueprints: ui-ux-delivery, bugfix-with-regression-lock, test-strategy-and-automation, validation-audit
            phases: verification
            stackTags: 
            templateTokens: 
            ---

            ## Use Playwright MCP
Validate this UI flow with Playwright MCP or the closest real browser automation path available.

Do not rely only on reading the code when:
- interaction timing matters,
- canvas or drag/drop behavior matters,
- responsive behavior matters,
- the reported bug is visual or browser-state dependent.

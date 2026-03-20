---
            key: toolbox-capture-browser-artifacts
            id: 1c02e351-64c0-5aaa-8c89-fa2921a134bb
            name: Toolbox: Capture Browser Artifacts
            group: toolbox-snippets
            blockKind: Delivery
            toolboxEligible: true
            recommended: false
            tags: artifacts, screenshots, toolbox, trace
            promptTypes: ui, testing, validation, bugfix
            blueprints: ui-ux-delivery, test-strategy-and-automation, validation-audit, bugfix-with-regression-lock
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Capture Browser Artifacts
Collect browser evidence for the key scenarios:
- screenshots for final or failing states,
- traces when interaction or timing bugs matter,
- logs or network evidence when data flow matters,
- video only when it materially improves diagnosis.

Include the saved artifact paths in the final output.

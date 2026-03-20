---
            key: stack-html-js-css
            id: b110a4ce-6a21-5f03-a73c-01a57bb28f63
            name: Stack: HTML/JS/CSS
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: css, frontend, html, javascript
            promptTypes: architecture, implementation, bugfix, ui, testing, validation
            blueprints: feature-implementation, bugfix-with-regression-lock, ui-ux-delivery, validation-audit
            phases: planning, implementation, verification
            stackTags: html, javascript, css
            templateTokens: 
            ---

            ## HTML, JavaScript, and CSS Guidance
For direct frontend work:
- respect the existing asset pipeline and project structure,
- keep behavior, layout, and styling responsibilities clear,
- test edge states such as small screens, long content, and async failures,
- avoid adding heavy dependencies unless the prompt explicitly allows them.

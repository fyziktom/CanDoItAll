---
            key: stack-canvas-html-js
            id: 4b6ab7f1-c2ee-587e-8a8a-f3fa4deceb19
            name: Stack: Canvas in HTML/JS
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: canvas, html5, javascript, rendering
            promptTypes: architecture, implementation, bugfix, ui, performance, validation
            blueprints: architecture-spec, feature-implementation, bugfix-with-regression-lock, ui-ux-delivery, performance-hardening, validation-audit
            phases: architecture, planning, implementation, verification
            stackTags: canvas, html, javascript
            templateTokens: 
            ---

            ## Canvas Guidance
For HTML5 canvas work:
- keep the actual interactive surface canvas-first if that is the product intent,
- use DOM only where it is clearly the correct tool,
- make hit testing, coordinate transforms, resize behavior, and redraw costs explicit,
- reuse existing canvas primitives before inventing a parallel rendering stack.

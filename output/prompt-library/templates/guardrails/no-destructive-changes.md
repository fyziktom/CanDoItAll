---
            key: no-destructive-changes
            id: c9f117c9-af66-5d66-9c87-82f9622ab028
            name: Guardrail: No Destructive Changes
            group: guardrails
            blockKind: Constraint
            toolboxEligible: false
            recommended: true
            tags: destructive-actions, git, safety
            promptTypes: implementation, refactor, bugfix, review, embedded
            blueprints: feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, embedded-firmware-iteration
            phases: implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Destructive Change Ban
Do not use destructive commands or irreversible cleanup unless the prompt explicitly authorizes them.

Avoid:
- deleting user work or unreviewed changes,
- force-resetting branches or databases,
- throwing away fixtures, logs, or screenshots that still matter for proof.

---
            key: protect-secrets-and-sensitive-data
            id: 22d411f3-002f-5192-8386-97b25a780ae9
            name: Guardrail: Protect Secrets and Sensitive Data
            group: guardrails
            blockKind: Security
            toolboxEligible: false
            recommended: true
            tags: privacy, secrets, security
            promptTypes: architecture, implementation, review, security, validation
            blueprints: architecture-spec, feature-implementation, senior-code-review, security-hardening, validation-audit
            phases: architecture, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Secret and Sensitive Data Handling
Do not expose secrets, tokens, private keys, or raw sensitive values in prompts, logs, screenshots, or generated code.

If the work touches authentication, billing, telemetry, or user data:
- keep secrets server-side when possible,
- redact sensitive values from examples and output,
- call out any unsafe storage or transport pattern you encounter.

# Assumptions And Risks

## Assumptions

- The connected Office365 account and category exist in the local running app, as stated by the user.
- The selected provider for the seeded workflow is expected to support structured output because `WorkflowExampleCatalogSeedService` prefers structured-output providers for managed workflow examples.
- The correct implementation is runtime hardening, not prompt-only wording. Prompt-only fixes are insufficient because the current failure already occurred despite JSON instructions.

## Critical Path Risks

- A provider selected without structured output support could still be used for a JSON-required workflow component. The hardening must fail early and explain the provider capability mismatch rather than silently falling back to prompt-only JSON.
- Existing persisted workflow components may not be reseeded immediately. Runtime-level response-format enforcement must apply to already persisted components based on their current `WorkflowModelSettings`.
- Overly narrow tests could pass by checking only non-null options. Tests must prove JSON-required workflow components request JSON response format and invalid raw JSON is still rejected.

## Validation Risks

- Live validation through `http://localhost:5032` may require an authenticated browser session or an internal API shape that is not discoverable from static code alone.
- A real Office365 run mutates message categories. Validation should prefer the existing test category and record any observed live mutation path carefully.
- Provider/network calls can be slow or unavailable. Unit and source-level proof must still close the runtime hardening subbundle; live proof remains required unless explicitly blocked.

## Reopen Triggers

- Any test or live run shows `summarize-office365` still calling MAF without JSON response format.
- Any implementation extracts, repairs, or trims malformed JSON into validity after the model returns it.
- Any downstream project-structure storage loses `projectId`, `nodeId`, or `runContext.office365Processing`.
- Any live validation fails at the same `ValidateJsonPayload` malformed JSON path after the runtime hardening is built.

# Secret Vault Storage And Runtime References

This bundle is a coordination and execution package for `secret-vault-storage`.

## Profile

- `initiative`

## Mission

- Move secrets behind a real vault boundary, make Windows DPAPI the default local provider, keep future platform providers explicit, and wire secret references into the existing settings, agent, workflow, and project-structure surfaces without persisting raw values outside the vault.

## Outcome Contract

- Requested outcome: existing `SecretRecord` metadata remains the catalog, encrypted secret payloads move through an `ISecretVault` abstraction with DPAPI used on Windows by default, runtime consumers resolve secret values only for explicitly allowed references, and UI surfaces offer safe selection, copy, and time-bounded reveal behavior.
- Hard constraints: no raw secret persistence in appsettings, workflow JSON, agent configuration, project-structure metadata, logs, screenshots, or activity text; do not silently fall back from a requested unsupported vault provider; keep provider selection strongly typed; prefer BaseLib components over page-local password/copy markup; keep non-Windows providers as explicit not-implemented stubs until real platform bindings exist.
- Evidence required before closure: focused vault unit tests, workflow HTTP secret-resolution tests, agent/provider credential resolution tests, BaseLib component or build proof for the time-bound secret field, browser proof for settings secret editor and project-structure secret dialog, and updated documentation in `docs/secure-configuration.md`.
- Known blockers or explicit scope exceptions: MAUI, macOS Keychain, Linux Secret Service, Azure Key Vault, and HashiCorp Vault integrations are interface-ready but unsupported in this bundle; production rotation/audit workflows are documented follow-up, not implemented here.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-secret-vault-contract-and-dpapi-foundation`
2. `subbundles/02-02-secret-catalog-service-and-runtime-resolution`
3. `subbundles/03-03-agent-workflow-and-project-secret-reference-surfaces`
4. `subbundles/04-04-baselib-secret-field-and-picker-ui`
5. `subbundles/05-05-validation-documentation-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed with documented scope exceptions`
- Final closure gate: `Passed`
- Browser validation analytics: `Completed for settings and project-structure; workflow UI selector partially blocked by canvas interaction, with runtime coverage from unit tests`

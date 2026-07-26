# Missing Partner API Contracts and SharedInfo Synchronization

Durable implementation state for the seven partner-backend API gaps reported against
CanDoItAll commit `065f31e0b527bcda2d499daf39e8a901e0231323`, followed by the
required SharedInfo OpenAPI, API-skill, documentation, validation, and installed-skill
synchronization.

## Profile

- `initiative`

## Mission

- External backend clients can safely provision/import agents, request portable structured
  output, resolve and launch workflows by stable identities with retry safety, generate
  typed clients from complete OpenAPI response schemas, and retain typed agent-interview
  evidence. SharedInfo must describe the shipped contract from a fresh canonical OpenAPI
  snapshot.

## Outcome Contract

- Requested outcome: implement every owned raw note and improve the related SharedInfo
  API skills and documentation.
- Hard constraints: no server-path contract for remote package upload; no raw provider
  secret import; tenant/workspace boundaries remain enforced; stale writes fail closed;
  retries do not duplicate agents or workflow runs; public schemas are portable; original
  notes remain traceable.
- Evidence required before closure: affected builds; targeted positive, adversarial,
  authorization, concurrency, and retry tests; generated OpenAPI schema assertions; fresh
  SharedInfo snapshot/provenance; SharedInfo and skill-package validators; raw-note closure.
- Known blockers or explicit scope exceptions: no UI work is in scope. Existing unrelated
  `System.Security.Cryptography.Xml 10.0.7` vulnerability warnings are baseline evidence,
  not authorization for a package-upgrade initiative.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries when architecture decisions are material
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts when repeated handoff needs them
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `01-remote-agent-package-import`
2. `02-agent-external-key-provisioning`
3. `03-portable-json-schema-output`
4. `04-workflow-stable-key-lookup`
5. `05-workflow-run-idempotency`
6. `06-agent-recruiting-evidence`
7. `07-openapi-response-contracts`
8. `08-sharedinfo-api-skills-and-docs`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## UI Target Policy

- CanDoItAll applications target large-screen desktop use; do not add small/medium/mobile tuning unless explicitly requested.
- Reusable basic `CanDoItAll.Components.BaseLib` components remain responsible for small, medium, and large viewport behavior.

## Validation Summary

- Bundle preparation status: `Prepared and validated`
- Execution status: `Complete — SB01 through SB08 closed`
- Subbundle gate review: `Pass — all eight sequential closure gates passed`
- Final closure gate: `Pass — product, OpenAPI, SharedInfo, installed-skill, and raw-note evidence agree`
- Browser validation analytics: `N/A - HTTP API and documentation work only`

## Final Outcome

- N001-N007 are implemented and behaviorally verified.
- R008 is published through SharedInfo and synchronized into the active Codex skill root.
- The canonical artifact contains 229 paths, 274 operations, and 342 schemas at SHA-256
  `A5D9EE04B93A5913CB3AF7004B1F91F7F85A6639CF911F2BA2258316C778B51C`.
- Product source provenance records the current branch and baseline HEAD plus
  `workingTreeClean: false`; no commit or staging action was performed.

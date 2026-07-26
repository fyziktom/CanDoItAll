# Structured Input

## Core Objective

- Close all seven proposed partner API gaps against current source and publish the exact
  contract through SharedInfo reusable API assets.

## Success Criteria

- Each note is `Solved`, `Partially solved`, or `Not solved` with exact proof.
- Positive and adversarial tests prove retry, concurrency, stale-version, security,
  validation, typed-evidence, and OpenAPI behavior.
- A non-.NET client can use portable request/response contracts without inspecting source.
- SharedInfo snapshot, manifest, route appendices, contract references, and validators agree
  with the shipped Web API.

## Hard Constraints

- Preserve source notes verbatim and preserve all current authorization boundaries.
- Never accept or persist imported provider secrets.
- Use bounded package and JSON Schema inputs.
- Use stable keys only inside the current tenant/workspace scope.
- Do not silently overwrite stale agent/workflow state or replay a changed request.
- Do not expose EF entities as the new public contract.
- Do not add UI or mobile work.

## Allowed Side Effects

- CanDoItAll production API, model, service, persistence, composition, and targeted test
  files required by subbundles 01-07.
- CanDoItAll.SharedInfo standards-adjacent API documentation, reusable API skills, shared
  OpenAPI snapshot/provenance, and validation required by subbundle 08.
- Active Codex skill-root synchronization after SharedInfo validation.

## Source Artifacts

- `inputs/raw/README.md`
- `inputs/raw/001-remote-agent-package-import.md`
- `inputs/raw/002-agent-upsert-by-external-key.md`
- `inputs/raw/003-agent-json-schema-output-contract.md`
- `inputs/raw/004-workflow-lookup-by-template-key.md`
- `inputs/raw/005-workflow-run-idempotency.md`
- `inputs/raw/006-openapi-response-schemas.md`
- `inputs/raw/007-agent-interview-evidence-api.md`

## Input Coverage Signals

- N001 remote agent package bytes import.
- N002 external-key agent provisioning.
- N003 portable JSON Schema output.
- N004 stable workflow lookup and provenance.
- N005 public workflow launch idempotency.
- N006 complete response/error OpenAPI schemas.
- N007 canonical agent interview/evaluation evidence.

## Dependency And Sequencing Signals

- SB01-SB03 share agent API/model ownership and execute serially.
- SB04-SB05 share workflow API/model ownership and execute serially.
- SB06 may reference agent execution, workflow, process, and prompt identifiers but must not
  create inward project references from their owning cores.
- SB07 depends on every new runtime contract.
- SB08 depends on the final generated OpenAPI document from SB07.

## Validation Expectations

- Targeted integration/unit tests must include one realistic positive and one adversarial
  negative per Behavioral subbundle.
- OpenAPI tests must assert response schema references, not only route existence.
- Authorization tests must prove protected routes remain protected when access is enabled.
- Concurrency/retry tests must prove one durable resource/run for identical submissions.

## Evidence Contract

- SB01-SB07: `Behavioral`.
- SB08: `Standard` plus canonical host capture because it publishes generated contract data.
- Exact commands and results are recorded in `reviews/01-execution-report.md`.

## UI Validation Strategy

- N/A. No browser-rendered UI behavior is changed.

## Browser Validation Analytics

- N/A. HTTP host/OpenAPI checks are recorded as host proof, not screenshot proof.

## Working Assumptions

- Current source, not the pinned baseline, decides whether a note is missing or partially
  implemented.
- The existing sandbox workspace document remains the canonical agent catalog persistence
  boundary unless implementation evidence forces a smaller contract extraction.
- Existing CRM-HR interviews are a different, application-centered projection and do not
  by themselves close the agent-evidence note.

## Primary Risks

- Large `AgentsApi.cs` and `WorkflowsApi.cs` files can absorb more unrelated behavior.
- External identity/idempotency ledgers can become non-atomic if stored outside the owning
  catalog/run transaction.
- Portable schema validation can be fake if only metadata is stored and result bytes are
  not actually validated.
- OpenAPI can appear complete while runtime serialization differs.

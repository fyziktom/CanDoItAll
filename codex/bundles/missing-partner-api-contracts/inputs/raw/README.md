# CanDoItAll missing API commands for partner backend integrations

> **Status: proposed, not implemented.** These notes were discovered while preparing the
> backend-only partner examples on 2026-07-25. CanDoItAll is high-WIP; re-check the
> intended host and remove or revise a note when its contract changes.

Contract baseline:

- CanDoItAll commit: `065f31e0b527bcda2d499daf39e8a901e0231323`
- OpenAPI SHA-256:
  `324A90A694B67FF341AE951FCEE6C5447B58D0E603268EF78250E7554C2C2118`
- partner example package: sibling folder `candoitall-partners-agents-pack`

## Proposed commands

| ID | Gap | Priority | Current example workaround |
| --- | --- | --- | --- |
| [001](001-remote-agent-package-import.md) | upload/import an agent package from an external client | high | send a full `AgentEditorModel` to `POST /api/agents` |
| [002](002-agent-upsert-by-external-key.md) | idempotent external agent provisioning | high | list agents and stop on an exact-name duplicate |
| [003](003-agent-json-schema-output-contract.md) | portable JSON Schema structured output | high | request JSON in instructions and validate `responseText` externally |
| [004](004-workflow-lookup-by-template-key.md) | stable workflow lookup by template key | high | exact-name match, then pin workflow/version IDs |
| [005](005-workflow-run-idempotency.md) | idempotent workflow run submission | high | partner-side request ledger before calling start |
| [006](006-openapi-response-schemas.md) | documented response DTOs for client generation | high | source inspection and runtime contract tests |
| [007](007-agent-interview-evidence-api.md) | canonical interview/scorecard links to execution evidence | high | partner-owned recruiting scorecard |

## Triage rule

A note belongs here only when the external backend flow needs a public contract and the
pinned API does not provide one. UI convenience requests and speculative product features
are intentionally excluded.

When implementing a note:

1. update the API and source models;
2. regenerate OpenAPI;
3. add positive, negative-authorization, concurrency, and retry tests;
4. update the partner pack example to use the new contract;
5. mark the note implemented with commit and OpenAPI hash—do not silently delete history.

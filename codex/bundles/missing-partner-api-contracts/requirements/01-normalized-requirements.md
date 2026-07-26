# Normalized Requirements

## N001 / R001 Remote Package Import

- Accept bounded `multipart/form-data` package bytes without a server filesystem path.
- Validate archive extension/signature, entry count, expanded size, traversal, symlink or
  executable entries, schema/hash, and secret-bearing provider material before mutation.
- Support `create`, `replace-exact-version`, and `clone`, stable external identity,
  optional expected hash/version, and `Idempotency-Key`.
- Return agent id, imported version/hash, created/replayed disposition, unresolved
  prerequisites, and warnings.

## N002 / R002 External-Key Agent Provisioning

- Provide tenant/workspace-scoped GET and PUT by normalized namespace/key plus guarded
  archive/delete.
- Atomically bind one key to one agent and persist an idempotency claim containing request
  fingerprint and result.
- Identical concurrent retries return one agent; changed payload with stale version or
  reused idempotency key returns 409 without mutation.

## N003 / R003 Portable JSON Schema Output

- Public execution requests accept a versioned `json-schema` DTO with name, schema, and
  strict flag and never serialize `.NET Type`.
- Enforce bounded schema bytes/depth/property/keyword complexity before billable execution.
- Preserve exact canonical schema/hash and raw provider output with the run.
- Return parsed data and distinguish success, provider refusal, malformed JSON, and
  schema-validation failure.

## N004 / R004 Stable Workflow Lookup

- Catalog/detail responses expose system template provenance and partner external identity.
- Provide template-key lookup and external namespace/key filtering with workspace-local
  uniqueness and explicit multiple/stale materialization outcomes.
- Clients can pin a concrete runnable version without display-name matching.

## N005 / R005 Workflow Launch Idempotency

- Accept `Idempotency-Key` on both public workflow start routes.
- Scope the key to the current workspace/tenant and fingerprint workflow/version/backend/
  canonical input.
- Identical concurrent/post-timeout replays return one run and report replay disposition;
  changed replays return 409.
- Provide lookup by idempotency key with safe key/hash, original run id and state.

## N006 / R006 Complete OpenAPI Responses

- Explicit public success DTOs and relevant Problem Details/error schemas are published for
  the named agent and workflow endpoints and every new endpoint in this bundle.
- Required/nullable members and enum encoding match runtime JSON.
- Generated-client contract tests assert named response schema references and deserialize
  representative runtime payloads.

## N007 / R007 Agent Interview Evidence

- Add canonical interview, attempt, review, detail, and candidate-readiness resources for
  agent candidates.
- Attempts reference exactly one typed agent execution, workflow run, or process run plus
  challenge/rubric versions and immutable input/output hashes.
- Preserve repeatable attempts, automated evaluator identity/provider/model, human
  approval, incomplete evidence, and visibility scope.
- Production readiness requires a qualifying human authorization and never activates an
  agent by itself.

## R008 SharedInfo Publication

- Refresh the canonical OpenAPI artifact/provenance/counts/hash from a clean current commit.
- Update agents, workflows, CRM-HR/agent-recruiting guidance, route appendices, contract
  references, validator operation sets, and relevant Codex documentation.
- Run SharedInfo and current skill/package validation, then synchronize changed packages
  into the active Codex skill root and verify hashes.

# Assumptions And Risks

## Assumptions

- The current Web host's existing `/api` authorization group protects every new route.
- Workspace/profile scope is the tenant boundary currently available to agent/workflow
  services; do not invent a second tenant model.
- Stable hashes use canonical UTF-8 JSON and SHA-256.
- Opaque versions may be derived from durable update/version fields but never from mutable
  display names.

## Critical Path Risks

- SB02 and SB05 are critical foundations because atomic identity/idempotency decisions
  control duplicate prevention.
- SB03 is critical because provider execution must preserve and validate the exact schema,
  not merely accept a JSON property.
- SB06 is critical because evidence links must remain typed without coupling CRM-HR,
  Prompt Gallery, Processes, Workflows, and Agent runtime projects cyclically.
- SB07/SB08 become untrustworthy if any earlier route or DTO changes afterward.

## Validation Risks

- The canonical host may need a fresh build and database profile before OpenAPI capture.
- Concurrency tests that use sequential requests do not prove the acceptance criteria.
- OpenAPI `$ref` existence alone does not prove runtime payload parity; contract tests must
  deserialize representative responses.

## Reopen Triggers

- Reopen SB01 if multipart import bypasses archive validation or leaks provider secrets.
- Reopen SB02/SB05 if parallel tests create duplicates or changed-payload replays succeed.
- Reopen SB03 if unsupported/provider-refusal/malformed/schema-invalid outcomes collapse
  to one status or raw evidence disappears.
- Reopen SB04 if multiple/stale materializations cannot be detected.
- Reopen SB06 if readiness can bypass human authorization or evidence visibility scope.
- Reopen SB01-SB06 if SB07 reveals runtime/OpenAPI type mismatch.
- Reopen any product subbundle if SB08 cannot document it from the generated contract.

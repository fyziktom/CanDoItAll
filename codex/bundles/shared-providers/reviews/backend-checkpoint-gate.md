# Backend checkpoint gate

Owner: SB07  
Initial result: `LOCKED`

UI cannot begin until this gate is `PASS`.

## Required evidence

- publication policy and sanitized catalog;
- auth scopes and errors;
- ETag/304;
- model routing collision proof;
- Responses and Chat Completions normal/streaming;
- function tool call round-trip;
- structured output allow/deny;
- image generation including ComfyUI mapping;
- access context accepted/validated/audited/not upstream;
- usage completeness/cost semantics;
- source URI and source identity policy;
- selection/reconciliation/stable local IDs;
- shared plus personal profiles;
- central outage/unpublish/recovery/no fallback;
- three separate CanDoItAll application containers;
- real PostgreSQL and deterministic upstream;
- secret/content redaction scan;
- current architecture gate.

## Decision

- Result: `LOCKED`
- Scenario artifact:
- Container status artifact:
- Blocking failures:
- UI unlock:

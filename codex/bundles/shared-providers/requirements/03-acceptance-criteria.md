# Acceptance criteria

## Backend acceptance gate: SB07

UI remains locked until all of the following are proven:

1. Central publication defaults to off and only eligible profiles can be published.
2. Catalog response contains no secret/internal fields and supports ETag/304.
3. Catalog and inference scopes are independently enforced.
4. OpenAI-compatible model list routes only public model IDs.
5. Chat Completions and Responses non-streaming calls traverse:
   client app -> central app -> deterministic upstream -> central -> client.
6. Streaming starts before upstream completion, survives multiple chunks, includes terminal
   semantics, and cancels on disconnect.
7. Function tool definitions and tool calls round-trip without central execution.
8. Structured output succeeds only for an advertised capable publication and fails otherwise.
9. Image generation succeeds through an OpenAI image provider fixture and a ComfyUI-style
   adapter fixture, returning the requested supported response format.
10. Client source sync creates selected imports, is idempotent, preserves local provider IDs,
    and supports two independent clients.
11. Personal provider and imported provider coexist.
12. Unpublish, central outage, invalid token, missing scope, source identity mismatch, duplicate
    model names, malformed access context, unsupported fields, and unavailable capabilities
    have explicit tested behavior.
13. Access context reaches central audit but not the deterministic upstream.
14. Audit has no content or secrets and usage completeness is truthful.
15. One central and two client app containers use separate PostgreSQL databases and are
    independently queryable.

## UI acceptance gate: SB09

1. Central publication action and eligibility explanation render correctly.
2. Source add/edit/test flow is usable in the supported desktop viewport.
3. Catalog discovery and multi-select import are usable without leaving ambiguous state.
4. Imported provider origin, availability, and read-only ownership are clear.
5. Existing local provider create/edit/delete behavior remains intact.
6. Normal, loading, empty, unauthorized, unavailable, stale, and conflict states are covered.
7. Component tests and focused Playwright tests pass.
8. Normal and relevant open-overlay screenshots are inspected and recorded.
9. The first viewport and scroll owner match the compact desktop composition decision.

## Final acceptance gate: SB12

1. A single final stable aggregate passes.
2. Documentation validators pass.
3. OpenAPI JSON endpoints are byte-identical where repository convention requires it.
4. SharedInfo snapshot manifest, hashes, operation counts, route appendices, and new skill pass.
5. Final clean multi-instance E2E passes.
6. Central, client-a, client-b, PostgreSQL, and deterministic upstream remain healthy and
   running.
7. Manual handoff contains no secret value but tells the operator where ephemeral credentials
   are stored locally.
8. Every FR/NFR has a closure classification and evidence.

# SB04 independent C# security and relay review

Final result: `PASS`. The final audit found no remaining SB04 code or test blocker.

The review was deliberately iterative. It did not treat zero-warning builds or the frozen test
counts as sufficient when request shapes, operation-specific usage, persistence constraints, or
proof boundaries remained semantically incomplete. Full chronology is retained in
`semantic-reopen-review.md`.

## Initial closure blockers repaired

- Chat `image_url` and Responses `input_image` lacked exact outer sibling rejection. Both now
  enforce exact surface-owned outer/inner shapes.
- SSE I/O and HTTP transport exceptions could escape without typed completion. They now map to
  sanitized terminal failure, dispose upstream, and never synthesize `[DONE]`.
- Old `InProgress` audit rows had no durable reconciliation path. A bounded scoped recovery
  service and hosted worker now recover stale rows with optimistic-concurrency handling.
- ComfyUI current-state resolution was placed in AgentFramework. Workspace now owns exact
  publication/profile/model/secret/eligibility re-resolution; AgentFramework is a narrow mapping
  bridge with no image-path persistence query.

## Semantic reopen blockers repaired

- Chat and Responses now use their distinct exact function tool, named choice, structured-output,
  text-part, and image-part shapes. Named choices must reference a declared tool; optional
  descriptions/parameters/schema/strict values are typed and bounded.
- Role/content rules are exact. Assistant omitted/null content requires valid tool calls. Chat
  images are user-only. `parallel_tool_calls` requires advertised support on field presence,
  including false.
- Data images require allowlisted detail/media type and non-empty valid bounded base64. Remote
  URLs, cross-surface forms, and unknown siblings fail. All five Production descriptors retain
  no-vision support.
- Images `n` now distinguishes absence from malformed presence: only absence defaults to 1;
  null, non-integer, fraction, overflow, non-positive, and above-limit values reject.
- `ImageCount` is carried through relay usage, invocation completion/record, existing usage
  contribution, and totals without mixing tokens and images. C# and PostgreSQL enforce the same
  operation-aware matrix and 1..16 bound.
- Projection now fails explicitly for inconsistent rows and aggregation validates/checked-sums
  image observations. Invocation completion/contribution/totals constructor and deconstruct arities
  remain 8/16/10.
- Real PostgreSQL-backed compatibility evidence now covers Workspace routing, current secrets,
  audit rows, usage projection, hosted stale recovery, and image target resolution. Only the
  external dispatcher is a neutral recording fake.
- Hosted recovery timing is deterministic without a public test knob: production uses 10 seconds/
  one minute; the friend integration assembly replaces an internal immutable schedule with
  100 ms/100 ms.

## Confirmed design and containment properties

- Web exposes exactly three invoke-authorized POST routes and delegates to the neutral Workspace
  application port. There is no wildcard inference proxy, audio route, or inference ETag surface.
- Workspace resolves opaque public routing identity against current persisted publication/profile/
  model/secret state before constructing a detached target and begins metadata-only audit before
  dispatch.
- Http depends only on Abstractions, owns upstream URI/header/error/usage/SSE behavior, and has no
  Workspace, Web, EF, secret-store, or access-context dependency.
- Exactly five Production rows exist: OpenAI chat/image, Ollama local/remote chat, and ComfyUI
  image. Missing/duplicate registration and unknown typed combinations fail rather than fallback.
- Function tools round-trip for client execution; central does not execute them. Hosted/built-in
  tools and unsupported advanced fields fail before dispatch.
- Response headers and failures are reconstructed from typed bounded policy. Caller authorization,
  context, cookies, private upstream metadata, raw bodies, endpoints, and credentials are not
  reflected.
- Audit and usage contain identifiers/timing/outcome/category/count metadata only—never prompts,
  responses, image bytes, tool arguments, or secrets.

## Honest proof boundaries

The exact compatibility lane proves real Web/Workspace/PostgreSQL/secret/audit/usage/recovery/
image-resolution behavior while substituting a neutral external dispatcher. It does not prove a
live OpenAI, Ollama, or ComfyUI network hop. SB07 owns multi-host/live-hop proof.

Finalizer source has one cached terminal task, three bounded attempts, and a bounded token. The
unit Fact proves transition/idempotency and the hosted PostgreSQL Fact proves recovery of a stale
row while preserving fresh/terminal rows. No test forces finalizer `SaveChanges` failure, so this
review does not claim a tested transient-finalizer-persistence-failure path.

The early Kestrel `Server` and deterministic TestServer `Content-Length` assertions were corrected
because they were host artifacts, not relay leakage/buffering signals. Upstream sentinels remain
denied, and both first-byte-before-completion gates remain. Read-only stream-double
`NotSupportedException` overrides were manually classified, not treated as product stubs.

## Final evidence assessment

- Web, Unit, and Integration Release builds: zero warnings, zero errors.
- Exact governed runs: relay policy 24/24, OpenAI compatibility 22/22, streaming 12/12.
- Supporting affected usage aggregation: 7/7.
- Downstream state/persistence/deletion revalidation: 18/18, 14/14, 6/6; no pending EF changes.
- EF configuration, migration, designer, and snapshot carry aligned operation-aware usage SQL.
- The final anti-stub selection passes across 69 governed files and the final secret/content scan
  passes.
- Refreshed snapshot `snap-20260825051057-300644c7` covers 14 projects, 752 documents, 35
  modules, 5,158 dependency edges, and 34 direct references. It reports zero project cycles, the
  governed two module/one type cycles, and zero error findings; no reverse project edge was added.
- Final independent audit: PASS, no residual SB04 code/test blocker.

Source-sync, real external providers, three-instance deployment, UI, exported OpenAPI, and final
aggregate regression remain in their owning downstream subbundles.

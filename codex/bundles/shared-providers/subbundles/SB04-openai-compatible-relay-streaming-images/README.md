# SB04 — Bounded OpenAI-compatible relay, streaming, tools, images, and usage

State: `DONE`
Proof tier: `Governed`  
Depends on: `SB03`  
Next on pass: `SB05`

## Objective

Implement central inference POST routes through an adapter registry with strict feature policies, streaming/cancellation, OpenAI errors, images, and metadata-only usage/audit.

## Observable outcome

A caller can use published OpenAI/Ollama/ComfyUI capabilities through a truthful tested OpenAI-compatible subset without creating an open proxy or central tool runtime.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Add/complete SharedProviders.Http implementation and typed HttpClient registrations.
- Implement adapter registry keyed by connector and operation support.
- Implement OpenAI-compatible and Ollama-compatible text relay adapters.
- Implement ComfyUI image mapping through existing provider image capability.
- Implement bounded parsers/policies for Responses, Chat Completions, and Images.
- Implement routing resolution, publication gate, secret resolution, effective upstream target, and no caller-controlled endpoint/header.
- Map POST /api/shared-providers/openai/v1/responses.
- Map POST /api/shared-providers/openai/v1/chat/completions.
- Map POST /api/shared-providers/openai/v1/images/generations.
- Implement normal and SSE response handling with ResponseHeadersRead, flush, idle timeout, cancellation and safe header allowlist.
- Relay client function-tool schemas/tool calls; reject hosted/built-in tools.
- Enforce structured-output/vision capability intersections.
- Implement OpenAI-compatible errors and safe upstream error mapping.
- Create/finalize metadata-only invocation records and existing usage projection integration.
- Ensure access context is recorded centrally and stripped upstream.
- Add deterministic in-process HTTP fixture tests; no live provider.

## Out of scope

- No client source/import implementation.
- No audio/batch/fine-tuning/file APIs.
- No central execution of client tools.
- No UI.
- No three-app Docker lane yet.

## Implementation sequence

1. Freeze supported fields per surface and document denied feature discriminators.
2. Use explicit normalized wire models or validated JsonDocument; never blind forward extension data.
3. Resolve only public routing IDs and replace with stored upstream model.
4. Use adapter-owned upstream URI/auth normalization and disable/revalidate redirects.
5. Stream without buffering the whole response; tee only bounded usage/event metadata.
6. Map ComfyUI bytes to bounded `b64_json` only, never a file path, private URL, or unimplemented artifact URL.
7. Capture provider usage when present; mark unavailable otherwise.
8. Finalize invocation record on success/failure/cancel using idempotent semantics.
9. Add architecture guardrail proving endpoint has no connector switch/open proxy behavior.
10. Update catalog capabilities to the exact passing adapter matrix if tests narrow support.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Web owns HTTP surface. Workspace owns publication/target/secret/audit application behavior. Http integration owns upstream protocols and streaming. Local tools remain client-owned.

## Dependency Direction

Workspace depends on inference transport abstraction only. Composition registers Http. Http has no Workspace EF/Web/Razor reference; it receives neutral targets/requests.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Registry/factory adapters, policy validators, streaming session abstraction, typed error mapper, metadata observation.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Deterministic scripted upstream covers every normal/error/stream/tool/image behavior. Real Web integration proves envelopes and cancellation.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

No large endpoint/runtime partial. One adapter per cohesive connector family and one surface policy per protocol.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Adapter registration/support matrix.
- Supported/denied field matrix.
- Normal Responses/Chat/Image fixtures.
- SSE first-byte/chunk/terminal/cancel evidence.
- Function tool round-trip and built-in tool rejection.
- Structured/vision allow and deny.
- OpenAI error mapping.
- Access-context upstream absence.
- Invocation/usage metadata and content/secret scan.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderRelayPolicyTests` | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | `FullyQualifiedName~SharedProviderRelayPolicyTests` | 24 | Covers allowlist, routing, capability, errors and usage extraction. |
| `SharedProviderOpenAiCompatibilityIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests` | 22 | Covers Responses/Chat/tools/structured/image and errors through real Web HTTP. |
| `SharedProviderStreamingIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderStreamingIntegrationTests` | 12 | Covers incremental SSE, terminal usage, cancellation, timeout and header policy. |

Before running a test topic:

1. build the owning production/test assembly;
2. run `--list-tests` when it is a .NET test lane;
3. compare actual discovery with the planned count;
4. update the planned count only before execution and with a written implementation-based
   reason;
5. reject zero discovery;
6. record transcript and counts in `proof/proof-manifest.json`.

Do not run an unfiltered project or broader lane unless this subbundle explicitly owns it.

## August 25 wire-contract reopen

SB07 proof invalidated the SB04-owned Responses allowlist/wire contract. The reopened contract is
exact and retention-safe: an omitted Responses `store` member is added to the canonical upstream
request as JSON `false`; explicit JSON `false` is accepted; `true`, `null`, strings, numbers,
objects, and arrays are rejected before dispatch. The request never relies on an upstream provider
default for retention.

The reopen retained the three frozen selections and their planned discovery counts. Existing Facts
were strengthened with real-Web and recorded-upstream assertions, so no new Fact changed discovery:

- relay policy: 24 discovered and 24/24 passed;
- OpenAI compatibility: 22 discovered and 22/22 passed;
- streaming: 12 discovered and 12/12 passed.

The chronology is preserved honestly. `proof/transcripts/sb04-reopen-entry-validator.txt` passed.
The first Unit build failed with four missing `System.Text.Json` symbol errors and the final Unit
build passed with zero warnings/errors. The Web build passed with zero warnings/errors. The first
Integration build failed because the new assertion referenced a nonexistent fixture constant and
the final Integration build passed with zero warnings/errors. The authoritative reopen transcripts
are listed in `proof/manifest.md`; the earlier failures are evidence of repair, not hidden or
represented as passing gates.

## Acceptance criteria

- Published route invokes exactly its upstream target.
- Duplicate model names cannot cross-route.
- Tools round-trip for client execution.
- Streaming is incremental and cancellable.
- ComfyUI image publication works through Images route.
- Missing usage is not fabricated.
- No advanced denied feature is forwarded.

## Negative proof

- Unknown/unpublished/mismatched-purpose model fails.
- Caller-supplied upstream URL/header is rejected or impossible.
- Persistence-enabling or malformed `store` values and background/web/file/MCP/computer/code-interpreter tool forms fail.
- Oversized body/tool/image limits fail before upstream.
- Access context and client Authorization are absent upstream.
- Raw upstream error/body/private URI is not reflected.

## Semantic invariants

- Central is not an arbitrary proxy.
- Client tools execute on the client, not central.
- Access context/auth/content/secrets do not reach the upstream or logs.

## Evidence artifacts

At minimum:

- completed `proof/proof-manifest.json`;
- command transcripts under `proof/transcripts/`;
- changed-file inventory;
- architecture/reference artifacts;
- focused behavior artifacts;
- completed `SESSION-HANDOFF.md`;
- updated root `STATUS.md` and traceability rows.

## Progression gate

Pass only when every acceptance criterion, architecture assertion, focused build/test, and
negative proof is backed by an artifact. On pass mark this subbundle `DONE`, unlock only
`SB05`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- Upstream fixture reveals OpenAI/Ollama incompatibility requiring catalog capability change.
- Current image capability cannot safely produce a public OpenAI response.
- Usage integration would require a competing ledger.
- Streaming abstraction would force Web types into Http public contracts.

## Execution checklist

- [x] Current branch/commit/worktree captured.
- [x] Mandatory skills loaded.
- [x] Bundle and subbundle readiness validated.
- [x] Dependencies are `DONE`.
- [x] Before architecture/reference evidence captured.
- [x] Scope implemented without widening.
- [x] Affected production projects built.
- [x] Test discovery recorded and nonzero.
- [x] Focused positive/negative tests passed.
- [x] Security/redaction checks passed where applicable.
- [x] After architecture/reference evidence captured.
- [x] Proof manifest completed with artifact hashes.
- [x] Session handoff completed.
- [x] Status/traceability/review updated.

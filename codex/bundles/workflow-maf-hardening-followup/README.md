# CanDoItAll Workflow MAF Hardening Follow-up

## Validation Summary

- Bundle preparation status: `Valid for execution`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `SB01-SB08 passed`
- Final closure gate: `Passed`
- Browser validation analytics: `SB07 workflow editor backend selector proof passed`

This bundle is a follow-up coordination package for the `processes-hardening` branch after the first Workflow MAF hardening pass was pushed.

## Mission

Polish the runtime correctness, production honesty, approval/HITL integration, event fidelity, checkpointing, artifact policy, and MAF package baseline around `CanDoItAll.AgentFramework.*`, `CanDoItAll.Modules.AgentFramework`, and plugin workflow executors.

## Observed baseline

- Repository: `fyziktom/CanDoItAll`
- Branch: `processes-hardening`
- Observed head: `0c5876df0fe42ffe3ecd2757257770683a9fb041`
- Previous bundle folder: `codex/bundles/workflow-maf-hardening`
- Codex closed the previous bundle as passed, but explicitly deferred MAF package migration and durable production backends.
- Current MAF packages observed in `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` still reference the `1.6.2` stable line and `1.6.2-preview.260521.1` A2A preview line.
- NuGet observed `Microsoft.Agents.AI.Workflows` `1.8.0` as available on 2026-05-28, so this follow-up starts with an explicit package/API delta gate.

## Outcome contract

The follow-up is complete only when:

1. MAF package baseline is intentionally upgraded to the latest compatible line or captured in a fresh ADR with exact blockers.
2. Human-input and approval-required executor flows no longer short-circuit the whole graph merely because a human node exists somewhere in the definition.
3. In-process runtime event persistence preserves useful node/executor identity and consumes streaming events where needed for HITL, requests, and checkpoint capture.
4. Checkpoint and resume capabilities are introduced behind an explicit abstraction and a trust-boundary policy.
5. Artifact and payload policies are consistently enforced for input, output, events, executor results, and plugin/tool receipts.
6. Plugin workflow executor permission policies are validated against plugin capabilities and observed through a deterministic, order-independent execution observer composition.
7. Runtime backend catalog/UI surfaces clearly distinguish registered/runnable backends from planned or unavailable production backends.
8. Final evidence is small, reproducible, and tied to targeted unit/component/integration tests.

## Recommended execution order

1. `subbundles/01-maf-1-8-upgrade-and-api-delta-gate`
2. `subbundles/02-hitl-and-approval-gate-runtime`
3. `subbundles/03-streaming-events-and-node-identity`
4. `subbundles/04-checkpoint-and-resume-foundation`
5. `subbundles/05-artifact-and-payload-policy-hardening`
6. `subbundles/06-plugin-permission-contract-and-observer-composition`
7. `subbundles/07-backend-catalog-and-production-runtime-honesty`
8. `subbundles/08-final-regression-ci-and-evidence-cleanup`

## Hard boundaries

- Do not run live Gmail, Office365, Docker, or host-command workflow proof unless explicitly enabled by an operator and guarded by test configuration.
- Do not silently fall back from a durable production backend to in-process execution.
- Do not make secrets, OAuth tokens, authorization headers, or full unbounded payloads visible in events, logs, assertions, screenshots, or proof transcripts.
- Do not replace the canonical CanDoItAll workflow persistence model with MAF-native model persistence. Use adapters.
- Do not introduce dynamic `IServiceProvider` executor resolution inside per-node execution.
- Do not leave tests as proof-only transcripts. Add source-level tests that will fail on regressions.

## Bundle layout

- `inputs/` original request and review inputs
- `analysis/` current implementation review and follow-up findings
- `requirements/` normalized testable requirements
- `architecture/` target solution and runtime boundaries
- `plan/` staged execution plan
- `references/` MAF and repo review notes
- `subbundles/` execution-ready workstreams
- `shared-prompts/` reusable Codex prompts
- `traceability/` requirement-to-subbundle matrix
- `reviews/` bundle self-review template

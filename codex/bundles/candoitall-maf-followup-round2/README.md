# CanDoItAll MAF stabilization follow-up bundle — round 2

Date: 2026-04-27

This bundle audits the latest post-Codex repository snapshot and provides the next execution-grade implementation package for Codex.

## Verdict

The latest implementation is materially stronger than the previous snapshot. The core process automation path now requests a required finalizer, validates machine output before assistant-message persistence, carries structured output through approval continuation, supports bounded JSON-extraction repair, and contains the previously missing hardening tests.

However, the architecture still has a few important stabilization gaps:

1. The MAF runtime attaches finalizer tools and exact-once finalizer instructions based only on `structuredOutput`, not on the effective `AgentFinalizerMode` stored in execution metadata.
2. Required-finalizer instructions still conflict with `ChatResponseFormat.ForJsonSchema(...)` because they call normal assistant text display-only while the run also requires JSON-schema output.
3. Tool-policy middleware still wraps ordinary `InvalidOperationException` / `NotSupportedException` thrown by tool execution as policy blocks.
4. Provider capability truth is split: the core `ProviderFeatureMatrix` says Ollama has no structured-output support, while Workspace UI defaults still mark Ollama structured-output capable and managed SQLite provider DB fields remain misleading.
5. Hardening tests exist now, but the current test depth is still too static for the most important runtime invariants.

## Priority order

1. Finalizer mode-aware runtime composition.
2. Finalizer instructions aligned with response format semantics.
3. Tool policy exception boundary.
4. Provider capability UI/DB truth alignment.
5. Behavioral hardening tests for the above.
6. Optional finalizer sequence invariant.
7. Optional `RunAsync<T>` evaluation for compile-time typed flows.

## Bundle contents

- `audit/current-state-audit.md` — detailed audit of the latest snapshot.
- `audit/evidence-map.md` — concrete file and line evidence.
- `requirements/requirements.md` — normalized requirements R01-R09.
- `subbundles/*` — implementation-grade Codex work packets.
- `shared-prompts/codex-master-prompt.md` — master prompt for Codex.
- `shared-prompts/codex-qa-prompt.md` — independent QA prompt.
- `reviews/readiness-gate.md` — final acceptance criteria and commands.
- `scripts/validate_bundle.py` — structural bundle validator.

## Non-goals

Do not rewrite the custom process engine into full MAF workflows in this round. The current process dispatcher is already capable of multi-agent process work. This bundle focuses on runtime correctness, policy boundaries, capability truth, and tests.

## Execution Status

Implemented on 2026-04-27.

Mandatory `dotnet --info`, `dotnet restore CanDoItAll.slnx`, and `dotnet build CanDoItAll.slnx --configuration Release --no-restore` passed. Mandatory `dotnet test CanDoItAll.slnx --configuration Release --no-build` ran and failed in unrelated broad suites; exact failure categories are recorded in `reviews/01-execution-report.md` and `docs/agent-runtime-hardening-verification.md`.

Focused round2 proof passed for finalizer mode-aware runtime composition, JSON-compatible finalizer instructions, dedicated tool-policy exception boundaries, provider capability truth, finalizer sequence enforcement, and typed-output evaluation documentation.

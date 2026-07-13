# CanDoItAll Process MAF Hardening Implementation

This initiative bundle converts the GPTPro Extended hardening analysis into an implementation-ready CanDoItAll bundle.

## Profile

- `initiative`

## Mission

Repair the recurring blocker class where large or nested process runs repeat manager rework because the process runtime cannot deterministically diagnose, bridge, or satisfy tool, artifact, subprocess, and AgentFramework observation contracts. The blocked `prepare-solution-skeleton` run in the 5032 instance is the concrete example, but the bundle scope is broader: every subprocess parent, child terminal handoff, artifact expectation, template contract, result-summary path, and runtime tool preflight surface must be audited and hardened.

## Outcome Contract

- Requested outcome: prepare the implementation bundle only. Do not change production source during bundle preparation.
- Raw source authority: preserve and cite `repo://codex/bundles/candoitall-process-maf-hardening-analysis`, including GPTPro findings F01-F12, roadmap B01-B07, data files, evidence notes, and mermaid flow diagrams.
- Scope expansion required by user: do not stop at `prepare-solution-skeleton`; analyze all subprocess process templates, shared artifact templates, and runtime/MAF adapter surfaces that can produce the same hidden contract failure.
- Architecture requirement: use C# architecture gates, CodeAnalytics evidence, pattern selection records, dependency-direction review, partial-class policy, and testability contracts before implementation starts.
- Hard constraints: keep generic process runtime domain-neutral; keep .NET-delivery specifics in templates, typed template metadata, drivers, or module integration; do not add another large partial-file dumping ground; preserve backward-compatible template loading while introducing typed contracts; never hide failures behind silent fallback or blind retry.
- Completion rule for this bundle: a later implementation agent can execute each subbundle in order without rediscovering the problem, guessing source files, weakening GPTPro scope, or inventing proof rules.

## Source Inputs

- `inputs/00-original-request.md` captures the user request.
- `inputs/gptpro-analysis-source/` contains a preserved copy of the GPTPro analysis pack.
- `inputs/02-structured-input.md` normalizes the requested implementation program.
- `requirements/01-normalized-requirements.md` maps GPTPro findings F01-F12 to bundle requirements R01-R15.
- `inventories/` enumerates process template, child handoff, artifact template, and source-code surfaces inspected locally.

## Bundle Layout

- `inputs/` raw request, source artifacts, GPTPro pack copy, and structured input
- `analysis/` current state, assumptions, risks, validation gaps, and reopen triggers
- `requirements/` normalized requirements with acceptance signals
- `architecture/` target architecture and C# architecture guard artifacts
- `inventories/` process/template/source inventories, including all subprocess parents
- `templates/` implementation-time templates and skeletons
- `plan/` phase sequence, dependency map, critical foundations, and architecture checkpoints
- `traceability/` finding and requirement coverage
- `shared-prompts/` implementation and QA prompts
- `subbundles/` numbered executable workstreams
- `proof/` prepared proof-manifest placeholders for critical subbundles
- `reviews/` self-review, execution report shell, and C# architecture gate

## Recommended Execution Order

1. `subbundles/01-source-inventory-and-failing-scenario-characterization`
2. `subbundles/02-exact-observation-diagnostics-and-blocked-step-packet`
3. `subbundles/03-structured-process-result-summary-persistence`
4. `subbundles/04-typed-subprocess-contract-model-and-template-loader`
5. `subbundles/05-runtime-owned-parent-subprocess-bridge`
6. `subbundles/06-artifact-descriptors-materialization-and-ledger-consistency`
7. `subbundles/07-exact-runtime-tool-preflight`
8. `subbundles/08-template-hardening-across-process-and-artifact-contracts`
9. `subbundles/09-regression-harness-and-architecture-closure`

## Critical Path

- SB01 is the audit foundation. It must prove the inventory covers GPTPro F01-F12 plus all current subprocess templates before implementation changes start.
- SB02 and SB03 fix diagnosability and persisted process result truth. Later bridge and preflight work must consume the structured blocked packet/result-summary path.
- SB04 defines the typed contract model that SB05 and SB08 depend on.
- SB05 makes subprocess ownership deterministic and must not proceed without SB04 contract semantics.
- SB06 makes produced artifacts content-grounded and fixes ledger consistency. SB05 and SB08 proof depends on this artifact truth model.
- SB07 prevents missing/denied tool loops before agent execution.
- SB08 hardens all affected templates and shared artifact contracts after runtime support exists.
- SB09 closes with focused unit/integration tests, template validation, current blocked-run recovery guidance, CodeAnalytics refresh, and C# architecture review.

## Validation Summary

- Bundle preparation status: `Prepared; automated validator passed on 2026-07-08`
- Execution status: `Completed on 2026-07-08`
- Subbundle gate review: `Seeded, pending implementation-time entry gates`
- Final closure gate: `Completed; validator passed after implementation`
- CodeAnalytics snapshot used during preparation: `snap-20260708104406-98263759`
- Snapshot scope: process runtime/application/projections/templates/contracts/core/builder/drivers, `CanDoItAll.Modules.Processes`, relevant MAF core/models/tooling/tools/maf projects
- Dependency cycle result: `[]` from CodeAnalytics dependency query
- Snapshot caveats: class diagrams for large projects were truncated to 80 types; non-blocking `Microsoft.OpenApi` advisory warnings appeared in unrelated app/test/tool projects.
- Browser validation analytics: planned as `N/A` for most backend/runtime subbundles; host-visible/operator-message validation is required where projection text changes. UI/browser evidence is required only if implementation changes Blazor rendering or operator views beyond text/projection data.

## Ready-To-Execute Gate

Prepared-stage validation passed during bundle preparation:

```powershell
python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --stage prepared codex\bundles\candoitall-process-maf-hardening-implementation
```

Rerun the command before implementation if bundle files change. The manual readiness gate is recorded in `reviews/00-bundle-self-review.md` and `reviews/csharp-architecture-gate.md`.

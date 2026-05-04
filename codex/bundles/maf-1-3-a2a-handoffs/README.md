# MAF 1.3 A2A Handoffs Agent Cooperation

This bundle coordinates the MAF 1.3 upgrade, default model migration to `gpt-5.4-mini`, and first-class A2A/handoff cooperation work for CanDoItAll agents and governed process flows.

## Profile

- `initiative`

## Mission

Upgrade the CanDoItAll agent runtime to the current Microsoft Agent Framework .NET 1.3 package line, expose A2A and handoff cooperation through typed agent/process contracts, and make software-delivery and business-analysis agents enter process steps with the tools, context, and artifact obligations needed for downstream QA to proceed from real evidence.

## Reopened Live Regression

- `2026-05-03`: Live run `cf086486-2424-487b-bd29-bfc3c111f307` proved the closed bundle still allowed an implementation agent to reach a process step with an unusable scaffold/build/test surface. See `inputs/03-live-process-tool-profile-regression.md`.
- `2026-05-03`: A manual rerun of the same live run failed after the recovery prompt recursively embedded previous recovery directives. The fix keeps prior blocked/failure causes but strips prior rendered recovery packets from new manual rerun directives and no longer stores the full rendered directive in the assignment decision reason.
- `2026-05-03`: The same software-delivery process could still block after valid implementation artifacts and proof were already recorded because the finalizer/retry attempt was judged only against current-attempt source reads. The dispatcher now carries implementation proof and current-step recorded artifacts across retry/finalizer attempts, while resetting that proof after any fresh concrete product mutation.
- `2026-05-03`: Reopened again after run `3bdbfe3e-7562-4ecc-96e3-8faff16192be` still blocked on implementation. The bundle now carries an explicit per-step process-agent input/output contract matrix and live run id controls in `analysis/03-process-agent-contracts-and-live-data.md`; verification must exercise each step-agent independently before a full-chain run.
- `2026-05-04`: Agent-by-agent live testing covered two different app topics: Basic App run `908bfd0f-4039-432e-914b-b8a7c35f17ae` and Harbor Shift Scheduler run `ce0da97a-ece3-46ec-b0b2-c443271d8d8d`. Both runs now end `Completed` with durable scoped artifacts and no blocked steps.
- `2026-05-04`: Harbor live testing found and repaired path-grounding drift, provider-native browser artifact projection gaps, bounded browser MCP context issues, repair-branch disposition confusion, terminal repair-escalation no-go completion semantics, and final status/reason distinctions for failed tools versus blocked proof gaps.
- Reopened scope: `06-tool-availability-profiles`, `09-process-flow-integration`, and `11-validation-and-operator-proof`.
- Repair decision: effective runtime workspace access, including trusted process workspace-tool profile overrides, must drive both configured workspace tools and catalog `workspace-plugin` tool exposure.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-maf-1-3-upgrade-contract`
2. `subbundles/02-default-model-and-provider-seeds`
3. `subbundles/03-a2a-agent-registry-and-hosting`
4. `subbundles/04-handoff-workflow-runtime`
5. `subbundles/05-process-artifact-handoff-enforcement`
6. `subbundles/06-tool-availability-profiles`
7. `subbundles/07-context-session-and-compaction-policy`
8. `subbundles/08-architecture-review-gate-1`
9. `subbundles/09-process-flow-integration`
10. `subbundles/10-architecture-review-gate-2`
11. `subbundles/11-validation-and-operator-proof`
12. `subbundles/12-final-architecture-review-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Subbundles 01-12 completed; live tool-profile, manual-rerun prompt recursion, implementation retry proof/artifact, browser-artifact, branch-disposition, and terminal escalation repairs completed`
- Subbundle gate review: `Completed after reopening subbundles 06, 09, and 11`
- Final closure gate: `Completed for original scope; live regression repairs completed with agent-by-agent and full-chain proof`
- Browser validation analytics: `Process-product browser proof captured in live Harbor run; no visible CanDoItAll Blazor UI changed`

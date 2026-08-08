# SB12-maf-dependency-graph-repair: Repair MAF compile-time dependency direction

## Metadata

- Phase: D — Dependency direction and process ownership
- Depends on: `SB11-runtime-split-checkpoint`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Remove product-module and misplaced workflow-adapter references from `CanDoItAll.AgentFramework.Maf`, introduce narrow abstractions where required, and prove a cycle-free graph.

## Why this subbundle exists

A framework adapter cannot be replaceable or reusable while it references product modules. The direct Security dependency is used by MCP secret resolution; the Workspace reference appears potentially stale; the workflow adapter reference is used for MAF-native handoff construction.

## Scope

- Extract Security runtime contracts.
- Audit/remove or abstract the Workspace reference.
- Move MAF-native handoff construction to the MAF owner.
- Remove all `Modules.*` project/source references from MAF.
- Update composition and architecture tests.

## Non-goals

- Do not move process artifact recovery until SB13, but do not introduce any new process dependency.
- Do not redesign the entire Security module.

## Required SharedInfo skills

- `csharp-project-boundary-extraction`
- `csharp-dependency-graph-audit`
- `csharp-provider-tool-plugin-isolation`
- `csharp-architecture-review-gate`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify dependencies are closed with an `Unlocked` decision.
2. Rebase/merge the current `development` branch and record HEAD.
3. Read the root architecture, ADR, plan, and evidence files relevant to this scope.
4. Create/refresh a CodeAnalytics snapshot when available.
5. Copy `../../templates/subbundle-proof-manifest.json` to `proof/proof-manifest.json` and fill it during work.
6. Add failing-first or characterization proof before moving behavior.

## Detailed implementation tasks

1. Create `CanDoItAll.Security.Abstractions` in the repository-conventional location. Move:
      - `ISecretRuntimeResolver`;
      - `SecretRuntimeRequest`;
      - runtime purpose and consumer identity contracts required by adapters.
      Keep EF/vault/protector implementations in `Modules.Security`.
2. Migrate `McpCapabilityBuilder` and other MAF callers to Security.Abstractions. Migrate module implementation/registration and tests. Delete duplicate old contracts after caller scan is clean.
3. Audit `CanDoItAll.Modules.Workspace` usage in the MAF project. If no source type is used, remove the stale project reference. If an interface is used, extract exactly that interface to the appropriate lower abstraction and migrate callers.
4. Move `MafHandoffWorkflowFactory`, handoff response projection, and MAF-specific depth guard from `Workflows.MafAdapter` into a MAF-owned Handoffs folder/namespace. Keep workflow compiler/backend/event normalizer in Workflows.MafAdapter.
5. Remove `AgentFramework.Maf -> Workflows.MafAdapter` project reference when no MAF production caller remains. Do not create the reverse reference unless required and cycle-free; prefer independent ownership.
6. Remove every `ProjectReference` from MAF containing `Modules`. Remove every `using CanDoItAll.Modules.*` under the MAF project.
7. Review concrete implementation references (documents/storage/etc.) exposed by capability composition. Remove those that the contributor/factory work made unnecessary. Record any intentionally retained infrastructure reference with a pattern decision and follow-up.
8. Update Hosting and AgentFramework module registration so only outer composition knows product implementations. Add architecture source tests for forbidden references and namespaces.
9. Run CodeAnalytics/direct dependency graph before and after. Explain every new reference and prove no cycles.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Security.Abstractions owns secret runtime contracts. Modules.Security implements them. MAF owns MAF-native handoff construction. Workflow MAF adapter owns stored workflow compilation/backend. Hosting/composition wires implementations.

## Dependency Direction

Target: MAF -> Runtime.Abstractions/Models/Core narrow abstractions/Security.Abstractions/provider/tool abstractions. Forbidden: MAF -> Modules.*, MAF -> Workflows.MafAdapter for runtime handoff.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Project boundary extraction and adapter ownership correction. Solve cycles with smaller contracts, never by moving unrelated code into Common or using reflection/service location.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Security abstraction direct contract tests and module implementation tests.
- MCP secret resolution tests continue to pass.
- MAF handoff integration/unit tests pass from the new owner.
- Workflow adapter isolation tests updated to the new ownership.
- Architecture source tests reject `Modules.*` in MAF.
- Composition smoke resolves all MAF ports and secret runtime implementation.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- MAF csproj has no `Modules` path.
- MAF source has no `using CanDoItAll.Modules.`.
- MAF csproj no longer references Workflows.MafAdapter for handoff.
- Security contracts project contains no EF/UI/module implementation dependency.

Other required proof:

- changed-file and changed-project list;
- before/after responsibility ownership;
- CodeAnalytics snapshot/dependency evidence when available;
- build and test transcripts;
- direct testability proof;
- old-owner shrink/deletion proof;
- no-new-caller proof for compatibility facades;
- privacy/logging review when context or tool data changes.

## Validation commands

- `Build Security.Abstractions, Modules.Security, MAF, workflow adapter, Hosting, solution.`
- `Dependency/cycle audit.`
- `Targeted MCP/handoff/workflow tests.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- MAF has no product-module reference.
- Security and handoff ownership are correct.
- Workspace reference is removed or narrowed through a proper abstraction.
- No cycle or service-locator workaround.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- A cycle appears. Stop and extract a smaller contract.
- Security abstraction begins to absorb vault/EF business behavior.
- Handoff move duplicates implementation or breaks adapter isolation.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- Security.Abstractions
- repaired MAF csproj/source
- moved handoff owner
- dependency before/after proof
- tests

## Downstream unlock

SB13 may start after forbidden-reference tests pass.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Moving `ISecretRuntimeResolver` out of `Modules.Security` must preserve authorization semantics and consumer-purpose constants without pulling persistence into abstractions.
- Removing `Maf -> Workflows.MafAdapter` can expose handoff/event types that need a smaller neutral contract, not a reverse reference.
- `Modules.Workspace` provider gateways and image services require an explicit adapter boundary; copying implementations into MAF is forbidden.
- Internal visibility and test project references may conceal cycles until the full solution builds.

## Safe cutover sequence

1. Extract the smallest contracts first and build after every project-reference change.
2. Move composition wiring outward; do not move implementations inward to make references disappear.
3. Replace each forbidden reference with an adapter registered in Hosting/App composition.
4. Remove the old reference only after contract, implementation, and composition smoke all pass.

## Post-change verification and bugfix procedure

1. Reproduce with fixed operation/run/session/context/authority/scope identifiers and a fake provider or deterministic fixture where possible.
2. Identify the failing stage from persisted activity and telemetry before editing: admission, context, authority, scope, composition, provider, session, tool, approval, output/finalizer, persistence, process, workflow, or UI refresh.
3. Add a failing regression test at the owner boundary. Do not patch the caller merely because the symptom appears there.
4. Compare against SB00 characterization/golden evidence and inspect changed project references and runtime/tool manifests.
5. Apply the smallest cohesive fix, then run focused tests, architecture guards, and the current checkpoint suite.
6. Update `proof/proof-manifest.json`, the risk register, and `proof/SESSION-HANDOFF.md` with the root cause and remaining uncertainty.

## Durable session handoff

Before ending a Claude Code session, update `proof/SESSION-HANDOFF.md` with:

- current commit and working-tree state;
- completed checklist items and changed files;
- exact commands and test results;
- CodeAnalytics snapshot/dependency evidence;
- selected cutover path/flag and observed telemetry;
- unresolved failures with correlation IDs and owning stage;
- the next smallest safe action;
- anything a fallback Claude model must not redo or reinterpret.

Do not rely on chat history as the only handoff mechanism.

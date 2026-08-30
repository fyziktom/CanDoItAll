# SB08 — SharedInfo API skills and schema export

## Status

- State: `Pending prerequisites`
- Proof tier: Behavioral
- Execution: not started; this file is a plan, not proof.

## Objective

Publish one accurate reusable API contract/skill source set and separate reproducible PostgreSQL schema evidence, with honest provenance.

## Covered Inputs

- R08/R10; N05/N06; DC03 and database migration review

## Prerequisites

- SB06 schema and SB07 docs accepted; final runtime source identity known. SharedInfo changes are explicitly future scope; never edit installed copies as source.
- Read root constraints, analysis evidence and plan/02-validation-strategy.md before edits.

## Exact Source References

- `bundle://analysis/docs-contracts-review.md`
- `C:/repositories/CanDoItAll.SharedInfo/codex/skills/_candoitall-api-shared/manifest.json`
- `C:/repositories/CanDoItAll.SharedInfo/codex/skills/_candoitall-api-shared/references/candoitall-web.openapi.json`
- `C:/repositories/CanDoItAll.SharedInfo/tools/validation/Test-CanDoItAllWebOpenApi.ps1`
- `C:/repositories/CanDoItAll.SharedInfo/tools/install/codex/Install-CodexSkills.ps1`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs`

repo:// paths resolve from the product repository; bundle:// paths resolve from this bundle. Absolute SharedInfo references identify the inspected sibling checkout; resolve its actual root with the shared-standards skill when executing elsewhere. Planned new tests below are not claimed to exist.

## Deliverables

- Use the exact export procedure/manifest field inventory in docs-contracts-review.md. Start from a freshly built identified final host at canonical localhost:5032 only under applicable host authority; existing unversioned host observations are not export proof.
- Capture both OpenAPI document paths as bytes into product ignored artifacts, compare, hash and count complete route families/operations/schemas including any non-sharing drift. Do not hand-edit generated JSON.
- Update SharedInfo _candoitall-api-shared snapshot/manifest/README; create candoitall-api-shared-providers source skill with exact five operations and limits; update Agents, LLM Chats, Workflows and operation appendices for actual drift.
- Record clean commit provenance. If an explicitly authorized pre-commit capture is used, preserve baseline branch/commit, workingTreeClean:false and prominent limitation; never auto-commit merely to satisfy capture.
- Run SharedInfo and current skill validators; preview exact package installer, synchronize approved active copies and reread/hash them. Installed-path permission is a final concrete step if required.
- Do not seed publication/import/history tables in the development baseline: they do not exist there. Keep the two source baselines explicit and inspect populated values/ownership, not only row counts.
- Run EF pending-model check and generate idempotent SQL into product artifacts/providers-shared-premerge/schema. Keep all seven existing additive migrations and any repair migration required by an actual schema change; apply only in the two isolated migration/preservation lanes below, never a live profile.

## Dependency Impact

- Unlocks SB09. Any final code/schema change invalidates matching export/skill/SQL evidence; hashes alone do not prove semantic completeness.
- Reopen on changes to: final source/host/build identity, wire schema/routes, SharedInfo manifests/skills, DB model/migrations, installed package hashes.

## Validation Depth

- Proof tier: Behavioral.
- Test project/check selection: SharedInfo Test-CanDoItAllWebOpenApi.ps1, Test-SharedInfo.ps1, current skill validator; Integration MigrationBootstrapIntegrationTests, SharedProviderPersistenceIntegrationTests, ProviderHistoryPersistenceIntegrationTests.
- Selection reason: tests own the changed behavior and concrete regression; no unrelated suite substitutes for missing cases.
- Expected discovery: existing selected classes must be nonzero; enumerate and freeze their exact current FQNs/data-row counts before execution. The following exact named/scenario cases are required, with planned new-case counts where stated:
- OpenAPI byte/hash/complete route-set parity (exact final counts generated, not stale 276/308/486 assumption)
- Skill source/active package hash parity (exact selected package list)
- DevelopmentToFinalUpgrade_PreservesExistingCanonicalDataAndBuildsHistory (1 new if absent)
- ReviewedHeadToRepairs_PreservesSharingHistoryAndTransfer (1 new if absent; migrate only if repair migrations exist)
- Invalidation keys: final source/host/build identity, wire schema/routes, SharedInfo manifests/skills, DB model/migrations, installed package hashes.
- Broad-gate decision: No broad gate here. Exact contract/schema/upgrade checks; final checkpoint SB09.

## Acceptance Checklist

- [ ] Final live document bytes and snapshot hash/route/operation/schema counts match; scalar/request semantics satisfy SB06.
- [ ] One shared snapshot source of truth; new skill and affected API guidance link its manifest and defer to a different target host's live schema.
- [ ] Skill source/active hashes and validator results recorded after authorized synchronization; no raw credentials in artifact.
- [ ] EF model/migrations agree; SQL generated without live DB mutation; lane A upgrades exact development migration 20260822013043_AddWorkflowNativeCheckpointRequestUniqueness to final while preserving existing provider profiles/canonical agent/Simple Chat/workflow data and proving new schema/backfill; lane B starts at reviewed head with populated sharing/history tables and validates repair migrations or preservation/transfer if no migration is needed.
- [ ] Keep strong identifiers/enums, explicit errors, safe logs, Egyptian braces and one statement per line.
- [ ] No production XML comments, unrelated refactor, silent fallback or inferred permission expansion.

## Proof Required

- Follow plan/02-validation-strategy.md for exact Release build/discovery/test command form; record commands, exit codes, expected/actual cases, source hashes and dependency mode.
- Old SharedInfo validator currently passes a stale snapshot; require identified live-vs-export parity, not just old internal hash consistency.
- Record realistic positive and adversarial negative proof, source producer/consumer/lifecycle assertions where applicable, and anti-stub review. Failing-first proof must exercise the reported defect.
- Record evidence in reviews/01-execution-report.md; separate governed manifests are not required for this unit.

## C# Architecture Impact

SharedInfo owns reusable API assets only; product owns exporters/runbook, SQL and evidence. New skill uses existing shared support package rather than duplicating OpenAPI.

## Boundary Ownership

- Keep the responsibility in the named current owner. Any extraction must be independently testable and remove moved logic from the old class.

## Dependency Direction

- Preserve architecture/02-csharp-dependency-direction.md; no new project/reference is assumed. If needed, stop that edit and amend the boundary/checkpoint before proceeding.

## Pattern Decision

- Follow architecture/03-csharp-pattern-selection-records.md. Prefer current adapters/decorators and small functions; avoid abstractions without a concrete boundary.

## Testability Contract

- Pure policies use direct isolated tests; persistence/network behavior uses the selected integration seam and a real production consumer. Do not construct the full runtime for a pure rule.

## Partial Class Policy

- No new runtime partial. Existing generated code and cohesive UI code-behind are allowed; no nested service used to hide responsibility.

## Architecture Proof Required

- Relevant checkpoint: plan/architecture-checkpoints.md. Review .csproj diff, policy placement, production registration, independent tests and no-new-partial proof.
- If behavior is extracted, show old-owner shrink/thin facade and a negative test rejecting delegation back to the monolith. No extraction is required solely for this metric.

## Progression Gate

- Pass only after acceptance and required proof agree; otherwise record precise failed/blocked cases.
- Unlocks SB09. Any final code/schema change invalidates matching export/skill/SQL evidence; hashes alone do not prove semantic completeness.
- Scope beyond the listed repair, new wire support, database destruction, hosted authority or installed-path permission must be handled explicitly; finish all unaffected authorized work first.

## Non-goals

- No merge/push/deployment, paid upstream call, unrelated sibling refactor, invented remote history API or broad UI redesign.

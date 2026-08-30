# SB07 — Maintained product documentation

## Status

- State: `Pending prerequisites`
- Proof tier: Standard
- Execution: not started; this file is a plan, not proof.

## Objective

Maintained docs explain sharing/history operation, ownership and limitations, and the six new projects pass the repository documentation gate.

## Covered Inputs

- R07/R10; N05/N06; DC01/DC04

## Prerequisites

- Drafting may start early; finalize after SB01–SB06 accepted behavior and any migration choices.
- Read root constraints, analysis evidence and plan/02-validation-strategy.md before edits.

## Exact Source References

- `repo://docs/README.md`
- `repo://docs/api-control-plane.md`
- `repo://docs/provider-capability-and-pricing.md`
- `repo://docs/secure-configuration.md`
- `repo://docs/operations/backup-and-restore.md`
- `repo://docs/architecture/overview.md`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/README.md`
- `repo://tools/Validation/Test-Documentation.ps1`
- `bundle://analysis/docs-contracts-review.md`

repo:// paths resolve from the product repository; bundle:// paths resolve from this bundle. Absolute SharedInfo references identify the inspected sibling checkout; resolve its actual root with the shared-standards skill when executing elsewhere. Planned new tests below are not claimed to exist.

## Deliverables

- Create the six exact project READMEs in DC01 using nearby conventions; state responsibilities, dependencies and smallest build/test commands.
- Add docs/shared-providers.md and docs/provider-request-history.md; update indexes/root/Web README, API, architecture, pricing, security, backup and migration guides listed in the documentation review.
- Document metadata/content/manage authorities, opaque external refs, application-visible attempts, Light/Detailed privacy, canonical content, retention/quota/recovery and timeout/stream errors.
- Move current user guidance out of bundle-only notes without copying raw proof logs; link historical evidence separately.
- Record transfer of old SB10 documentation scope to this unit; preserve old authority/outcomes per handoff plan.

## Dependency Impact

- Unlocks SB08 final source/skill guidance. Any changed behavior reopens affected documentation sections; doc-only corrections do not invalidate product binaries.
- Reopen on changes to: maintained docs, public paths, behavior descriptions, authorization scopes, migration inventory.

## Validation Depth

- Proof tier: Standard.
- Test project/check selection: N/A automated tests; tools/Validation/Test-Documentation.ps1 and link/source-parity review are the owning checks.
- Selection reason: tests own the changed behavior and concrete regression; no unrelated suite substitutes for missing cases.
- Expected discovery: N/A; no automated test selection for this documentation-only unit. Required non-test checks:
- Documentation validator: zero findings
- Six listed README paths exist and contain accurate ownership/run/test guidance
- Invalidation keys: maintained docs, public paths, behavior descriptions, authorization scopes, migration inventory.
- Broad-gate decision: No product tests/broad gate for documentation-only edits.

## Acceptance Checklist

- [ ] Test-Documentation.ps1 reports zero missing project README/link/metadata errors.
- [ ] Operator can configure publication/import and distinguish catalog scope, invoke scope, history UI/service authority and unsupported remote APIs.
- [ ] Docs describe backup dependencies (DB, canonical files/journals, protection keys), recovery, schema deployment and truthful limitations.
- [ ] Keep strong identifiers/enums, explicit errors, safe logs, Egyptian braces and one statement per line.
- [ ] No production XML comments, unrelated refactor, silent fallback or inferred permission expansion.

## Proof Required

- Record documentation validator command, exit code/output, changed-document paths and source/link/claim review. No Release product build or test discovery is required for documentation-only edits.
- Passing file-existence checks with copied boilerplate is insufficient; review claims against exact runtime and schema behavior.
- Static/diff validation is proportionate; no tests for documentation text alone.
- Record evidence in reviews/01-execution-report.md; separate governed manifests are not required for this unit.

## C# Architecture Impact

Product owns durable guides and project READMEs; shared standards stay in SharedInfo. No production code or API invention in this unit.

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
- Unlocks SB08 final source/skill guidance. Any changed behavior reopens affected documentation sections; doc-only corrections do not invalidate product binaries.
- Scope beyond the listed repair, new wire support, database destruction, hosted authority or installed-path permission must be handled explicitly; finish all unaffected authorized work first.

## Non-goals

- No merge/push/deployment, paid upstream call, unrelated sibling refactor, invented remote history API or broad UI redesign.

# Independent contract and documentation readiness review

Reviewed 2026-08-30 against the draft root README, plan/01-phase-plan.md, plan/02-validation-strategy.md, plan/03-historical-handoff.md and SB06-SB09. Production code, SharedInfo, and bundle implementation files were not changed. Execution-report and traceability scaffolding was excluded as requested.

Decision: one P2 preparation correction required; otherwise the documentation/schema/authority design is consistent with inspected repository contracts.

## IC01 — P2: split the two materially different upgrade baselines

Targets: subbundles/08-sharedinfo-and-schema-export/README.md:55 and :64; plan/02-validation-strategy.md SB08 row; corresponding SQL/upgrade instructions in analysis/docs-contracts-review.md.

The planned PopulatedDevelopmentToFinalUpgrade_PreservesProviderHistoryAndSharing case and acceptance wording combine development-to-final upgrade with preservation of populated publication/import/provider-history records. Those records cannot exist at the stated development baseline. Git tree inspection confirms development 1625b336 ends at 20260822013043_AddWorkflowNativeCheckpointRequestUniqueness. The 20260824224847_AddSharedProviderPersistence migration creates the publication/source/import tables, and 20260828164039_AddProviderRequestHistory creates provider-history tables.

Using the latest model to seed the baseline would invalidate the upgrade proof; using empty new tables would make the requested preservation check vacuous.

Precise repair:

1. Define a development migration rehearsal from exactly 20260822013043_AddWorkflowNativeCheckpointRequestUniqueness to the final migration. Seed only records that exist at that baseline: local provider profiles/configuration and canonical agent/Simple Chat/workflow evidence. Assert those survive and the new feature tables/constraints/backfill behave correctly.
2. Define separate current-feature preservation proof from the reviewed head migration 20260830104752_AddProviderHistoryExternalReference to final repairs. Seed publications/imports, history entries/details, owners, policy/quota and relevant transfer state before applying any new repair migration. If the repairs need no migration, run populated persistence/retention/transfer regression at this schema and record that the migration delta is empty; do not manufacture a migration.
3. Give each selector an explicit baseline migration id, realistic seed and negative assertion. Name the cases to match these separate obligations, and record actual discovery at execution.
4. In SB09 reuse these identified SB08 results if source/config/schema hashes are unchanged; only repeat invalidated migration proof. This is consistent with the plan's bounded validation policy.

## Small clarity corrections

- SB07 Validation Depth says N/A automated tests, but the next generic bullet requires nonzero class/FQN/data-row discovery, and Proof Required directs a Release build/test transcript. Replace those two generic instructions for SB07 with N/A test discovery; record documentation validator exit/output and source/link/claim review. The current later text already says no product tests for doc-only edits.
- SB08 deliverable says keep all seven existing additive migrations and future repair migration. Change to keep all seven existing additive migrations and any repair migration required by an actual schema change. Current correctness repairs may not require a schema mutation.

These wording corrections do not authorize broader execution.

## Verified correct

- The five supported operations are exactly catalog GET, models GET, Responses POST, Chat Completions POST and image-generations POST under the routes in SharedProviderProtocol.cs. No remote provider-history or source-management API is invented.
- Both capture URLs are correct: /openapi/v1.json and /swagger/v1/swagger.json at canonical http://localhost:5032. Product-owned byte capture, comparison, hash and full route counting precede SharedInfo publication. The unversioned current host observation is correctly excluded as final-source proof.
- Schema semantics are correctly owned by SB06 before export. Custom string scalar/enum mapping, request allowlists, terminal/error forms, reasoning controls, non-stored Responses and base64-only image response are explicit.
- SharedInfo support snapshot/manifest/README, new candoitall-api-shared-providers source package and affected existing API skills have the right owner. Actual metadata/route-set drift is refreshed; generated JSON is not hand-edited. Installed copies are updated only through the existing installer and applicable authority.
- SQL evidence stays in product artifacts; EF pending-model/idempotent script commands use the real migrations project, Web startup project and AppDbContext. No database apply or live data mutation is implied by export.
- Seventeen explicitly referenced existing paths in SB07-SB09 were checked with Test-Path and exist. Six missing READMEs and the two new guides are correctly described as files to create, not existing sources. Planned new tests are labeled as new.
- The Stable command/filter matches docs/testing.md, including browser/live/long-running/quarantined/portability/Docker exclusions; excluded lanes are not claimed passed.
- Root, historical handoff and SB09 preserve original SB07 Docker ceilings and its separate three-application requirement. Later two-instance proof does not silently close it. Preparation does not authorize a lifecycle, remove a budget, publish artifacts or merge.
- Product documentation targets cover the six failing project READMEs and product API/architecture/pricing/security/backup/migration guides. SharedInfo remains read-only during preparation.

## Checks performed

Read-only source/path/plan inspection and git-tree migration-baseline check. No build, test suite, schema export, host restart, migration apply, Docker lifecycle, SharedInfo write, installed-skill synchronization, commit or merge was run for this independent review.

## Resolution follow-up

IC01 was accepted by the primary agent. The analysis report's migration section now separates exact-development pre-feature record preservation/backfill from reviewed-feature-head populated sharing/history upgrade or preservation. It also explains baseline-specific idempotent SQL generation and matching-evidence reuse. The primary agent owns the corresponding SB08 and validation-plan edits; readiness can pass when those references express the same two baselines. No production source changed.

## Final preparation decision

Passed after correction on 2026-08-30. Narrow re-review confirmed IC01 is resolved consistently in SB08 and plan/02-validation-strategy.md: the exact development baseline preserves only pre-existing records and verifies new schema/backfill; the reviewed feature baseline proves populated sharing/history preservation, with a migration only when a real schema change requires one. The two named cases are explicit. SB09 reuses matching SB08 evidence and reruns only invalidated lanes. SB07 now correctly marks automated discovery N/A and removes the Release product build/test obligation for documentation-only edits.

No unresolved contract/documentation preparation finding remains from this independent review. This pass approves the plan's readiness, not repaired production behavior, schema publication, hosted execution, or merge readiness. Historical authority restrictions and all future execution gates remain unchanged.

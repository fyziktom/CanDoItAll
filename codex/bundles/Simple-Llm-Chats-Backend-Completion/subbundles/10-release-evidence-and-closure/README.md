# SB10 — Release Evidence And Closure

## Status

- `Ready`

## Objective

- Run the one allowed broad release gate at the frozen CP3 commit, obtain same-commit three-OS CI evidence, and make bundle/code/proof/documentation status agree.

## Success Criteria

- Product and Stable solutions restore/build in Release using the same pinned sibling-source graph.
- The current broad stable filter has a frozen expected discovery count, actual discovery matches, and every selected case passes.
- Documentation, architecture, transfer/migration, and EF pending-model checks pass.
- Windows x64, Ubuntu x64, and macOS arm64 CI jobs pass at the exact same application and sibling commits with uploaded provenance/artifacts.
- Independent final verifier finds no stale status, broken link, checksum/hash mismatch, unresolved invariant, or raw secret.

## Covered Inputs

- BC-004, BC-005, BC-090 through BC-092 and closure of every prior requirement.

## Prerequisites

- SB09/CP3 `Pass` and frozen application commit.
- Authority to run/observe the repository CI matrix; otherwise record `Blocked`, never substitute local inspection.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://tests/Solutions/CanDoItAll.Tests.Stable.slnx`
- `repo://docs/testing.md`
- `repo://.github/workflows/ci.yml`
- `repo://Directory.Build.targets`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql`
- `bundle://reviews/01-execution-report.md`
- `bundle://traceability/01-requirement-traceability.md`
- `bundle://plan/architecture-checkpoints.md`

## UI Composition Contract

- N/A — no UI was changed. Existing CI may execute its standard unrelated browser/portability lanes, but they are not claimed as UI feature proof.

## Deliverables

- Frozen commit/dependency provenance and one local broad-gate transcript set.
- Stable filtered listing with expected/actual discovery and result.
- Final EF pending-model, documentation, architecture, secret, link, and bundle-validation results.
- Three-OS CI run identifiers/artifact references tied to the same commits.
- Governed final manifest, checksum index, final red-team/closure decision, and consistent bundle status.

## Dependency Impact

- Final closure only; no downstream implementation.

## Validation Depth

- Proof tier: `Governed`.
- Test solution: `repo://tests/Solutions/CanDoItAll.Tests.Stable.slnx`.
- Filter: `Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true`.
- Selection reason: one release gate is required by the shared ProviderRuntime, public Web contract, Composition/DI, persistence/schema/migration/transfer, and test changes.
- Expected discovery: at the frozen CP3 commit, list this exact filter first and record the exact nonzero case count as the expected release count before executing; independent verification must reproduce it. The count cannot be numerically frozen during preparation because SB02-SB09 intentionally add cases.
- Invalidation keys: application/sibling commit, lock files/source roots, product/Stable solution, filter/category traits, migration snapshot, docs/guards, CI workflow/runner matrix, any production/test change.
- Broad-gate decision: required exactly once here. A changed frozen commit invalidates the run rather than authorizing repeated partial reuse.

## Implementation Steps

1. Verify the CP3 commit and pinned sibling source commits; require a clean product/test tree and record proof-only changes.
2. From repository root, run the current source-mode commands:
   - `dotnet restore ./CanDoItAll.slnx -p:UseLocalCanDoItAllLibraries=true`
   - `dotnet build ./CanDoItAll.slnx --configuration Release --no-restore -p:UseLocalCanDoItAllLibraries=true /m:1`
   - `dotnet restore ./tests/Solutions/CanDoItAll.Tests.Stable.slnx -p:UseLocalCanDoItAllLibraries=true`
   - `dotnet build ./tests/Solutions/CanDoItAll.Tests.Stable.slnx --configuration Release --no-restore -p:UseLocalCanDoItAllLibraries=true /m:1`
3. Run Stable `--list-tests` with the exact filter, freeze/review the expected count, then run the same filter once with `--no-build --no-restore`.
4. Run `dotnet ef migrations has-pending-model-changes --no-build --project ./src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --context AppDbContext --configuration Release`.
5. Run `./tools/Validation/Test-Documentation.ps1`, successor architecture guards/self-tests, secret/artifact scan, bundle preparation validator at closed stage, and semantic closure review.
6. Dispatch/observe the current CI matrix at exactly the frozen commits; require all Windows/Ubuntu/macOS stable/portability/headless steps relevant to the workflow to pass.
7. Have an independent verifier re-hash Governed manifests/transcripts, check raw-input closure and invalidation, and issue `Pass`, `Fail`, or `Blocked`.
8. Update status/traceability/reviews only after every result agrees; never mark Completed on partial/local-only evidence.

## C# Architecture Impact

- No implementation change allowed. Any source change reopens CP3.

## Boundary Ownership

- Verifies the final graph only.

## Dependency Direction

- Must equal CP3; re-query if any generated/build metadata suggests drift.

## Pattern Decision

- One frozen broad gate, not accumulated evidence from changing commits.

## Testability Contract

- Expected/actual discovery and exact filter/commit/configuration are mandatory; zero/mismatch fails.

## Partial Class Policy

- Final guard result must remain clean.

## Architecture Proof Required

- Reuse CP3 only if no invalidation key changed; otherwise rerun CP3. Final verifier checks that fact.

## Scope Exceptions

- Live provider and UI/manual browser certification are not release criteria for this backend bundle.
- Quarantined/excluded lanes are not claimed green; their exclusion is explicit in the filter.

## Do Not Do

- Do not switch to package mode, use `CanDoItAll.slnx` as a test solution, run a different filter, combine OS results from different commits, or rerun broad subsets to hide a failure.
- Do not edit product/test source during final closure.

## Acceptance Checklist

- [ ] Frozen application and sibling commits recorded.
- [ ] Product/Stable restore and Release builds pass.
- [ ] Broad expected/actual discovery matches and filtered Stable run passes once.
- [ ] Pending-model/docs/architecture/secret/bundle validators pass.
- [ ] Same-commit Windows/Linux/macOS CI matrix passes.
- [ ] Independent final verifier and traceability/raw-input closure pass.
- [ ] Status/proof/docs/code agree.

## Proof Required

- Portable command transcripts with timestamps/configuration/commit/discovery/results, CI run/artifact identifiers, hashes, pending-model/docs/guard/secret output, semantic invariants, independent red-team review, and final decision under `proof/SB10` and `reviews`.

## Browser Validation Logging

- N/A — no feature UI. Record existing CI browser artifacts only as workflow provenance, not as a chat UI claim.

## Progression Gate

- Final state is `Pass`, `Fail`, or `Blocked`. `Pass` requires every checklist item; no “pass with missing CI/pending-model/discovery” state exists.

## Reopen Triggers

- Any application/sibling source, test/filter/trait, solution, migration, docs/source-truth, build graph, architecture guard, or CI workflow change invalidates final closure.

## Suggested Agent Prompt

```text
Execute SB10 only at the frozen CP3 commit. Run the current source-mode broad gate exactly once, require matching discovery and same-commit three-OS CI, then obtain independent Governed closure. Do not change implementation or convert missing evidence into residual risk.
```

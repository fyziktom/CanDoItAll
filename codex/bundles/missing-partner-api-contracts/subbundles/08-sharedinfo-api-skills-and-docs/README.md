# SharedInfo API Skills And Docs

## Status

- `Completed`

## Objective

- Publish the final current API contract through SharedInfo and synchronize the changed
  reusable packages to the active Codex skill root.

## Success Criteria

- Shared OpenAPI snapshot and both runtime document paths are byte-identical and traceable
  to a clean current CanDoItAll commit.
- Manifest hash/counts/families/operation sets match.
- Related API skills and references describe every new route, DTO, sequencing, conflict,
  retry, evidence, and security rule.
- SharedInfo and skill/package validators pass; installed copies match source hashes.

## Covered Inputs

- R008 and documentation/skill follow-through for N001-N007.

## Prerequisites

- SB07 closed; CanDoItAll source/build/OpenAPI contract stable.

## Exact Source References

- `C:\repositories\CanDoItAll.SharedInfo\docs\standards\codex.md`
- `C:\repositories\CanDoItAll.SharedInfo\codex\skills\_candoitall-api-shared`
- `C:\repositories\CanDoItAll.SharedInfo\codex\skills\candoitall-api-agents`
- `C:\repositories\CanDoItAll.SharedInfo\codex\skills\candoitall-api-workflows`
- `C:\repositories\CanDoItAll.SharedInfo\codex\skills\candoitall-api-crmhr`
- `C:\repositories\CanDoItAll.SharedInfo\tools\validation\Test-CanDoItAllWebOpenApi.ps1`

## Deliverables

- Fresh OpenAPI JSON, manifest, support README, API skill route appendices/references.
- Validator updates when the shared contract expands.
- Active skill-root synchronization and source/installed hash proof.

## Dependency Impact

- Final publication and closure surface; any mismatch reopens the owning product subbundle.

## Validation Depth

- Proof tier: `Standard` plus canonical host capture.

## Implementation Steps

1. Build/run clean CanDoItAll Web on canonical development URL.
2. Capture and compare `/openapi/v1.json` and `/swagger/v1/swagger.json`.
3. Update snapshot/provenance/counts/families/operation sets.
4. Update related skills/references and routing docs.
5. Run OpenAPI, SharedInfo, and skill/package validators.
6. Install/synchronize changed packages and verify hashes.

## Scope Exceptions

- No product task bundle or proof artifact is copied into SharedInfo.
- The product implementation was intentionally not committed by this task. The
  publication records baseline commit
  `8d65ad1092a0f3bd1089a28b6fe827a7b405fd2c`,
  `workingTreeClean: false`, a working-tree status fingerprint, and a prominent
  limitation note. Refresh commit-clean provenance after the product changes are
  committed.

## Do Not Do

- Do not hand-edit generated OpenAPI.
- Do not publish from a dirty/uncommitted source state without recording the limitation.
- Do not hard-code developer-specific paths in reusable files.

## Acceptance Checklist

- [x] baseline commit and explicit non-clean provenance limitation recorded
- [x] runtime OpenAPI endpoints byte-identical
- [x] hash/path/operation/schema counts match
- [x] agents/workflows/recruiting/processes skills match route sets
- [x] SharedInfo and skill validators pass
- [x] active installed packages hash-match source

## Proof Required

- `Test-CanDoItAllWebOpenApi.ps1`
- `Test-SharedInfo.ps1`
- current skill/package validator
- recursive source-vs-installed file hash comparison for changed packages

## Browser Validation Logging

- N/A; HTTP host capture only.

## C# Architecture Impact

### Boundary Ownership

- N/A; SharedInfo owns reusable docs/assets only.

### Dependency Direction

- No filesystem dependency from product repos to SharedInfo.

### Pattern Decision

- N/A.

### Testability Contract

- Deterministic validators and hash comparison.

### Partial Class Policy

- N/A.

### Architecture Proof Required

- Final product architecture gate must already pass.

## Progression Gate

- All validators/hash checks pass and every raw note has a closure decision.

## Implementation Result

- Captured both runtime documents from the Development host at
  `http://localhost:5032`; their 427,242 bytes and SHA-256
  `A5D9EE04B93A5913CB3AF7004B1F91F7F85A6639CF911F2BA2258316C778B51C`
  are identical.
- Published OpenAPI `3.1.1` with 229 paths, 274 operations, and 342 component schemas.
  Fifteen route families total exactly 229 paths and 274 operations.
- Manifest parity covers all 119 operations in the Agents (64), Agent Recruiting (5),
  Workflows (41), and Processes (9) operation sets.
- Updated Agents, Workflows, CRM-HR, and Processes skills plus detailed partner-contract
  references. The Processes guidance now correctly marks four
  `processes-snapshots`-only durable routes as unavailable on the current branch.
- Extended the validator for explicit dirty-source provenance and multiple named route
  appendices.

## Validation Result

- `Test-CanDoItAllWebOpenApi.ps1`: passed with zero failures.
- `Test-SharedInfo.ps1`: passed with zero failures across 43 skills, 395 Markdown files,
  and 12 PowerShell files.
- Current `quick_validate.py`: all four changed discoverable skills passed in both
  SharedInfo and the active installed root.
- A read-only independent forward test found and closed three documentation defects:
  internal workflow lineage fields removed from the public DTO list, unconditional and
  mode-specific package-import requirements made explicit, and both execution-start
  attachment/global request fields completed.
- Recursive source/install comparison: zero differences for all five packages:
  `_candoitall-api-shared`, `candoitall-api-agents`, `candoitall-api-workflows`,
  `candoitall-api-crmhr`, and `candoitall-api-processes`.
- Installed aggregate SHA-256 digests:
  - `_candoitall-api-shared`:
    `DD06065D045CF88EB06A093026A1CE56E005925D377DF19335DDCE5851591CE3`
  - `candoitall-api-agents`:
    `668A77A5C3CF04F8BC85D3B1B79C5C3D4348186816BB371BFA54F17F2D059A81`
  - `candoitall-api-workflows`:
    `EE1AA939CFD37B50193FABDAB0CD6592B2FE1B35DA3641C75C7ABBBDD2836C84`
  - `candoitall-api-crmhr`:
    `D5CF46C0AE766D488BAF774F45A20BE82FA6C2C3BF8B61979F7C13F550831556`
  - `candoitall-api-processes`:
    `F4B8248260589EA3EA3033FEFEACD8601617174313C898BEF899EDF6E4280C79`
- Final initiative bundle validator: passed at stage `completed`.

## Closure Decision

- R008 is solved with the recorded non-clean source-provenance limitation.
- SB08 closes; all eight subbundles are complete.

## Reopen Triggers

- Product route/DTO changes, snapshot drift, validator mismatch, or installed package drift.

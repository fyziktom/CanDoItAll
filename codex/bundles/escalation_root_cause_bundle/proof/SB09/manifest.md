# SB09 Proof Manifest

## Implementation Scope

- Migrated high-risk process definitions to typed execution metadata:
  - `dotnet-solution-setup` deterministic setup, validation, repair, and revalidation steps.
  - `dotnet-development-slice` runtime-owned subprocess parent steps and branch decisions.
  - `software-delivery` runtime-owned subprocess parent steps and branch decisions.
  - `dotnet-ui-screenshot-writeback` screenshot capture/storage tool plans.
  - `blazor-app-delivery` runtime validation and revalidation branch gates.
- Added `BranchDecision` execution class to existing branch-decision steps across the pack.
- Migrated `branching-code-review` reserved `__default__` and `__error__` branch keys to stable `default` and `error` identifiers, including markdown, toolbox, and seed references.
- Added `SemanticAcceptanceContract` to all six business artifact JSON templates so file existence is never sufficient acceptance proof.
- Generalized strict scanner validation so non-script tool plans can use typed receipts/slots without fake script refs, while `.NET solution` plans still require script refs and readback checks.

## Full Audit

- Full audit table: `proof/SB09/template-audit.csv`.
- Audit rows: 236.
- Source scope covered:
  - 24 process definitions.
  - 155 step markdown files.
  - 15 validation JSON files.
  - 14 prompt JSON files.
  - 16 checklist JSON files.
  - 6 business artifact JSON templates.
  - 6 business artifact markdown templates.
- The live tree has fewer validation/prompt JSON files than the prepared estimate; the proof records current source counts from `Templates/Processes/processes`.

## Validation

- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProcessTemplateCompatibilityHistoryTests" -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb09-unit -p:WarningsNotAsErrors=NU1903 --results-directory repo://artifacts/sb09-test-results --logger "trx;LogFileName=sb09-unit.trx" --logger "console;verbosity=minimal"`
- Result: 11 tests passed, 0 failed. Existing NU1903 `Microsoft.OpenApi` advisory warning only during restore/build graph loading.
- `dotnet build src/Processes/CanDoItAll.Processes.Templates/CanDoItAll.Processes.Templates.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb09-templates-build -p:WarningsNotAsErrors=NU1903`
- Result: build passed with 0 warnings and 0 errors.
- Full-pack strict validation proof: `Template_compatibility_strict_scan_accepts_full_migrated_template_pack`.
- Negative typed-gate proof: `Template_compatibility_strict_scan_rejects_full_pack_when_required_typed_contract_removed`.
- Artifact acceptance proof: `Business_artifact_templates_declare_semantic_acceptance_contract`.
- CodeAnalytics snapshot: `snap-20260708201501-85ab0701`.
- CodeAnalytics dependency cycle query: `cycles: []`.

## File Hashes

- Hash ledger: `proof/SB09/changed-file-hashes.txt`.
- Audit CSV hash: `E931EA145AA44122DBB6A8D6354F203EEE42E343E61C7982E90814ED66EEB361`.
- Hash ledger hash: `8567356D1803CA8A148093E40749ECEA47F66FD22A34B16AC2A75DD7E9B95C95`.


## Completed Validator Metadata

- Semantic invariant contract: proof/SB09/semantic-invariants.md.
- Portable source proof: bundle://subbundles/09-sb09-template-artifact-audit-migration/README.md.
- Portable bundle proof: bundle://proof/SB09/manifest.md.
- SHA-256 changed-file hash: A5FA7424969BD671325F98F1BE46E441D969511B4F400CC92EB81C49C0A469AE.
- Passing transcript: proof/SB09/transcripts/00-validator-metadata.txt.
- Anti-stub audit transcript: proof/SB09/transcripts/00-validator-metadata.txt.
- Failing-first: N/A - process/non-production final proof uses adversarial negative tests or preserved subbundle proof rather than a historical failing transcript.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB09 proof metadata | proof/SB09/manifest.md | proof/SB09/transcripts/00-validator-metadata.txt | final proof closure | proof/SB09/semantic-invariants.md rejects shallow closure |

